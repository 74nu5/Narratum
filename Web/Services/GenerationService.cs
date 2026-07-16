using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Services;
using Narratum.Orchestration.Models;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Service for Blazor UI to interact with narrative generation.
/// Wraps FullOrchestrationService and IStoryRepository (hexagonal architecture).
/// </summary>
public class GenerationService
{
    private readonly FullOrchestrationService _orchestrator;
    private readonly IStoryRepository _storyRepository;
    private readonly ModelSelectionService _modelSelector;

    public GenerationService(
        FullOrchestrationService orchestrator,
        IStoryRepository storyRepository,
        ModelSelectionService modelSelector)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _storyRepository = storyRepository ?? throw new ArgumentNullException(nameof(storyRepository));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
    }

    /// <summary>
    /// Creates a new story slot in the database with initial state (page 0).
    /// </summary>
    public async Task<Result<string>> CreateStoryAsync(
        string slotName,
        StoryCreationRequest request,
        CancellationToken ct = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<string>.Fail("Le nom du slot ne peut pas être vide");

        if (request == null)
            return Result<string>.Fail("La requête ne peut pas être nulle");

        if (string.IsNullOrWhiteSpace(request.WorldName))
            return Result<string>.Fail("Le nom du monde ne peut pas être vide");

        if (!request.Characters.Any())
            return Result<string>.Fail("Au moins un personnage est requis");

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

        // Use repository (hexagonal architecture)
        var result = await _storyRepository.CreateStoryAsync(
            slotName,
            request.WorldName,
            request.GenreStyle,
            displayDescription,
            storyState,
            initText,
            ct);

        return result.Match<Result<string>>(
            onSuccess: metadata => Result<string>.Ok(metadata.SlotName),
            onFailure: error => Result<string>.Fail(error));
    }

    /// <summary>
    /// Generates next page using FullOrchestrationService.
    /// </summary>
    public async Task<Result<PageInfo>> GenerateNextPageAsync(
        string slotName,
        string intentDescription,
        CancellationToken ct = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<PageInfo>.Fail("Le nom du slot ne peut pas être vide");

        if (string.IsNullOrWhiteSpace(intentDescription))
            return Result<PageInfo>.Fail("La description de l'intention ne peut pas être vide");

        if (intentDescription.Length > 1000)
            return Result<PageInfo>.Fail("La description de l'intention est trop longue (max 1000 caractères)");

        // Load latest page using repository
        var loadResult = await _storyRepository.LoadLatestPageAsync(slotName, ct);

        if (!loadResult.IsSuccess)
            return Result<PageInfo>.Fail(loadResult.Error);

        var latestPage = ((Result<Core.PageSnapshot>.Success)loadResult).Value;
        var storyState = latestPage.State;

        // Create intent
        var intent = NarrativeIntent.Continue(intentDescription);

        // Execute pipeline
        var result = await _orchestrator.ExecuteCycleAsync(storyState, intent, ct);

        return await result.MatchAsync<Result<PageInfo>>(
            onSuccess: async pipelineResult =>
            {
                if (!pipelineResult.IsSuccess || pipelineResult.Output == null)
                    return Result<PageInfo>.Fail(pipelineResult.ErrorMessage ?? "Génération échouée");

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
                    onSuccess: savedPage => Result<PageInfo>.Ok(new PageInfo(
                        savedPage.PageIndex,
                        savedPage.NarrativeText,
                        savedPage.GeneratedAt)),
                    onFailure: error => Result<PageInfo>.Fail(error));
            },
            onFailure: error => Task.FromResult(Result<PageInfo>.Fail(error)));
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
                page.GeneratedAt)),
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
    DateTime GeneratedAt);
