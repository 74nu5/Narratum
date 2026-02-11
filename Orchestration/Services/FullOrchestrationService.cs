using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.State;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Logging;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Validation;
using Narratum.Orchestration.Prompts;

namespace Narratum.Orchestration.Services;

/// <summary>
/// Configuration complète de l'orchestration avec tous les composants.
/// </summary>
public sealed record FullOrchestrationConfig
{
    /// <summary>
    /// Nombre maximum de tentatives en cas d'échec de validation.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Timeout pour chaque étape du pipeline.
    /// </summary>
    public TimeSpan StageTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout global pour l'exécution complète.
    /// </summary>
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Active le logging détaillé.
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = true;

    /// <summary>
    /// Active la validation structurelle.
    /// </summary>
    public bool EnableStructureValidation { get; init; } = true;

    /// <summary>
    /// Active la validation de cohérence.
    /// </summary>
    public bool EnableCoherenceValidation { get; init; } = true;

    /// <summary>
    /// Délai entre les retries.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static FullOrchestrationConfig Default => new();

    /// <summary>
    /// Configuration pour les tests (rapide).
    /// </summary>
    public static FullOrchestrationConfig ForTesting => new()
    {
        MaxRetries = 1,
        StageTimeout = TimeSpan.FromSeconds(5),
        GlobalTimeout = TimeSpan.FromSeconds(30),
        EnableDetailedLogging = true,
        RetryDelay = TimeSpan.FromMilliseconds(10)
    };

    /// <summary>
    /// Configuration pour les tests de performance.
    /// </summary>
    public static FullOrchestrationConfig ForPerformance => new()
    {
        MaxRetries = 0,
        StageTimeout = TimeSpan.FromSeconds(2),
        GlobalTimeout = TimeSpan.FromSeconds(5),
        EnableDetailedLogging = false,
        RetryDelay = TimeSpan.Zero
    };
}

/// <summary>
/// Service d'orchestration complet intégrant tous les composants Phase 3.
///
/// Ce service coordonne :
/// - Pipeline de génération (ContextBuilder → PromptBuilder → AgentExecutor → Validator → Integrator)
/// - Validation structurelle et de cohérence
/// - Retry avec politiques configurables
/// - Logging exhaustif avec PipelineLogger
/// - Audit trail des décisions
/// - Métriques de performance
///
/// Principe fondamental : le système fonctionne même avec un LLM "stupide".
/// </summary>
public sealed class FullOrchestrationService
{
    private readonly ILlmClient _llmClient;
    private readonly IMemoryService? _memoryService;
    private readonly IPipelineLogger _pipelineLogger;
    private readonly AuditTrail _auditTrail;
    private readonly MetricsCollector _metricsCollector;
    private readonly IStructureValidator _structureValidator;
    private readonly ICoherenceValidatorAdapter? _coherenceValidator;
    private readonly PromptRegistry _promptRegistry;
    private readonly FullOrchestrationConfig _config;
    private readonly ILogger<FullOrchestrationService>? _logger;

    public FullOrchestrationConfig Config => _config;

    public FullOrchestrationService(
        ILlmClient llmClient,
        FullOrchestrationConfig? config = null,
        IPipelineLogger? pipelineLogger = null,
        AuditTrail? auditTrail = null,
        MetricsCollector? metricsCollector = null,
        IStructureValidator? structureValidator = null,
        ICoherenceValidatorAdapter? coherenceValidator = null,
        PromptRegistry? promptRegistry = null,
        IMemoryService? memoryService = null,
        ILogger<FullOrchestrationService>? logger = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? FullOrchestrationConfig.Default;
        _pipelineLogger = pipelineLogger ?? NullPipelineLogger.Instance;
        _auditTrail = auditTrail ?? new AuditTrail();
        _metricsCollector = metricsCollector ?? new MetricsCollector();
        _structureValidator = structureValidator ?? new StructureValidator();
        _coherenceValidator = coherenceValidator;
        _promptRegistry = promptRegistry ?? PromptRegistry.CreateWithDefaults();
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <summary>
    /// Exécute un cycle complet de génération narrative avec tous les composants intégrés.
    /// </summary>
    public async Task<Result<FullPipelineResult>> ExecuteCycleAsync(
        StoryState storyState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storyState);
        ArgumentNullException.ThrowIfNull(intent);

        var pipelineId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var stageResults = new List<PipelineStageResult>();
        var retryCount = 0;

        // Démarrer le tracking
        _pipelineLogger.LogPipelineStart(pipelineId, intent);
        _metricsCollector.StartPipeline(pipelineId);
        _auditTrail.RecordDecision(pipelineId, "PipelineStart", $"Intent: {intent.Type}");

        try
        {
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            globalCts.CancelAfter(_config.GlobalTimeout);

            // Étape 1: Construire le contexte
            var contextResult = await ExecuteStageAsync(
                pipelineId,
                "ContextBuilder",
                () => BuildContextAsync(storyState, intent, pipelineId, globalCts.Token),
                stageResults,
                globalCts.Token);

            if (contextResult is Result<NarrativeContext>.Failure contextFailure)
            {
                return CreateFailureResult(pipelineId, storyState, intent, contextFailure.Message,
                    stageResults, stopwatch.Elapsed, retryCount);
            }

            var context = ((Result<NarrativeContext>.Success)contextResult).Value;

            // Étape 2: Construire les prompts
            var promptsResult = await ExecuteStageAsync(
                pipelineId,
                "PromptBuilder",
                () => BuildPromptsAsync(context, intent, globalCts.Token),
                stageResults,
                globalCts.Token);

            if (promptsResult is Result<PromptSet>.Failure promptFailure)
            {
                return CreateFailureResult(pipelineId, storyState, intent, promptFailure.Message,
                    stageResults, stopwatch.Elapsed, retryCount);
            }

            var prompts = ((Result<PromptSet>.Success)promptsResult).Value;

            // Boucle de génération avec retry
            RawOutput? rawOutput = null;
            ValidationResult? lastValidation = null;

            while (retryCount <= _config.MaxRetries)
            {
                // Étape 3: Exécuter les agents
                var generateResult = await ExecuteStageAsync(
                    pipelineId,
                    $"AgentExecutor{(retryCount > 0 ? $"_Retry{retryCount}" : "")}",
                    () => ExecuteAgentsAsync(prompts, context, pipelineId, globalCts.Token),
                    stageResults,
                    globalCts.Token);

                if (generateResult is Result<RawOutput>.Failure generateFailure)
                {
                    _pipelineLogger.LogRetry(pipelineId, retryCount + 1,
                        new[] { generateFailure.Message });
                    retryCount++;
                    if (retryCount <= _config.MaxRetries)
                    {
                        await Task.Delay(_config.RetryDelay, globalCts.Token);
                    }
                    continue;
                }

                rawOutput = ((Result<RawOutput>.Success)generateResult).Value;

                // Étape 4: Valider la sortie
                var validationResult = await ExecuteStageAsync(
                    pipelineId,
                    $"Validation{(retryCount > 0 ? $"_Retry{retryCount}" : "")}",
                    () => ValidateOutputAsync(rawOutput, context, pipelineId, globalCts.Token),
                    stageResults,
                    globalCts.Token);

                if (validationResult is Result<ValidationResult>.Failure validationFailure)
                {
                    _pipelineLogger.LogRetry(pipelineId, retryCount + 1,
                        new[] { validationFailure.Message });
                    retryCount++;
                    if (retryCount <= _config.MaxRetries)
                    {
                        await Task.Delay(_config.RetryDelay, globalCts.Token);
                    }
                    continue;
                }

                lastValidation = ((Result<ValidationResult>.Success)validationResult).Value;

                if (lastValidation.IsValid)
                {
                    _auditTrail.RecordDecision(pipelineId, "ValidationPassed",
                        $"Output validated after {retryCount} retries");
                    break;
                }

                // Validation échouée - retry
                _pipelineLogger.LogRetry(pipelineId, retryCount + 1, lastValidation.ErrorMessages);
                _auditTrail.RecordValidationFailure(pipelineId, "OutputValidator",
                    lastValidation.ErrorMessages);
                _metricsCollector.RecordRetry(pipelineId, retryCount + 1);

                retryCount++;
                if (retryCount <= _config.MaxRetries)
                {
                    await Task.Delay(_config.RetryDelay, globalCts.Token);
                }
            }

            stopwatch.Stop();

            if (rawOutput == null || (lastValidation != null && !lastValidation.IsValid))
            {
                var errorMsg = lastValidation != null
                    ? $"Validation failed after {retryCount} retries: {string.Join("; ", lastValidation.ErrorMessages.Take(3))}"
                    : $"Generation failed after {retryCount} retries";

                return CreateFailureResult(pipelineId, storyState, intent, errorMsg,
                    stageResults, stopwatch.Elapsed, retryCount);
            }

            // Étape 5: Intégrer le résultat
            var output = CreateNarrativeOutput(rawOutput, context);
            stageResults.Add(PipelineStageResult.Success("Integrate", TimeSpan.Zero));

            // Finaliser les métriques et logs
            _pipelineLogger.LogPipelineComplete(pipelineId, stopwatch.Elapsed);
            var metricsSummary = _metricsCollector.EndPipeline(pipelineId, success: true);

            _auditTrail.RecordDecision(pipelineId, "PipelineComplete",
                $"Success in {stopwatch.ElapsedMilliseconds}ms with {retryCount} retries");

            return Result<FullPipelineResult>.Ok(
                FullPipelineResult.Success(
                    pipelineId,
                    PipelineContext.CreateMinimal(storyState, intent),
                    output,
                    stageResults,
                    stopwatch.Elapsed,
                    retryCount,
                    metricsSummary));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _pipelineLogger.LogPipelineError(pipelineId,
                new TimeoutException("Pipeline execution timed out"));
            _metricsCollector.EndPipeline(pipelineId, success: false);

            return CreateFailureResult(pipelineId, storyState, intent, "Pipeline timed out",
                stageResults, stopwatch.Elapsed, retryCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _pipelineLogger.LogPipelineError(pipelineId, ex);
            _metricsCollector.EndPipeline(pipelineId, success: false);
            _auditTrail.Record(AuditEntry.CriticalError(pipelineId, "Pipeline", ex));

            return CreateFailureResult(pipelineId, storyState, intent, ex.Message,
                stageResults, stopwatch.Elapsed, retryCount);
        }
    }

    /// <summary>
    /// Construit le contexte narratif enrichi.
    /// </summary>
    private async Task<Result<NarrativeContext>> BuildContextAsync(
        StoryState storyState,
        NarrativeIntent intent,
        Guid pipelineId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Récupérer l'état canonique si disponible
            CanonicalState? canonicalState = null;
            if (_memoryService != null)
            {
                var worldId = storyState.WorldState.WorldId;
                var canonicalResult = await _memoryService.GetCanonicalStateAsync(
                    worldId, DateTime.UtcNow);

                if (canonicalResult is Result<CanonicalState>.Success success)
                {
                    canonicalState = success.Value;
                }
            }

            // Construire les personnages actifs
            var activeCharacters = new List<CharacterContext>();
            var characterIds = intent.TargetCharacterIds.Count > 0
                ? intent.TargetCharacterIds
                : storyState.Characters.Keys.ToList();

            foreach (var characterId in characterIds)
            {
                if (storyState.Characters.TryGetValue(characterId, out var charState))
                {
                    activeCharacters.Add(CharacterContext.FromCharacterState(charState));
                }
            }

            var context = new NarrativeContext(
                storyState,
                activeCharacters: activeCharacters,
                canonicalState: canonicalState);

            _auditTrail.RecordDecision(pipelineId, "ContextBuilt",
                $"Built context with {activeCharacters.Count} characters");

            return Result<NarrativeContext>.Ok(context);
        }
        catch (Exception ex)
        {
            return Result<NarrativeContext>.Fail($"Context build failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Construit les prompts pour les agents.
    /// </summary>
    private Task<Result<PromptSet>> BuildPromptsAsync(
        NarrativeContext context,
        NarrativeIntent intent,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompts = new List<AgentPrompt>();

            // Sélectionner le template approprié
            var template = _promptRegistry.GetTemplate(AgentType.Narrator, intent.Type);

            if (template != null)
            {
                var systemPrompt = template.BuildSystemPrompt(context);
                var userPrompt = template.BuildUserPrompt(context, intent);
                var variables = template.GetVariables(context);

                prompts.Add(new AgentPrompt(
                    template.TargetAgent,
                    systemPrompt,
                    userPrompt,
                    variables));
            }
            else
            {
                // Prompt par défaut si pas de template
                prompts.Add(AgentPrompt.Create(
                    AgentType.Narrator,
                    $"You are a narrative engine for the world \"{context.State.WorldState.WorldName}\".",
                    $"Intent: {intent.Type}. {intent.Description ?? ""}"));
            }

            return Task.FromResult(Result<PromptSet>.Ok(
                new PromptSet(prompts, ExecutionOrder.Sequential)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<PromptSet>.Fail($"Prompt build failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Exécute les agents et génère le contenu.
    /// </summary>
    private async Task<Result<RawOutput>> ExecuteAgentsAsync(
        PromptSet prompts,
        NarrativeContext context,
        Guid pipelineId,
        CancellationToken cancellationToken)
    {
        var responses = new List<AgentResponse>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var prompt in prompts.Prompts)
        {
            _pipelineLogger.LogAgentCall(pipelineId, prompt.TargetAgent, prompt.UserPrompt);
            _metricsCollector.StartStage(pipelineId, $"Agent_{prompt.TargetAgent}");

            var agentStopwatch = Stopwatch.StartNew();

            // Appeler le LLM avec le type d'agent pour le routing de modèle
            var metadata = new Dictionary<string, object>
            {
                ["llm.agentType"] = prompt.TargetAgent
            };
            var request = new LlmRequest(
                prompt.SystemPrompt,
                prompt.UserPrompt,
                LlmParameters.Default,
                metadata);

            var llmResult = await _llmClient.GenerateAsync(request, cancellationToken);
            agentStopwatch.Stop();

            if (llmResult is Result<LlmResponse>.Failure llmFailure)
            {
                var failResponse = AgentResponse.CreateFailure(
                    prompt.TargetAgent,
                    llmFailure.Message,
                    agentStopwatch.Elapsed);

                _pipelineLogger.LogAgentResponse(pipelineId, prompt.TargetAgent, failResponse);
                _metricsCollector.RecordAgentCall(pipelineId, prompt.TargetAgent,
                    agentStopwatch.Elapsed, success: false);

                if (prompt.Priority == PromptPriority.Required)
                {
                    return Result<RawOutput>.Fail($"Agent {prompt.TargetAgent} failed: {llmFailure.Message}");
                }

                responses.Add(failResponse);
                continue;
            }

            var llmResponse = ((Result<LlmResponse>.Success)llmResult).Value;
            var response = AgentResponse.CreateSuccess(
                prompt.TargetAgent,
                llmResponse.Content,
                agentStopwatch.Elapsed);

            _pipelineLogger.LogAgentResponse(pipelineId, prompt.TargetAgent, response);
            _metricsCollector.RecordAgentCall(pipelineId, prompt.TargetAgent,
                agentStopwatch.Elapsed, success: true);
            _metricsCollector.EndStage(pipelineId, $"Agent_{prompt.TargetAgent}");

            _auditTrail.RecordAgentAction(pipelineId, prompt.TargetAgent, "Generate",
                $"Generated {llmResponse.Content.Length} chars");

            responses.Add(response);
        }

        stopwatch.Stop();

        if (responses.Count == 0 || responses.All(r => !r.Success))
        {
            return Result<RawOutput>.Fail("All agents failed to generate content");
        }

        return Result<RawOutput>.Ok(RawOutput.Create(responses, stopwatch.Elapsed));
    }

    /// <summary>
    /// Valide la sortie des agents.
    /// </summary>
    private async Task<Result<ValidationResult>> ValidateOutputAsync(
        RawOutput output,
        NarrativeContext context,
        Guid pipelineId,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validation structurelle
        if (_config.EnableStructureValidation)
        {
            var structureResult = _structureValidator.Validate(output);

            if (!structureResult.IsValid)
            {
                errors.AddRange(structureResult.Errors.Select(e =>
                    new ValidationError(e.Message, e.Severity)));
            }
            warnings.AddRange(structureResult.Warnings.Select(w =>
                new ValidationWarning(w.Message, w.Context)));
        }

        // Validation de cohérence
        if (_config.EnableCoherenceValidation && _coherenceValidator != null)
        {
            var coherenceResult = await _coherenceValidator.ValidateAsync(
                output, context, cancellationToken);

            if (!coherenceResult.IsCoherent)
            {
                errors.AddRange(coherenceResult.Issues
                    .Where(i => i.Severity == CoherenceIssueSeverity.Error)
                    .Select(i => new ValidationError(i.Description, ErrorSeverity.Major)));
            }

            warnings.AddRange(coherenceResult.Issues
                .Where(i => i.Severity == CoherenceIssueSeverity.Warning)
                .Select(i => new ValidationWarning(i.Description)));
        }

        var result = errors.Count == 0
            ? new ValidationResult(true, errors, warnings, new Dictionary<string, object>())
            : new ValidationResult(false, errors, warnings, new Dictionary<string, object>());

        _pipelineLogger.LogValidation(pipelineId, result);

        return Result<ValidationResult>.Ok(result);
    }

    /// <summary>
    /// Crée la sortie narrative à partir des réponses des agents.
    /// </summary>
    private NarrativeOutput CreateNarrativeOutput(RawOutput rawOutput, NarrativeContext context)
    {
        // Combiner les réponses des agents
        var narrativeText = rawOutput.GetContent(AgentType.Narrator)
            ?? rawOutput.Responses.Values.FirstOrDefault(r => r.Success)?.Content
            ?? string.Empty;

        return new NarrativeOutput(
            narrativeText,
            metadata: new Dictionary<string, object>
            {
                ["agentCount"] = rawOutput.Responses.Count,
                ["totalDuration"] = rawOutput.TotalDuration.TotalMilliseconds,
                ["isMock"] = _llmClient.IsMock
            });
    }

    /// <summary>
    /// Exécute une étape avec logging et métriques.
    /// </summary>
    private async Task<Result<T>> ExecuteStageAsync<T>(
        Guid pipelineId,
        string stageName,
        Func<Task<Result<T>>> stageAction,
        List<PipelineStageResult> results,
        CancellationToken cancellationToken)
    {
        _pipelineLogger.LogStageStart(pipelineId, stageName);
        _metricsCollector.StartStage(pipelineId, stageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.StageTimeout);

            var result = await stageAction();
            stopwatch.Stop();

            if (result is Result<T>.Success)
            {
                results.Add(PipelineStageResult.Success(stageName, stopwatch.Elapsed));
                _pipelineLogger.LogStageComplete(pipelineId, stageName, stopwatch.Elapsed);
            }
            else if (result is Result<T>.Failure failure)
            {
                results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, failure.Message));
                _pipelineLogger.LogStageFailure(pipelineId, stageName, failure.Message);
            }

            _metricsCollector.EndStage(pipelineId, stageName);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var error = "Stage timed out";
            results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, error));
            _pipelineLogger.LogStageFailure(pipelineId, stageName, error);
            return Result<T>.Fail($"Stage {stageName} timed out");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            results.Add(PipelineStageResult.Failure(stageName, stopwatch.Elapsed, ex.Message));
            _pipelineLogger.LogStageFailure(pipelineId, stageName, ex.Message);
            return Result<T>.Fail($"Stage {stageName} failed: {ex.Message}");
        }
    }

    private Result<FullPipelineResult> CreateFailureResult(
        Guid pipelineId,
        StoryState storyState,
        NarrativeIntent intent,
        string error,
        List<PipelineStageResult> stageResults,
        TimeSpan duration,
        int retryCount)
    {
        _auditTrail.RecordDecision(pipelineId, "PipelineFailed", error);

        var metricsSummary = _metricsCollector.EndPipeline(pipelineId, success: false);

        return Result<FullPipelineResult>.Ok(
            FullPipelineResult.Failure(
                pipelineId,
                PipelineContext.CreateMinimal(storyState, intent),
                error,
                stageResults,
                duration,
                retryCount,
                metricsSummary));
    }

    /// <summary>
    /// Récupère l'historique du pipeline.
    /// </summary>
    public IReadOnlyList<PipelineEvent> GetPipelineHistory(Guid pipelineId)
        => _pipelineLogger.GetPipelineHistory(pipelineId);

    /// <summary>
    /// Récupère l'audit trail.
    /// </summary>
    public AuditReport GetAuditReport(Guid pipelineId)
        => _auditTrail.GenerateReport(pipelineId);

    /// <summary>
    /// Récupère les métriques.
    /// </summary>
    public MetricsReport GetMetricsReport()
        => _metricsCollector.GenerateReport();

    /// <summary>
    /// Vérifie si le service est prêt.
    /// </summary>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
        => await _llmClient.IsHealthyAsync(cancellationToken);
}

/// <summary>
/// Résultat complet du pipeline avec métriques.
/// </summary>
public sealed record FullPipelineResult
{
    public Guid PipelineId { get; }
    public PipelineContext InputContext { get; }
    public NarrativeOutput? Output { get; }
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<PipelineStageResult> StageResults { get; }
    public TimeSpan TotalDuration { get; }
    public int RetryCount { get; }
    public PipelineMetricsSummary? Metrics { get; }
    public DateTime CompletedAt { get; }

    private FullPipelineResult(
        Guid pipelineId,
        PipelineContext inputContext,
        NarrativeOutput? output,
        bool isSuccess,
        string? errorMessage,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan totalDuration,
        int retryCount,
        PipelineMetricsSummary? metrics)
    {
        PipelineId = pipelineId;
        InputContext = inputContext;
        Output = output;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StageResults = stageResults.ToList();
        TotalDuration = totalDuration;
        RetryCount = retryCount;
        Metrics = metrics;
        CompletedAt = DateTime.UtcNow;
    }

    public static FullPipelineResult Success(
        Guid pipelineId,
        PipelineContext context,
        NarrativeOutput output,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan duration,
        int retryCount,
        PipelineMetricsSummary? metrics = null)
    {
        return new FullPipelineResult(
            pipelineId,
            context,
            output,
            isSuccess: true,
            errorMessage: null,
            stageResults,
            duration,
            retryCount,
            metrics);
    }

    public static FullPipelineResult Failure(
        Guid pipelineId,
        PipelineContext context,
        string errorMessage,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan duration,
        int retryCount,
        PipelineMetricsSummary? metrics = null)
    {
        return new FullPipelineResult(
            pipelineId,
            context,
            output: null,
            isSuccess: false,
            errorMessage,
            stageResults,
            duration,
            retryCount,
            metrics);
    }
}
