using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.State;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Llm;

namespace Narratum.Orchestration.Services;

/// <summary>
/// Implémentation du service d'orchestration.
///
/// Cette implémentation coordonne le pipeline de génération narrative
/// en utilisant une architecture en étapes (stages).
///
/// Pipeline d'exécution :
/// 1. BuildContext - Construit le contexte enrichi
/// 2. PreparePrompt - Prépare les prompts pour le LLM
/// 3. Generate - Génère le contenu via le LLM
/// 4. Validate - Valide la sortie
/// 5. Integrate - Intègre dans l'état (si valide)
/// </summary>
public class OrchestrationService : IOrchestrationService
{
    private readonly ILlmClient _llmClient;
    private readonly IMemoryService? _memoryService;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly OrchestrationConfig _config;

    public OrchestrationConfig Config => _config;

    public OrchestrationService(
        ILlmClient llmClient,
        OrchestrationConfig? config = null,
        IMemoryService? memoryService = null,
        ILogger<OrchestrationService>? logger = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? OrchestrationConfig.Default;
        _memoryService = memoryService;
        _logger = logger ?? CreateNullLogger();
    }

    public async Task<Result<PipelineResult>> ExecuteCycleAsync(
        StoryState storyState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storyState);
        ArgumentNullException.ThrowIfNull(intent);

        var stopwatch = Stopwatch.StartNew();
        var stageResults = new List<PipelineStageResult>();
        var retryCount = 0;

        _logger.LogInformation(
            "Starting orchestration cycle for intent {IntentType}",
            intent.Type);

        // Étape 1: Construire le contexte
        var contextResult = await ExecuteStageAsync(
            "BuildContext",
            () => BuildContextAsync(storyState, intent, cancellationToken),
            stageResults,
            cancellationToken);

        if (contextResult is Result<PipelineContext>.Failure contextFailure)
        {
            stopwatch.Stop();
            var failedContext = PipelineContext.CreateMinimal(storyState, intent);
            return Result<PipelineResult>.Ok(
                PipelineResult.Failure(failedContext, contextFailure.Message, stageResults, stopwatch.Elapsed));
        }

        var context = ((Result<PipelineContext>.Success)contextResult).Value;

        // Étape 2-4: Génération avec retry
        NarrativeOutput? output = null;

        while (retryCount <= _config.MaxRetries)
        {
            // Étape 2: Préparer le prompt
            var promptResult = await ExecuteStageAsync(
                "PreparePrompt",
                () => PreparePromptAsync(context, cancellationToken),
                stageResults,
                cancellationToken);

            if (promptResult is Result<LlmRequest>.Failure promptFailure)
            {
                retryCount++;
                continue;
            }

            var request = ((Result<LlmRequest>.Success)promptResult).Value;

            // Étape 3: Générer via LLM
            var generateResult = await ExecuteStageAsync(
                "Generate",
                () => GenerateAsync(request, cancellationToken),
                stageResults,
                cancellationToken);

            if (generateResult is Result<LlmResponse>.Failure generateFailure)
            {
                retryCount++;
                continue;
            }

            var response = ((Result<LlmResponse>.Success)generateResult).Value;
            output = new NarrativeOutput(
                response.Content,
                metadata: new Dictionary<string, object>
                {
                    ["llmTokens"] = response.TotalTokens,
                    ["generationDuration"] = response.GenerationDuration,
                    ["isMock"] = _llmClient.IsMock
                });

            // Étape 4: Valider la sortie
            var validateResult = ValidateOutput(output, context);

            if (validateResult is Result<Unit>.Failure)
            {
                _logger.LogWarning(
                    "Validation failed on attempt {Attempt}, retrying...",
                    retryCount + 1);
                retryCount++;
                continue;
            }

            // Succès - sortir de la boucle
            stageResults.Add(PipelineStageResult.Success("Validate", TimeSpan.Zero));
            break;
        }

        stopwatch.Stop();

        if (output == null)
        {
            return Result<PipelineResult>.Ok(
                PipelineResult.Failure(
                    context,
                    $"Pipeline failed after {retryCount} retries",
                    stageResults,
                    stopwatch.Elapsed,
                    retryCount));
        }

        // Étape 5: Intégrer (pour Phase 3, c'est optionnel)
        stageResults.Add(PipelineStageResult.Success("Integrate", TimeSpan.Zero));

        _logger.LogInformation(
            "Orchestration cycle completed successfully in {Duration}ms with {Retries} retries",
            stopwatch.ElapsedMilliseconds,
            retryCount);

        return Result<PipelineResult>.Ok(
            PipelineResult.Success(context, output, stageResults, stopwatch.Elapsed, retryCount));
    }

    public async Task<Result<PipelineContext>> BuildContextAsync(
        StoryState storyState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Récupérer l'état canonique le plus récent (si disponible)
            CanonicalState? canonicalState = null;

            if (_memoryService != null)
            {
                var worldId = storyState.WorldState.WorldId;
                var canonicalResult = await _memoryService.GetCanonicalStateAsync(worldId, DateTime.UtcNow);

                if (canonicalResult is Result<CanonicalState>.Success success)
                {
                    canonicalState = success.Value;
                }
            }

            // Extraire les personnages actifs
            var activeCharacters = intent.TargetCharacterIds.Count > 0
                ? intent.TargetCharacterIds
                : storyState.Characters.Keys.ToList();

            var context = new PipelineContext(
                storyState,
                intent,
                currentMemorandum: null,
                canonicalState: canonicalState,
                activeCharacterIds: activeCharacters,
                currentLocationId: intent.TargetLocationId,
                metadata: new Dictionary<string, object>
                {
                    ["builtAt"] = DateTime.UtcNow,
                    ["hasMemory"] = canonicalState != null
                });

            return Result<PipelineContext>.Ok(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build context");
            return Result<PipelineContext>.Fail($"Context build failed: {ex.Message}");
        }
    }

    public Result<Unit> ValidateOutput(NarrativeOutput output, PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(context);

        // Validation structurelle de base
        if (string.IsNullOrWhiteSpace(output.NarrativeText))
        {
            return Result<Unit>.Fail("Output narrative text is empty");
        }

        if (output.NarrativeText.Length < 10)
        {
            return Result<Unit>.Fail("Output narrative text is too short");
        }

        // Pour Phase 3, on accepte tout texte non-vide
        // Les validations de cohérence seront ajoutées dans les étapes suivantes
        return Result<Unit>.Ok(Unit.Default());
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        return await _llmClient.IsHealthyAsync(cancellationToken);
    }

    /// <summary>
    /// Prépare la requête LLM à partir du contexte.
    /// </summary>
    private Task<Result<LlmRequest>> PreparePromptAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildUserPrompt(context);

        var request = new LlmRequest(
            systemPrompt,
            userPrompt,
            LlmParameters.Default,
            new Dictionary<string, object>
            {
                ["intentType"] = context.Intent.Type.ToString(),
                ["contextId"] = context.ContextId.Value
            });

        return Task.FromResult(Result<LlmRequest>.Ok(request));
    }

    /// <summary>
    /// Génère du contenu via le LLM.
    /// </summary>
    private async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        return await _llmClient.GenerateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Construit le prompt système.
    /// </summary>
    private string BuildSystemPrompt(PipelineContext context)
    {
        return $"""
            You are a narrative engine for the world "{context.StoryState.WorldState.WorldName}".
            Generate coherent narrative content that advances the story.
            Maintain consistency with established facts and character behaviors.
            """;
    }

    /// <summary>
    /// Construit le prompt utilisateur.
    /// </summary>
    private string BuildUserPrompt(PipelineContext context)
    {
        var intentDescription = context.Intent.Type switch
        {
            IntentType.ContinueNarrative => "Continue the narrative naturally.",
            IntentType.GenerateDialogue => "Generate a dialogue between the characters.",
            IntentType.DescribeScene => "Describe the current scene in detail.",
            IntentType.CreateTension => "Create dramatic tension in the narrative.",
            IntentType.ResolveConflict => "Resolve the current conflict.",
            IntentType.Summarize => "Summarize recent events.",
            _ => "Generate appropriate narrative content."
        };

        var promptParts = new List<string>
        {
            $"Intent: {intentDescription}"
        };

        if (context.Intent.Description != null)
        {
            promptParts.Add($"Details: {context.Intent.Description}");
        }

        if (context.ActiveCharacterIds.Count > 0)
        {
            promptParts.Add($"Active characters: {context.ActiveCharacterIds.Count}");
        }

        return string.Join("\n", promptParts);
    }

    /// <summary>
    /// Exécute une étape du pipeline avec timing.
    /// </summary>
    private async Task<Result<T>> ExecuteStageAsync<T>(
        string stageName,
        Func<Task<Result<T>>> stageAction,
        List<PipelineStageResult> results,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.StageTimeout);

            var result = await stageAction();
            stopwatch.Stop();

            if (result is Result<T>.Success success)
            {
                results.Add(PipelineStageResult.Success(stageName, stopwatch.Elapsed));
            }
            else if (result is Result<T>.Failure failure)
            {
                results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, failure.Message));
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, "Stage timed out"));
            return Result<T>.Fail($"Stage {stageName} timed out");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, ex.Message));
            return Result<T>.Fail($"Stage {stageName} failed: {ex.Message}");
        }
    }

    private static ILogger<OrchestrationService> CreateNullLogger()
    {
        return new NullLogger<OrchestrationService>();
    }

    /// <summary>
    /// Logger null pour les cas sans DI.
    /// </summary>
    private sealed class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
