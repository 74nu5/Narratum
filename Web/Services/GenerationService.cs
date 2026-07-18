using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Services;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Prompts;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Configuration;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Service for Blazor UI to interact with narrative generation.
/// Wraps FullOrchestrationService and IStoryRepository (hexagonal architecture).
/// </summary>
public class GenerationService : IGenerationService
{
    private readonly FullOrchestrationService _orchestrator;
    private readonly IStoryRepository _storyRepository;
    private readonly ModelSelectionService _modelSelector;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<GenerationService> _logger;
    private readonly PromptOptimizationService _promptOptimizer = new();
    private readonly AgentTemperatureConfig _temperatures = AgentTemperatureConfig.Default;

    /// <summary>
    /// Forces French output regardless of the (partly English) prompt scaffolding.
    /// Appended to every prompt sent to the model so stories are exclusively in French.
    /// </summary>
    private const string FrenchOnly =
        "IMPORTANT : rédige ta réponse EXCLUSIVEMENT en français, quelle que soit la langue des instructions ou du contexte ci-dessus.";

    public GenerationService(
        FullOrchestrationService orchestrator,
        IStoryRepository storyRepository,
        ModelSelectionService modelSelector,
        ILlmClient llmClient,
        ILogger<GenerationService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _storyRepository = storyRepository ?? throw new ArgumentNullException(nameof(storyRepository));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new story slot in the database with initial state (page 0).
    /// </summary>
    public async Task<Result<string>> CreateStoryAsync(
        string slotName,
        StoryCreationRequest request,
        CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SlotName"] = slotName,
            ["OperationId"] = Guid.NewGuid()
        });

        _logger.LogInformation("Starting story creation for slot {SlotName}", slotName);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(slotName))
        {
            _logger.LogWarning("Story creation failed: empty slot name");
            return Result<string>.Fail("Le nom du slot ne peut pas être vide");
        }

        if (request == null)
        {
            _logger.LogWarning("Story creation failed: null request");
            return Result<string>.Fail("La requête ne peut pas être nulle");
        }

        if (string.IsNullOrWhiteSpace(request.WorldName))
        {
            _logger.LogWarning("Story creation failed: empty world name");
            return Result<string>.Fail("Le nom du monde ne peut pas être vide");
        }

        if (!request.Characters.Any())
        {
            _logger.LogWarning("Story creation failed: no characters");
            return Result<string>.Fail("Au moins un personnage est requis");
        }

        // Create initial state with characters
        var worldId = Id.New();
        var characterStates = request.Characters.Select(c =>
        {
            var id = Id.New();
            return new CharacterState(id, c.Name);
        }).ToArray();

        var storyState = StoryState.Create(worldId, request.WorldName)
            .WithCharacters(characterStates);

        var initText = $"Histoire créée: {request.WorldName}\nGenre: {request.GenreStyle}" +
            (string.IsNullOrWhiteSpace(request.WorldDescription) ? "" : $"\nMonde: {request.WorldDescription}") +
            $"\nPersonnages: {string.Join(", ", request.Characters.Select(c => c.Name))}";

        var displayDescription = string.IsNullOrWhiteSpace(request.NarrativeStyle)
            ? request.GenreStyle
            : $"{request.GenreStyle} — {request.NarrativeStyle}";

        // Use repository (hexagonal architecture). The chosen model is stored on page 0
        // so the generation view can default to it.
        var result = await _storyRepository.CreateStoryAsync(
            slotName,
            request.WorldName,
            request.GenreStyle,
            displayDescription,
            storyState,
            initText,
            _modelSelector.NormalizeOrDefault(request.Model),
            ct);

        return result.Match<Result<string>>(
            onSuccess: metadata =>
            {
                _logger.LogInformation(
                    "Story created successfully. SlotName: {SlotName}, WorldName: {WorldName}, Characters: {CharacterCount}",
                    metadata.SlotName, request.WorldName, request.Characters.Count());
                return Result<string>.Ok(metadata.SlotName);
            },
            onFailure: error =>
            {
                _logger.LogError("Story creation failed: {Error}", error);
                return Result<string>.Fail(error);
            });
    }

    /// <summary>
    /// Generates next page using FullOrchestrationService.
    /// </summary>
    public async Task<Result<PageInfo>> GenerateNextPageAsync(
        string slotName,
        string intentDescription,
        CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SlotName"] = slotName,
            ["OperationId"] = Guid.NewGuid()
        });

        _logger.LogInformation("Starting page generation for slot {SlotName}", slotName);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(slotName))
        {
            _logger.LogWarning("Page generation failed: empty slot name");
            return Result<PageInfo>.Fail("Le nom du slot ne peut pas être vide");
        }

        if (string.IsNullOrWhiteSpace(intentDescription))
        {
            _logger.LogWarning("Page generation failed: empty intent description");
            return Result<PageInfo>.Fail("La description de l'intention ne peut pas être vide");
        }

        if (intentDescription.Length > 1000)
        {
            _logger.LogWarning("Page generation failed: intent too long ({Length} chars)", intentDescription.Length);
            return Result<PageInfo>.Fail("La description de l'intention est trop longue (max 1000 caractères)");
        }

        // Load latest page using repository
        var loadResult = await _storyRepository.LoadLatestPageAsync(slotName, ct);

        if (loadResult is Result<Core.PageSnapshot>.Failure loadFailure)
        {
            _logger.LogError("Failed to load latest page for slot {SlotName}: {Error}", slotName, loadFailure.Message);
            return Result<PageInfo>.Fail(loadFailure.Message);
        }

        var latestPage = ((Result<Core.PageSnapshot>.Success)loadResult).Value;
        var storyState = latestPage.State;

        _logger.LogDebug("Loaded page {PageIndex} for slot {SlotName}", latestPage.PageIndex, slotName);

        // Create intent
        var intent = NarrativeIntent.Continue(intentDescription);

        // Execute pipeline
        var startTime = DateTime.UtcNow;
        var result = await _orchestrator.ExecuteCycleAsync(storyState, intent, ct);

        return await result.MatchAsync<Result<PageInfo>>(
            onSuccess: async pipelineResult =>
            {
                if (!pipelineResult.IsSuccess || pipelineResult.Output == null)
                {
                    _logger.LogError("Pipeline execution failed: {ErrorMessage}", pipelineResult.ErrorMessage);
                    return Result<PageInfo>.Fail(pipelineResult.ErrorMessage ?? "Génération échouée");
                }

                var generationTime = (DateTime.UtcNow - startTime).TotalSeconds;

                // Save new page using repository
                var saveResult = await _storyRepository.SavePageAsync(
                    slotName,
                    latestPage.PageIndex + 1,
                    pipelineResult.Output.NarrativeText,
                    intentDescription,
                    _modelSelector.CurrentNarratorModel,
                    storyState,
                    ct);

                return saveResult.Match<Result<PageInfo>>(
                    onSuccess: savedPage =>
                    {
                        _logger.LogInformation(
                            "Page generated successfully. SlotName: {SlotName}, PageIndex: {PageIndex}, GenerationTime: {GenerationTime:F2}s, Model: {Model}",
                            slotName, savedPage.PageIndex, generationTime, _modelSelector.CurrentNarratorModel);
                        return Result<PageInfo>.Ok(new PageInfo(
                            savedPage.PageIndex,
                            savedPage.NarrativeText,
                            savedPage.GeneratedAt));
                    },
                    onFailure: error =>
                    {
                        _logger.LogError("Failed to save generated page: {Error}", error);
                        return Result<PageInfo>.Fail(error);
                    });
            },
            onFailure: error =>
            {
                _logger.LogError("Pipeline execution failed: {Error}", error);
                return Task.FromResult(Result<PageInfo>.Fail(error));
            });
    }

    /// <summary>
    /// Streams the next page's narrative fragment-by-fragment, then persists it.
    /// This path calls the Narrator LLM directly (bypassing the batch orchestration
    /// pipeline) so text can be surfaced live; the completed page is saved at the end.
    /// </summary>
    public async IAsyncEnumerable<string> GenerateNextPageStreamingAsync(
        string slotName,
        string intentDescription,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SlotName"] = slotName,
            ["OperationId"] = Guid.NewGuid()
        });

        // Validate inputs (thrown errors are surfaced to the component's try/catch)
        if (string.IsNullOrWhiteSpace(slotName))
            throw new ArgumentException("Le nom du slot ne peut pas être vide", nameof(slotName));
        if (string.IsNullOrWhiteSpace(intentDescription))
            throw new ArgumentException("La description de l'intention ne peut pas être vide", nameof(intentDescription));
        if (intentDescription.Length > 1000)
            throw new ArgumentException("La description de l'intention est trop longue (max 1000 caractères)", nameof(intentDescription));

        if (_llmClient is not IStreamingLlmClient streamingClient)
            throw new InvalidOperationException("Le client LLM ne supporte pas le streaming");

        var chosenModel = _modelSelector.NormalizeOrDefault(model);
        _logger.LogInformation("Starting multi-agent streaming generation for slot {SlotName} with model {Model}", slotName, chosenModel);

        // Load the latest page to continue from
        var loadResult = await _storyRepository.LoadLatestPageAsync(slotName, ct);
        if (loadResult is Result<Core.PageSnapshot>.Failure loadFailure)
        {
            _logger.LogError("Failed to load latest page for slot {SlotName}: {Error}", slotName, loadFailure.Message);
            throw new InvalidOperationException(loadFailure.Message);
        }

        var latestPage = ((Result<Core.PageSnapshot>.Success)loadResult).Value;
        var storyState = latestPage.State;
        var intent = NarrativeIntent.Continue(intentDescription);

        // Agent traces are captured for Expert mode; the user only ever sees the Narrator's prose.
        var traces = new List<AgentTraceInfo>();

        // 1. Summary agent — condenses the story so far to ground the Narrator (runs before streaming).
        // Grounded on the actual page text (source of truth), not the event history.
        var storySoFar = await _storyRepository.GetStoryTextAsync(slotName, ct);
        var summaryPrompt = string.IsNullOrWhiteSpace(storySoFar)
            ? "Il n'y a pas encore d'histoire à résumer ; réponds simplement « (histoire à peine commencée) »."
            : $"Résume en 2 à 3 phrases, de façon strictement factuelle et sans rien inventer, l'histoire suivante :\n\n{storySoFar}";
        var summary = await RunAgentAsync(
            AgentType.Summary, "Résumé", "Condense l'histoire jusqu'ici",
            summaryPrompt, chosenModel, ct);
        traces.Add(summary);

        // 2. Narrator agent — the user-visible prose, streamed live.
        var narratorPrompt = _promptOptimizer.BuildOptimizedNarratorPrompt(
                storyState, intent, previousNarrative: latestPage.NarrativeText)
            + $"\n\nRÉSUMÉ DU CONTEXTE (référence, ne pas recopier) :\n{summary.Output}"
            + "\n\n" + FrenchOnly;

        var narratorRequest = new LlmRequest(
            "Tu es un maître conteur qui écrit une narration immersive et vivante. " + FrenchOnly,
            narratorPrompt,
            LlmParameters.Default with { Temperature = _temperatures.GetTemperature(AgentType.Narrator) },
            new Dictionary<string, object>
            {
                ["llm.agentType"] = AgentType.Narrator,
                ["llm.model"] = chosenModel
            });

        var narratorTimer = Stopwatch.StartNew();
        var builder = new StringBuilder();
        await foreach (var chunk in streamingClient.GenerateStreamingAsync(narratorRequest, ct).WithCancellation(ct))
        {
            builder.Append(chunk);
            yield return chunk;
        }
        narratorTimer.Stop();

        var fullText = builder.ToString();
        if (string.IsNullOrWhiteSpace(fullText))
            throw new InvalidOperationException("La génération n'a produit aucun texte");

        traces.Add(new AgentTraceInfo("Narrateur", "Écrit la prose (texte affiché)", fullText, narratorTimer.Elapsed.TotalMilliseconds));

        // Save the page NOW. The narrative is the only user-visible output, and the user
        // just watched it stream in — the post-generation agents below must never cost them
        // that page (a slow/failing agent used to abort before the save ran).
        var newPageIndex = latestPage.PageIndex + 1;
        var saveResult = await _storyRepository.SavePageAsync(
            slotName, newPageIndex, fullText, intentDescription, chosenModel, storyState, ct);

        if (saveResult is Result<Core.PageSnapshot>.Failure saveFailure)
        {
            _logger.LogError("Failed to save streamed page: {Error}", saveFailure.Message);
            throw new InvalidOperationException(saveFailure.Message);
        }

        // 3. Consistency agent — checks the generated text against established facts (Expert-only).
        var establishedFacts = storyState.Characters.Values
            .SelectMany(c => c.KnownFacts)
            .Distinct()
            .ToList();
        traces.Add(await RunAgentAsync(
            AgentType.Consistency, "Cohérence", "Vérifie le texte contre les faits établis",
            _promptOptimizer.BuildOptimizedConsistencyPrompt(storyState, fullText, establishedFacts), chosenModel, ct));

        // 4. Character agent — maintains an evolving roster (personality, arc, key facts per
        // character), re-derived from the full story so it reflects their evolution (Expert-only).
        var knownNames = storyState.Characters.Values.Select(c => c.Name).ToList();
        var fullStory = string.IsNullOrWhiteSpace(storySoFar) ? fullText : storySoFar + "\n\n" + fullText;
        traces.Add(await RunAgentAsync(
            AgentType.Character, "Personnages", "Fiches : personnalité, évolution, faits marquants",
            BuildCharacterRosterPrompt(fullStory, knownNames), chosenModel, ct));

        // Persist the Expert traces — best effort: the page is already saved.
        try
        {
            var expertJson = JsonSerializer.Serialize(traces);
            await _storyRepository.SavePageExpertDataAsync(slotName, newPageIndex, expertJson, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist expert data for page {PageIndex}", newPageIndex);
        }

        _logger.LogInformation(
            "Multi-agent page saved. SlotName: {SlotName}, PageIndex: {PageIndex}, Agents: {AgentCount}",
            slotName, newPageIndex, traces.Count);
    }

    /// <summary>
    /// Runs a single non-streaming agent and captures its output as a trace.
    /// Agent failures are recorded in the trace rather than thrown, so one weak agent
    /// never aborts the page.
    /// </summary>
    private async Task<AgentTraceInfo> RunAgentAsync(
        AgentType agent, string displayName, string role, string userPrompt, string model, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var request = new LlmRequest(
            AgentSystemPrompt(agent),
            userPrompt + "\n\n" + FrenchOnly,
            LlmParameters.Default with { Temperature = _temperatures.GetTemperature(agent) },
            new Dictionary<string, object>
            {
                ["llm.agentType"] = agent,
                ["llm.model"] = model
            });

        string output;
        try
        {
            var result = await _llmClient.GenerateAsync(request, ct);
            output = result switch
            {
                Result<LlmResponse>.Success s => s.Value.Content,
                Result<LlmResponse>.Failure f => $"(agent indisponible : {f.Message})",
                _ => "(aucune sortie)"
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // genuine user cancellation
        }
        catch (Exception ex)
        {
            // A weak/slow agent (timeout, retries exhausted, …) must never abort the page.
            _logger.LogWarning(ex, "Agent {Agent} failed: {Message}", agent, ex.Message);
            output = $"(agent en échec : {ex.Message})";
        }

        timer.Stop();
        return new AgentTraceInfo(displayName, role, output, timer.Elapsed.TotalMilliseconds);
    }

    private static string AgentSystemPrompt(AgentType agent) => agent switch
    {
        AgentType.Summary => "Tu résumes les événements d'une histoire de façon concise et factuelle. " + FrenchOnly,
        AgentType.Consistency => "Tu vérifies la cohérence d'un texte narratif au regard des faits établis. Réponds brièvement. " + FrenchOnly,
        AgentType.Character => "Tu es l'archiviste des personnages. Tu tiens à jour, de façon factuelle, la fiche de chaque personnage d'une histoire. " + FrenchOnly,
        _ => "Tu es un agent narratif. " + FrenchOnly
    };

    /// <summary>
    /// Builds the prompt asking the Character agent to maintain a roster: one fiche per
    /// character with personality, evolution over the story, and key facts.
    /// </summary>
    private static string BuildCharacterRosterPrompt(string storyText, IReadOnlyList<string> knownNames)
    {
        var known = knownNames.Count > 0 ? string.Join(", ", knownNames) : "(aucun personnage prédéfini)";
        var story = string.IsNullOrWhiteSpace(storyText) ? "(histoire à peine commencée)" : storyText;

        return
            $"Voici l'histoire jusqu'ici :\n\n{story}\n\n" +
            $"Personnages connus au départ : {known}.\n\n" +
            "Dresse la liste de TOUS les personnages présents dans l'histoire. " +
            "Pour CHAQUE personnage, indique, dans cet ordre :\n" +
            "- Nom\n" +
            "- Personnalité\n" +
            "- Évolution au fil de l'histoire\n" +
            "- Faits marquants le concernant\n\n" +
            "Reste strictement factuel : n'invente rien qui ne découle pas de l'histoire ci-dessus. " +
            "Présente une section claire et distincte par personnage.";
    }

    /// <summary>
    /// Reads the per-agent traces stored for a page (Expert mode).
    /// </summary>
    public async Task<IReadOnlyList<AgentTraceInfo>> GetAgentTraceAsync(
        string slotName, int pageIndex, CancellationToken ct = default)
    {
        var json = await _storyRepository.GetPageExpertDataAsync(slotName, pageIndex, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<AgentTraceInfo>();

        try
        {
            return JsonSerializer.Deserialize<List<AgentTraceInfo>>(json) ?? (IReadOnlyList<AgentTraceInfo>)Array.Empty<AgentTraceInfo>();
        }
        catch (JsonException)
        {
            return Array.Empty<AgentTraceInfo>();
        }
    }

    /// <summary>
    /// Loads a specific page from database.
    /// </summary>
    public async Task<Result<PageInfo>> LoadPageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default)
    {
        var result = await _storyRepository.LoadPageAsync(slotName, pageIndex, ct);

        return result.Match<Result<PageInfo>>(
            onSuccess: page => Result<PageInfo>.Ok(new PageInfo(
                page.PageIndex,
                page.NarrativeText,
                page.GeneratedAt,
                page.ModelUsed)),
            onFailure: error => Result<PageInfo>.Fail(error));
    }

    /// <summary>
    /// Gets timeline summary (all page indices).
    /// </summary>
    public async Task<List<int>> GetPageHistoryAsync(
        string slotName,
        CancellationToken ct = default)
    {
        return await _storyRepository.GetPageHistoryAsync(slotName, ct);
    }

    /// <summary>
    /// Gets the display name for a story slot (falls back to slot name).
    /// </summary>
    public async Task<string> GetDisplayNameAsync(
        string slotName,
        CancellationToken ct = default)
    {
        return await _storyRepository.GetDisplayNameAsync(slotName, ct);
    }
}

/// <summary>
/// Simple DTO for page information.
/// </summary>
public record PageInfo(
    int PageIndex,
    string NarrativeText,
    DateTime GeneratedAt,
    string ModelUsed = "");
