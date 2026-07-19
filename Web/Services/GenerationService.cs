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
    private readonly IImageGenerator _imageGenerator;
    private readonly ImageStorageService _imageStorage;
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
        IImageGenerator imageGenerator,
        ImageStorageService imageStorage,
        ILogger<GenerationService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _storyRepository = storyRepository ?? throw new ArgumentNullException(nameof(storyRepository));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _imageGenerator = imageGenerator ?? throw new ArgumentNullException(nameof(imageGenerator));
        _imageStorage = imageStorage ?? throw new ArgumentNullException(nameof(imageStorage));
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

        // Persist the full world definition: characters' descriptions, locations and narrative
        // style used to be collected by the wizard and then dropped on the floor. They now
        // survive as the story's bible and are fed back into every page (see the narrator below).
        if (result is Result<StoryMetadata>.Success)
        {
            var world = new StoryWorld(
                request.WorldName,
                request.GenreStyle,
                request.WorldDescription,
                request.NarrativeStyle,
                [.. request.Characters.Select(c => new WorldCharacter(c.Name, c.Description))],
                [.. (request.Locations ?? []).Select(l => new WorldPlace(l.Name, l.Description))]);

            try
            {
                await _storyRepository.SaveStoryWorldAsync(slotName, JsonSerializer.Serialize(world), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist the world bible for {SlotName}", slotName);
            }
        }

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
        string? imageModel = null,
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

        // Hidden secrets planted in earlier pages, fed back so the narrator can make them
        // subtly present without revealing them — the real hidden-state continuity that
        // story-maker generated but never re-consumed.
        var hiddenSecrets = await GetHiddenSecretsAsync(slotName, ct);
        if (hiddenSecrets.Count > 0)
            _logger.LogInformation("Feeding {Count} hidden secret(s) into the narrator for continuity", hiddenSecrets.Count);
        var hiddenSecretsSection = hiddenSecrets.Count > 0
            ? "\n\nFILS CACHÉS À FAIRE VIVRE (contexte secret : fais-les affleurer subtilement, ne les révèle PAS explicitement) :\n"
                + string.Join("\n", hiddenSecrets.Select(s => $"- {s}"))
            : string.Empty;

        // The world bible: what the author defined at creation (monde, ton, personnages, lieux).
        // Without it the narrator only ever sees names, and the setting stops constraining anything.
        var world = await GetStoryWorldAsync(slotName, ct);
        var worldSection = world is null
            ? string.Empty
            : "\n\nBIBLE DE L'UNIVERS (référence permanente — respecte-la, ne la recopie pas) :\n"
                + world.ToPromptSection();

        // 2. Narrator agent — the user-visible prose, streamed live.
        var narratorPrompt = _promptOptimizer.BuildOptimizedNarratorPrompt(
                storyState, intent, previousNarrative: latestPage.NarrativeText)
            + worldSection
            + $"\n\nRÉSUMÉ DU CONTEXTE (référence, ne pas recopier) :\n{summary.Output}"
            + hiddenSecretsSection
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
        // The bible is the canon the consistency agent should check against, alongside what the
        // characters have learned along the way.
        var establishedFacts = (world?.ToFacts() ?? [])
            .Concat(storyState.Characters.Values.SelectMany(c => c.KnownFacts))
            .Distinct()
            .ToList();
        traces.Add(await RunAgentAsync(
            AgentType.Consistency, "Cohérence", "Vérifie le texte contre les faits établis",
            _promptOptimizer.BuildOptimizedConsistencyPrompt(storyState, fullText, establishedFacts), chosenModel, ct));

        // 4. Character agent — maintains an evolving roster (role, description, arc, key facts per
        // character), re-derived from the full story as structured output and persisted for the UI.
        var knownNames = storyState.Characters.Values.Select(c => c.Name).ToList();
        var fullStory = string.IsNullOrWhiteSpace(storySoFar) ? fullText : storySoFar + "\n\n" + fullText;
        var characterTimer = Stopwatch.StartNew();
        var roster = await GenerateCharactersAsync(fullStory, knownNames, chosenModel, ct);
        characterTimer.Stop();
        try
        {
            await _storyRepository.SavePageCharactersAsync(
                slotName, newPageIndex, JsonSerializer.Serialize(roster), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist characters for page {PageIndex}", newPageIndex);
        }
        traces.Add(new AgentTraceInfo(
            "Personnages", "Casting structuré (rôle, évolution, faits)",
            string.Join("\n", roster.Select(c => $"• {c.Name} — {c.Role}")),
            characterTimer.Elapsed.TotalMilliseconds));

        // 5. Choice agent — proposes exactly 3 next-step options (structured output), persisted
        // for the UI. Best effort: the page is already saved, so a failure just yields fallbacks.
        var choiceTimer = Stopwatch.StartNew();
        var choices = await GenerateChoicesAsync(fullText, chosenModel, ct);
        choiceTimer.Stop();
        try
        {
            await _storyRepository.SavePageChoicesAsync(
                slotName, newPageIndex, JsonSerializer.Serialize(choices), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist choices for page {PageIndex}", newPageIndex);
        }
        traces.Add(new AgentTraceInfo(
            "Choix", "Propose 3 suites possibles",
            string.Join("\n", choices.Select(c => $"• {c.Text} — {c.Description}")),
            choiceTimer.Elapsed.TotalMilliseconds));

        // 6. Secret agent — tracks revealed/hidden information (structured), persisted. Hidden
        // secrets feed later pages' narrator for genuine continuity (see above).
        var secretTimer = Stopwatch.StartNew();
        var secrets = await GenerateSecretsAsync(fullText, intentDescription, chosenModel, ct);
        secretTimer.Stop();
        try
        {
            await _storyRepository.SavePageSecretsAsync(
                slotName, newPageIndex, JsonSerializer.Serialize(secrets), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist secrets for page {PageIndex}", newPageIndex);
        }
        traces.Add(new AgentTraceInfo(
            "Secrets", "Infos révélées + cachées",
            string.Join("\n", secrets.Select(s => $"• [{s.Category}] {(s.IsRevealed ? "révélé" : "caché")} — {s.Content}")),
            secretTimer.Elapsed.TotalMilliseconds));

        // 7. Image — two-stage: an ImagePrompt agent turns the page into a visual prompt, then the
        // chosen image model renders it. Only when the user picked an image model; best-effort.
        if (_imageGenerator.CanHandle(imageModel))
        {
            var imageTimer = Stopwatch.StartNew();
            var (imagePath, imagePrompt) = await GenerateImageAsync(slotName, newPageIndex, fullText, imageModel!, chosenModel, ct);
            imageTimer.Stop();
            if (imagePath is not null)
            {
                try
                {
                    await _storyRepository.SavePageImageAsync(slotName, newPageIndex, imagePath, imagePrompt, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist image for page {PageIndex}", newPageIndex);
                }
            }
            traces.Add(new AgentTraceInfo(
                "Image", "Prompt visuel + génération",
                imagePath is not null ? $"{imagePath}\n« {imagePrompt} »" : $"(échec) prompt : {imagePrompt}",
                imageTimer.Elapsed.TotalMilliseconds));
        }

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
        AgentType.Choice => "Tu conçois des choix interactifs qui font avancer une histoire. " + FrenchOnly,
        AgentType.Secret => "Tu gères les informations secrètes d'une histoire : ce que le lecteur découvre et ce que seul le narrateur sait. " + FrenchOnly,
        AgentType.ImagePrompt => "Tu transformes une scène narrative en un prompt visuel concis pour un générateur d'images. " + FrenchOnly,
        _ => "Tu es un agent narratif. " + FrenchOnly
    };

    /// <inheritdoc />
    public async Task<string> SuggestImagePromptAsync(
        string slotName, int pageIndex, CancellationToken ct = default)
    {
        // Prefer the prompt that produced the current illustration, so the author edits what
        // they can actually see. A failed attempt persists none, hence the fallback.
        var stored = await _storyRepository.GetPageImagePromptAsync(slotName, pageIndex, ct);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        var loadResult = await _storyRepository.LoadPageAsync(slotName, pageIndex, ct);
        if (loadResult is not Result<Core.PageSnapshot>.Success success)
            return string.Empty;

        var page = success.Value;
        var textModel = _modelSelector.NormalizeOrDefault(page.ModelUsed);

        return await DeriveImagePromptAsync(page.NarrativeText, textModel, ct);
    }

    /// <inheritdoc />
    public async Task<Result<string>> RegeneratePageImageAsync(
        string slotName, int pageIndex, string imageModel, string? imagePromptOverride = null,
        CancellationToken ct = default)
    {
        if (!_imageGenerator.CanHandle(imageModel))
            return Result<string>.Fail("Aucun modèle d'image sélectionné.");

        var loadResult = await _storyRepository.LoadPageAsync(slotName, pageIndex, ct);
        if (loadResult is Result<Core.PageSnapshot>.Failure loadFailure)
            return Result<string>.Fail(loadFailure.Message);

        var page = ((Result<Core.PageSnapshot>.Success)loadResult).Value;

        // With no override, re-derive the prompt from the page text: a failed first attempt
        // persists no prompt, so there is nothing to reuse.
        var textModel = _modelSelector.NormalizeOrDefault(page.ModelUsed);
        var (imagePath, imagePrompt) = await GenerateImageAsync(
            slotName, pageIndex, page.NarrativeText, imageModel, textModel, ct, imagePromptOverride);

        if (imagePath is null)
            return Result<string>.Fail("La génération de l'illustration a échoué.");

        await _storyRepository.SavePageImageAsync(slotName, pageIndex, imagePath, imagePrompt, ct);
        _logger.LogInformation("Regenerated image for {SlotName} page {PageIndex}", slotName, pageIndex);

        return Result<string>.Ok(imagePath);
    }

    /// <inheritdoc />
    public Task<int> TruncateAfterAsync(string slotName, int pageIndex, CancellationToken ct = default)
        => _storyRepository.TruncatePagesAfterAsync(slotName, pageIndex, ct);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StoryChoice>>> RegeneratePageChoicesAsync(
        string slotName, int pageIndex, string? model = null, CancellationToken ct = default)
    {
        var loadResult = await _storyRepository.LoadPageAsync(slotName, pageIndex, ct);
        if (loadResult is Result<Core.PageSnapshot>.Failure loadFailure)
            return Result<IReadOnlyList<StoryChoice>>.Fail(loadFailure.Message);

        var page = ((Result<Core.PageSnapshot>.Success)loadResult).Value;
        var chosenModel = _modelSelector.NormalizeOrDefault(model ?? page.ModelUsed);

        var choices = await GenerateChoicesAsync(page.NarrativeText, chosenModel, ct);
        await _storyRepository.SavePageChoicesAsync(
            slotName, pageIndex, JsonSerializer.Serialize(choices), ct);

        _logger.LogInformation("Regenerated choices for {SlotName} page {PageIndex}", slotName, pageIndex);

        return Result<IReadOnlyList<StoryChoice>>.Ok(choices);
    }

    /// <inheritdoc />
    public Task UpdatePageTextAsync(
        string slotName, int pageIndex, string narrativeText, CancellationToken ct = default)
        => _storyRepository.UpdatePageTextAsync(slotName, pageIndex, narrativeText, ct);

    /// <inheritdoc />
    public Task RenameStoryAsync(string slotName, string displayName, CancellationToken ct = default)
        => _storyRepository.RenameStoryAsync(slotName, displayName, ct);

    /// <summary>
    /// Two-stage image generation: an ImagePrompt agent turns the page into a visual prompt (using
    /// the text model), then the chosen image model renders it and the bytes are saved to a file.
    /// Returns the served image URL (or null on failure) and the prompt used.
    /// </summary>
    private async Task<(string? Path, string Prompt)> GenerateImageAsync(
        string slotName, int pageIndex, string narrative, string imageModel, string textModel,
        CancellationToken ct, string? explicitPrompt = null)
    {
        // An author-supplied prompt wins: no point asking the agent to rewrite what they typed.
        var imagePrompt = string.IsNullOrWhiteSpace(explicitPrompt)
            ? await DeriveImagePromptAsync(narrative, textModel, ct)
            : explicitPrompt.Trim();

        var result = await _imageGenerator.GenerateAsync(imagePrompt, imageModel, ct);
        if (result is Result<ImageResult>.Success success)
        {
            try
            {
                var path = await _imageStorage.SaveAsync(
                    slotName, pageIndex, success.Value.Bytes, success.Value.FileExtension, ct);
                return (path, imagePrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save generated image file for page {PageIndex}", pageIndex);
            }
        }
        else if (result is Result<ImageResult>.Failure failure)
        {
            _logger.LogWarning("Image generation failed: {Message}", failure.Message);
        }

        return (null, imagePrompt);
    }

    /// <summary>
    /// Reads the story's world bible. Returns null for stories created before it was persisted
    /// (or when the blob can't be read) — every caller degrades to the previous behaviour.
    /// </summary>
    private async Task<StoryWorld?> GetStoryWorldAsync(string slotName, CancellationToken ct)
    {
        try
        {
            var json = await _storyRepository.GetStoryWorldAsync(slotName, ct);

            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<StoryWorld>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read the world bible for {SlotName}", slotName);
            return null;
        }
    }

    /// <summary>
    /// Runs the ImagePrompt agent to turn a page into a visual prompt, falling back to the
    /// narrative itself when the agent produces nothing usable.
    /// </summary>
    private async Task<string> DeriveImagePromptAsync(string narrative, string textModel, CancellationToken ct)
    {
        var promptAgent = await RunAgentAsync(
            AgentType.ImagePrompt, "Prompt d'image", "Texte → prompt visuel",
            BuildImagePromptPrompt(narrative), textModel, ct);

        var imagePrompt = promptAgent.Output;
        if (string.IsNullOrWhiteSpace(imagePrompt) || imagePrompt.StartsWith("(agent", StringComparison.Ordinal))
            imagePrompt = narrative.Length > 400 ? narrative[..400] : narrative; // fallback: the narrative itself

        return imagePrompt;
    }

    /// <summary>Builds the prompt asking for a single concise visual prompt of the latest page.</summary>
    private static string BuildImagePromptPrompt(string narrative)
    {
        var scene = string.IsNullOrWhiteSpace(narrative) ? "(scène à peine commencée)" : narrative;

        return
            $"Voici la dernière page de l'histoire :\n\n{scene}\n\n" +
            "Rédige un UNIQUE prompt visuel décrivant cette scène pour un générateur d'images : " +
            "cadre, décor, personnages, ambiance et lumière, en une à deux phrases denses. " +
            "Style illustration / art numérique, cinématographique. Aucun texte ni dialogue dans l'image. " +
            "Réponds uniquement avec le prompt, sans préambule.";
    }

    /// <summary>
    /// Generates the structured secrets for a page — a mix of revealed (the reader just learned
    /// them) and hidden (only the narrator knows; setups for future twists) information.
    /// </summary>
    private async Task<IReadOnlyList<StorySecret>> GenerateSecretsAsync(
        string narrative, string chosenPath, string model, CancellationToken ct)
    {
        var request = new LlmRequest(
            AgentSystemPrompt(AgentType.Secret),
            BuildSecretsPrompt(narrative, chosenPath) + "\n\n" + FrenchOnly,
            LlmParameters.Default with { Temperature = _temperatures.GetTemperature(AgentType.Secret) },
            new Dictionary<string, object>
            {
                ["llm.agentType"] = AgentType.Secret,
                ["llm.model"] = model
            });

        try
        {
            var result = await _llmClient.GenerateStructuredAsync<SecretSet>(request, ct);
            if (result is Result<SecretSet>.Success success)
                return StorySecrets.Clean(success.Value.Secrets);

            _logger.LogWarning("Secret agent produced no valid structured output");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Secret agent failed");
        }

        return [];
    }

    /// <summary>
    /// Accumulates the hidden secrets (IsRevealed = false) from every prior page, so they can be
    /// fed back into the narrator prompt for continuity.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetHiddenSecretsAsync(string slotName, CancellationToken ct)
    {
        var allJson = await _storyRepository.GetAllPageSecretsAsync(slotName, ct);
        var hidden = new List<string>();

        foreach (var json in allJson ?? [])
        {
            try
            {
                var secrets = JsonSerializer.Deserialize<List<StorySecret>>(json);
                if (secrets is null)
                    continue;

                hidden.AddRange(secrets
                    .Where(s => !s.IsRevealed && !string.IsNullOrWhiteSpace(s.Content))
                    .Select(s => s.Content));
            }
            catch (JsonException)
            {
                // Skip a malformed page's secrets.
            }
        }

        return hidden.Distinct().ToList();
    }

    /// <summary>
    /// Builds the prompt asking the Secret agent for 0–3 secrets from the latest page, each tagged
    /// revealed or hidden, leaning toward hidden setups for future twists.
    /// </summary>
    private static string BuildSecretsPrompt(string narrative, string chosenPath)
    {
        var scene = string.IsNullOrWhiteSpace(narrative) ? "(scène à peine commencée)" : narrative;
        var path = string.IsNullOrWhiteSpace(chosenPath) ? string.Empty : $"Intention du joueur : {chosenPath}\n\n";

        return
            $"Voici la dernière page de l'histoire :\n\n{scene}\n\n" + path +
            "Identifie 0 à 3 informations secrètes de cette scène. Pour chacune :\n" +
            "- « Content » : le secret, en une phrase ;\n" +
            "- « Category » : « plot », « character » ou « location » ;\n" +
            "- « IsRevealed » : true si le lecteur vient de le découvrir dans le texte ; " +
            "false si c'est un élément que SEUL le narrateur sait (préparation d'un rebondissement futur, non montré au joueur).\n\n" +
            "Vise environ un tiers de secrets révélés et deux tiers cachés. Appuie-toi sur le texte ; " +
            "les secrets cachés peuvent préparer la suite.";
    }

    /// <summary>
    /// Proposes the next-step choices via structured output, always normalized to exactly three
    /// so the UI is stable even when a small local model returns too few or malformed options.
    /// </summary>
    private async Task<IReadOnlyList<StoryChoice>> GenerateChoicesAsync(
        string narrative, string model, CancellationToken ct)
    {
        var request = new LlmRequest(
            AgentSystemPrompt(AgentType.Choice),
            BuildChoicesPrompt(narrative) + "\n\n" + FrenchOnly,
            LlmParameters.Default with { Temperature = _temperatures.GetTemperature(AgentType.Choice) },
            new Dictionary<string, object>
            {
                ["llm.agentType"] = AgentType.Choice,
                ["llm.model"] = model
            });

        try
        {
            var result = await _llmClient.GenerateStructuredAsync<ProposedChoices>(request, ct);
            if (result is Result<ProposedChoices>.Success success)
                return StoryChoices.NormalizeToThree(success.Value.Choices);

            _logger.LogWarning("Choice agent produced no valid structured output; using fallback choices");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Choice agent failed; using fallback choices");
        }

        return StoryChoices.NormalizeToThree(null);
    }

    /// <summary>
    /// Builds the prompt asking for exactly three distinct next-step options, each with a short
    /// action and its consequence, adapted to the tone of the latest page.
    /// </summary>
    private static string BuildChoicesPrompt(string narrative)
    {
        var scene = string.IsNullOrWhiteSpace(narrative) ? "(scène à peine commencée)" : narrative;

        return
            $"Voici la dernière page de l'histoire :\n\n{scene}\n\n" +
            "Propose EXACTEMENT 3 suites possibles, nettement différentes les unes des autres, " +
            "sans qu'aucune ne soit évidemment meilleure. Pour chaque choix :\n" +
            "- « Text » : l'action, à l'infinitif, 8 à 15 mots ;\n" +
            "- « Description » : la conséquence pressentie, intrigante, 15 à 25 mots.\n" +
            "Adapte le ton à celui du récit ci-dessus.";
    }

    /// <summary>
    /// Generates the structured character roster (role, description, evolution, key facts per
    /// character), re-derived from the whole story so it reflects their evolution.
    /// </summary>
    private async Task<IReadOnlyList<CharacterProfile>> GenerateCharactersAsync(
        string fullStory, IReadOnlyList<string> knownNames, string model, CancellationToken ct)
    {
        var request = new LlmRequest(
            AgentSystemPrompt(AgentType.Character),
            BuildCharacterRosterPrompt(fullStory, knownNames) + "\n\n" + FrenchOnly,
            LlmParameters.Default with { Temperature = _temperatures.GetTemperature(AgentType.Character) },
            new Dictionary<string, object>
            {
                ["llm.agentType"] = AgentType.Character,
                ["llm.model"] = model
            });

        try
        {
            var result = await _llmClient.GenerateStructuredAsync<CharacterRoster>(request, ct);
            if (result is Result<CharacterRoster>.Success success)
                return StoryCharacters.Clean(success.Value.Characters);

            _logger.LogWarning("Character agent produced no valid structured output");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Character agent failed");
        }

        return [];
    }

    /// <summary>
    /// Builds the prompt asking the Character agent to maintain a roster: one entry per character
    /// with role, a short description, evolution over the story, and key facts.
    /// </summary>
    private static string BuildCharacterRosterPrompt(string storyText, IReadOnlyList<string> knownNames)
    {
        var known = knownNames.Count > 0 ? string.Join(", ", knownNames) : "(aucun personnage prédéfini)";
        var story = string.IsNullOrWhiteSpace(storyText) ? "(histoire à peine commencée)" : storyText;

        return
            $"Voici l'histoire jusqu'ici :\n\n{story}\n\n" +
            $"Personnages connus au départ : {known}.\n\n" +
            "Recense TOUS les personnages présents dans l'histoire. Pour chacun :\n" +
            "- « Name » : le nom ou un descripteur (ex. « Le Gardien ») ;\n" +
            "- « Role » : sa fonction narrative (protagoniste, allié, adversaire, mentor…) ;\n" +
            "- « Description » : une description concise (5 à 15 mots) ;\n" +
            "- « Evolution » : comment il évolue au fil de l'histoire (vide si trop tôt) ;\n" +
            "- « KeyFacts » : les faits marquants le concernant (liste courte).\n\n" +
            "Reste strictement factuel : n'invente rien qui ne découle pas de l'histoire ci-dessus.";
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
    /// Reads the next-step choices stored for a page, normalized to exactly three.
    /// </summary>
    public async Task<IReadOnlyList<StoryChoice>> GetPageChoicesAsync(
        string slotName, int pageIndex, CancellationToken ct = default)
    {
        var json = await _storyRepository.GetPageChoicesAsync(slotName, pageIndex, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<StoryChoice>();

        try
        {
            return JsonSerializer.Deserialize<List<StoryChoice>>(json) ?? (IReadOnlyList<StoryChoice>)Array.Empty<StoryChoice>();
        }
        catch (JsonException)
        {
            return Array.Empty<StoryChoice>();
        }
    }

    /// <summary>
    /// Reads the character roster stored for a page.
    /// </summary>
    public async Task<IReadOnlyList<CharacterProfile>> GetPageCharactersAsync(
        string slotName, int pageIndex, CancellationToken ct = default)
    {
        var json = await _storyRepository.GetPageCharactersAsync(slotName, pageIndex, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<CharacterProfile>();

        try
        {
            return JsonSerializer.Deserialize<List<CharacterProfile>>(json) ?? (IReadOnlyList<CharacterProfile>)Array.Empty<CharacterProfile>();
        }
        catch (JsonException)
        {
            return Array.Empty<CharacterProfile>();
        }
    }

    /// <summary>
    /// Reads the secrets stored for a page (both revealed and hidden; the UI shows only revealed).
    /// </summary>
    public async Task<IReadOnlyList<StorySecret>> GetPageSecretsAsync(
        string slotName, int pageIndex, CancellationToken ct = default)
    {
        var json = await _storyRepository.GetPageSecretsAsync(slotName, pageIndex, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<StorySecret>();

        try
        {
            return JsonSerializer.Deserialize<List<StorySecret>>(json) ?? (IReadOnlyList<StorySecret>)Array.Empty<StorySecret>();
        }
        catch (JsonException)
        {
            return Array.Empty<StorySecret>();
        }
    }

    /// <summary>
    /// Reads the generated image URL for a page, or null if none.
    /// </summary>
    public async Task<string?> GetPageImageAsync(string slotName, int pageIndex, CancellationToken ct = default)
        => await _storyRepository.GetPageImageAsync(slotName, pageIndex, ct);

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
                page.ModelUsed,
                page.IntentDescription)),
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

    public async Task<IReadOnlyDictionary<int, string?>> GetPageModelsAsync(
        string slotName,
        CancellationToken ct = default)
    {
        return await _storyRepository.GetPageModelsAsync(slotName, ct);
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
    string ModelUsed = "",
    string IntentDescription = "");
