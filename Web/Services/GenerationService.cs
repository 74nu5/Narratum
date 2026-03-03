using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Services;
using Narratum.Orchestration.Models;
using Narratum.Persistence;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Service for Blazor UI to interact with narrative generation.
/// Wraps FullOrchestrationService and PersistenceService.
/// </summary>
public class GenerationService
{
    private readonly FullOrchestrationService _orchestrator;
    private readonly ISnapshotService _snapshotService;
    private readonly NarrativumDbContext _dbContext;
    private readonly ModelSelectionService _modelSelector;

    public GenerationService(
        FullOrchestrationService orchestrator,
        ISnapshotService snapshotService,
        NarrativumDbContext dbContext,
        ModelSelectionService modelSelector)
    {
        _orchestrator = orchestrator;
        _snapshotService = snapshotService;
        _dbContext = dbContext;
        _modelSelector = modelSelector;
    }

    /// <summary>
    /// Creates a new story slot in the database with initial state (page 0).
    /// </summary>
    public async Task<Result<string>> CreateStoryAsync(
        string slotName,
        StoryCreationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Create initial state with characters
            var worldId = Id.New();
            var characterStates = request.Characters.Select(c =>
            {
                var id = Id.New();
                return new CharacterState(id, c.Name);
            }).ToArray();

            var storyState = StoryState.Create(worldId, request.WorldName)
                .WithCharacters(characterStates);

            // Create snapshot
            var snapshot = _snapshotService.CreateSnapshot(storyState);
            var serializedSnapshot = JsonSerializer.Serialize(snapshot);

            var initText = $"Histoire créée: {request.WorldName}\nGenre: {request.GenreStyle}" +
                (string.IsNullOrWhiteSpace(request.WorldDescription) ? "" : $"\nMonde: {request.WorldDescription}") +
                $"\nPersonnages: {string.Join(", ", request.Characters.Select(c => c.Name))}";

            // Save initial page snapshot (page 0)
            var pageSnapshot = new PageSnapshotEntity
            {
                Id = Guid.NewGuid(),
                SlotName = slotName,
                PageIndex = 0,
                GeneratedAt = DateTime.UtcNow,
                NarrativeText = initText,
                SerializedState = serializedSnapshot,
                IntentDescription = "Création initiale",
                ModelUsed = "N/A",
                GenreStyle = request.GenreStyle
            };

            _dbContext.PageSnapshots.Add(pageSnapshot);

            // Register slot metadata so the library can list this story
            var metadata = new SaveSlotMetadata
            {
                SlotName = slotName,
                LastSavedAt = DateTime.UtcNow,
                TotalEvents = 0,
                CurrentChapterId = Guid.Empty,
                DisplayName = request.WorldName,
                Description = string.IsNullOrWhiteSpace(request.NarrativeStyle)
                    ? request.GenreStyle
                    : $"{request.GenreStyle} — {request.NarrativeStyle}"
            };

            _dbContext.SaveSlots.Add(metadata);
            await _dbContext.SaveChangesAsync(ct);

            return Result<string>.Ok(slotName);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Erreur création: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates next page using FullOrchestrationService.
    /// </summary>
    public async Task<Result<PageInfo>> GenerateNextPageAsync(
        string slotName,
        string intentDescription,
        CancellationToken ct = default)
    {
        try
        {
            // Load latest snapshot
            var latest = await _dbContext.PageSnapshots
                .Where(p => p.SlotName == slotName)
                .OrderByDescending(p => p.PageIndex)
                .FirstOrDefaultAsync(ct);

            if (latest == null)
                return Result<PageInfo>.Fail("Aucune histoire trouvée pour ce slot");

            // Deserialize StateSnapshot from JSON
            var stateSnapshot = JsonSerializer.Deserialize<Narratum.Persistence.StateSnapshot>(latest.SerializedState)
                ?? throw new InvalidOperationException("Failed to deserialize StateSnapshot");

            var storyStateResult = _snapshotService.RestoreFromSnapshot(stateSnapshot);

            if (storyStateResult is not Result<StoryState>.Success successResult)
                return Result<PageInfo>.Fail("Impossible de restaurer l'état de l'histoire");

            var storyState = successResult.Value;

            // Create intent
            var intent = NarrativeIntent.Continue(intentDescription);

            // Execute pipeline
            var result = await _orchestrator.ExecuteCycleAsync(storyState, intent, ct);

            return await result.MatchAsync<Result<PageInfo>>(
                onSuccess: async pipelineResult =>
                {
                    if (!pipelineResult.IsSuccess || pipelineResult.Output == null)
                        return Result<PageInfo>.Fail(pipelineResult.ErrorMessage ?? "Génération échouée");

                    var newSnapshot = _snapshotService.CreateSnapshot(storyState);
                    var serializedSnapshot = JsonSerializer.Serialize(newSnapshot);

                    var pageSnapshot = new PageSnapshotEntity
                    {
                        Id = Guid.NewGuid(),
                        SlotName = slotName,
                        PageIndex = latest.PageIndex + 1,
                        GeneratedAt = DateTime.UtcNow,
                        NarrativeText = pipelineResult.Output.NarrativeText,
                        SerializedState = serializedSnapshot,
                        IntentDescription = intentDescription,
                        ModelUsed = _modelSelector.CurrentNarratorModel,
                        GenreStyle = latest.GenreStyle
                    };

                    _dbContext.PageSnapshots.Add(pageSnapshot);
                    await _dbContext.SaveChangesAsync(ct);

                    return Result<PageInfo>.Ok(new PageInfo(
                        pageSnapshot.PageIndex,
                        pageSnapshot.NarrativeText,
                        pageSnapshot.GeneratedAt));
                },
                onFailure: error => Task.FromResult(Result<PageInfo>.Fail(error)));
        }
        catch (Exception ex)
        {
            return Result<PageInfo>.Fail($"Erreur génération: {ex.Message}", ex);
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
        try
        {
            var snapshot = await _dbContext.PageSnapshots
                .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);

            if (snapshot == null)
                return Result<PageInfo>.Fail($"Page {pageIndex} introuvable");

            return Result<PageInfo>.Ok(new PageInfo(
                snapshot.PageIndex,
                snapshot.NarrativeText ?? "",
                snapshot.GeneratedAt));
        }
        catch (Exception ex)
        {
            return Result<PageInfo>.Fail($"Erreur chargement: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets timeline summary (all page indices).
    /// </summary>
    public async Task<List<int>> GetPageHistoryAsync(
        string slotName,
        CancellationToken ct = default)
    {
        return await _dbContext.PageSnapshots
            .Where(p => p.SlotName == slotName)
            .OrderBy(p => p.PageIndex)
            .Select(p => p.PageIndex)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets the display name for a story slot (falls back to slot name).
    /// </summary>
    public async Task<string> GetDisplayNameAsync(
        string slotName,
        CancellationToken ct = default)
    {
        var slot = await _dbContext.SaveSlots
            .FirstOrDefaultAsync(s => s.SlotName == slotName, ct);
        return slot?.DisplayName ?? slotName;
    }
}

/// <summary>
/// Simple DTO for page information.
/// </summary>
public record PageInfo(
    int PageIndex,
    string NarrativeText,
    DateTime GeneratedAt);
