using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Narratum.Core;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Entity Framework implementation of IStoryRepository.
/// Provides data access for story persistence while maintaining hexagonal architecture.
///
/// Uses an <see cref="IDbContextFactory{TContext}"/> to create a fresh, short-lived
/// DbContext per operation. In Blazor Server a scoped DbContext lives for the whole
/// circuit, which accumulates tracked entities across operations and leaves behind
/// uncommitted 'Added' entities when a save fails — causing tracking conflicts and
/// duplicate-insert (UNIQUE constraint) errors on later calls. A per-operation context
/// avoids that entire class of problems.
/// </summary>
public class StoryRepository : IStoryRepository
{
    private readonly IDbContextFactory<NarrativumDbContext> _contextFactory;
    private readonly ISnapshotService _snapshotService;

    public StoryRepository(IDbContextFactory<NarrativumDbContext> contextFactory, ISnapshotService snapshotService)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
    }

    public async Task<Result<StoryMetadata>> CreateStoryAsync(
        string slotName,
        string worldName,
        string genreStyle,
        string displayDescription,
        StoryState initialState,
        string initialNarrativeText,
        string initialModel,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            // Check if story already exists
            var exists = await db.PageSnapshots.AnyAsync(p => p.SlotName == slotName, ct);
            if (exists)
                return Result<StoryMetadata>.Fail($"Une histoire existe déjà avec le nom '{slotName}'");

            // Create snapshot
            var snapshot = _snapshotService.CreateSnapshot(initialState);
            var serializedSnapshot = JsonSerializer.Serialize(snapshot);

            // Save initial page snapshot (page 0)
            var pageSnapshot = new PageSnapshotEntity
            {
                Id = Guid.NewGuid(),
                SlotName = slotName,
                PageIndex = 0,
                GeneratedAt = DateTime.UtcNow,
                NarrativeText = initialNarrativeText,
                SerializedState = serializedSnapshot,
                IntentDescription = "Création initiale",
                ModelUsed = initialModel,
                GenreStyle = genreStyle
            };

            db.PageSnapshots.Add(pageSnapshot);

            // Register slot metadata
            var metadata = new SaveSlotMetadata
            {
                SlotName = slotName,
                LastSavedAt = DateTime.UtcNow,
                TotalEvents = 0,
                CurrentChapterId = Guid.Empty,
                DisplayName = worldName,
                Description = displayDescription
            };

            db.SaveSlots.Add(metadata);
            await db.SaveChangesAsync(ct);

            return Result<StoryMetadata>.Ok(new StoryMetadata(
                slotName,
                worldName,
                genreStyle,
                DateTime.UtcNow,
                TotalPages: 1));
        }
        catch (DbUpdateException ex)
        {
            return Result<StoryMetadata>.Fail($"Erreur base de données lors de la création: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            return Result<StoryMetadata>.Fail($"Erreur de sérialisation: {ex.Message}", ex);
        }
    }

    public async Task<Result<PageSnapshot>> SavePageAsync(
        string slotName,
        int pageIndex,
        string narrativeText,
        string intentDescription,
        string modelUsed,
        StoryState currentState,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            // Verify story exists
            var exists = await db.PageSnapshots.AnyAsync(p => p.SlotName == slotName, ct);
            if (!exists)
                return Result<PageSnapshot>.Fail($"Aucune histoire trouvée pour le slot '{slotName}'");

            // Create snapshot
            var snapshot = _snapshotService.CreateSnapshot(currentState);
            var serializedSnapshot = JsonSerializer.Serialize(snapshot);

            // Get genre from metadata
            var metadata = await db.SaveSlots.FindAsync(new object[] { slotName }, ct);
            var genreStyle = "Unknown";
            if (metadata != null)
            {
                // Try to get genre from first page
                var firstPage = await db.PageSnapshots
                    .Where(p => p.SlotName == slotName && p.PageIndex == 0)
                    .FirstOrDefaultAsync(ct);
                genreStyle = firstPage?.GenreStyle ?? "Unknown";
            }

            var pageSnapshot = new PageSnapshotEntity
            {
                Id = Guid.NewGuid(),
                SlotName = slotName,
                PageIndex = pageIndex,
                GeneratedAt = DateTime.UtcNow,
                NarrativeText = narrativeText,
                SerializedState = serializedSnapshot,
                IntentDescription = intentDescription,
                ModelUsed = modelUsed,
                GenreStyle = genreStyle
            };

            db.PageSnapshots.Add(pageSnapshot);

            // Update metadata in place. `metadata` was loaded via FindAsync and is therefore
            // already tracked by this context, so updating its values avoids the
            // "another instance with the same key value is already being tracked" error that
            // attaching a second instance via Update() would cause.
            if (metadata != null)
            {
                var updatedMetadata = metadata with
                {
                    LastSavedAt = DateTime.UtcNow,
                    TotalEvents = currentState.EventHistory.Count
                };
                db.Entry(metadata).CurrentValues.SetValues(updatedMetadata);
            }

            await db.SaveChangesAsync(ct);

            return Result<PageSnapshot>.Ok(new PageSnapshot(
                slotName,
                pageIndex,
                narrativeText,
                intentDescription,
                modelUsed,
                DateTime.UtcNow,
                currentState));
        }
        catch (DbUpdateException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur base de données lors de la sauvegarde: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur de sérialisation: {ex.Message}", ex);
        }
    }

    public async Task<Result<PageSnapshot>> LoadPageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var pageEntity = await db.PageSnapshots
                .AsNoTracking()
                .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
                .FirstOrDefaultAsync(ct);

            if (pageEntity == null)
                return Result<PageSnapshot>.Fail($"Page {pageIndex} introuvable pour le slot '{slotName}'");

            var stateSnapshot = JsonSerializer.Deserialize<StateSnapshot>(pageEntity.SerializedState)
                ?? throw new InvalidOperationException("Failed to deserialize StateSnapshot");

            var storyStateResult = _snapshotService.RestoreFromSnapshot(stateSnapshot);

            if (storyStateResult is not Result<StoryState>.Success successResult)
                return Result<PageSnapshot>.Fail("Impossible de restaurer l'état de l'histoire");

            return Result<PageSnapshot>.Ok(new PageSnapshot(
                pageEntity.SlotName,
                pageEntity.PageIndex,
                pageEntity.NarrativeText ?? string.Empty,
                pageEntity.IntentDescription ?? string.Empty,
                pageEntity.ModelUsed ?? "N/A",
                pageEntity.GeneratedAt,
                successResult.Value));
        }
        catch (JsonException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur de désérialisation: {ex.Message}", ex);
        }
        catch (DbUpdateException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur base de données: {ex.Message}", ex);
        }
    }

    public async Task<Result<PageSnapshot>> LoadLatestPageAsync(
        string slotName,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var pageEntity = await db.PageSnapshots
                .AsNoTracking()
                .Where(p => p.SlotName == slotName)
                .OrderByDescending(p => p.PageIndex)
                .FirstOrDefaultAsync(ct);

            if (pageEntity == null)
                return Result<PageSnapshot>.Fail($"Aucune page trouvée pour le slot '{slotName}'");

            var stateSnapshot = JsonSerializer.Deserialize<StateSnapshot>(pageEntity.SerializedState)
                ?? throw new InvalidOperationException("Failed to deserialize StateSnapshot");

            var storyStateResult = _snapshotService.RestoreFromSnapshot(stateSnapshot);

            if (storyStateResult is not Result<StoryState>.Success successResult)
                return Result<PageSnapshot>.Fail("Impossible de restaurer l'état de l'histoire");

            return Result<PageSnapshot>.Ok(new PageSnapshot(
                pageEntity.SlotName,
                pageEntity.PageIndex,
                pageEntity.NarrativeText ?? string.Empty,
                pageEntity.IntentDescription ?? string.Empty,
                pageEntity.ModelUsed ?? "N/A",
                pageEntity.GeneratedAt,
                successResult.Value));
        }
        catch (JsonException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur de désérialisation: {ex.Message}", ex);
        }
        catch (DbUpdateException ex)
        {
            return Result<PageSnapshot>.Fail($"Erreur base de données: {ex.Message}", ex);
        }
    }

    public async Task<List<StoryEntry>> ListStoriesAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Pull the minimal columns and group client-side: EF Core cannot translate a
        // GroupBy whose projection calls g.First() (returns an entity) into SQL.
        // A story library is small, so in-memory grouping is both correct and cheap.
        var pages = await db.PageSnapshots
            .AsNoTracking()
            .Select(p => new { p.SlotName, p.GenreStyle, p.GeneratedAt })
            .ToListAsync(ct);

        var storyGroups = pages
            .GroupBy(p => p.SlotName)
            .Select(g =>
            {
                var genre = g.OrderByDescending(p => p.GeneratedAt).First().GenreStyle ?? "Unknown";
                return new StoryEntry(
                    g.Key,
                    genre, // DisplayName approximated from genre
                    genre,
                    g.Count(),
                    g.Max(p => p.GeneratedAt),
                    genre);
            })
            .OrderByDescending(s => s.LastUpdated)
            .ToList();

        // Enrich with metadata if available
        var metadataBySlot = await db.SaveSlots
            .AsNoTracking()
            .ToDictionaryAsync(m => m.SlotName, ct);

        var enrichedStories = new List<StoryEntry>();
        foreach (var story in storyGroups)
        {
            if (metadataBySlot.TryGetValue(story.SlotName, out var metadata))
            {
                enrichedStories.Add(story with
                {
                    DisplayName = metadata.DisplayName ?? story.DisplayName,
                    Description = metadata.Description ?? story.Description,
                    UniverseId = metadata.UniverseId
                });
            }
            else
            {
                enrichedStories.Add(story);
            }
        }

        return enrichedStories;
    }

    public async Task DeleteStoryAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var pages = await db.PageSnapshots
            .Where(p => p.SlotName == slotName)
            .ToListAsync(ct);

        db.PageSnapshots.RemoveRange(pages);

        var metadata = await db.SaveSlots.FindAsync(new object[] { slotName }, ct);
        if (metadata != null)
            db.SaveSlots.Remove(metadata);

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> TruncatePagesAfterAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var obsolete = await db.PageSnapshots
            .Where(p => p.SlotName == slotName && p.PageIndex > pageIndex)
            .ToListAsync(ct);

        if (obsolete.Count == 0)
            return 0;

        db.PageSnapshots.RemoveRange(obsolete);
        await db.SaveChangesAsync(ct);

        return obsolete.Count;
    }

    public async Task<bool> StoryExistsAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AnyAsync(p => p.SlotName == slotName, ct);
    }

    public async Task<List<int>> GetPageHistoryAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName)
            .OrderBy(p => p.PageIndex)
            .Select(p => p.PageIndex)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, string?>> GetPageModelsAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var rows = await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName)
            .Select(p => new { p.PageIndex, p.ModelUsed })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.PageIndex, r => r.ModelUsed);
    }

    public async Task SavePageExpertDataAsync(string slotName, int pageIndex, string expertData, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { SerializedPipelineResult = expertData });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetPageExpertDataAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.SerializedPipelineResult)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SavePageChoicesAsync(string slotName, int pageIndex, string choicesJson, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { SerializedChoices = choicesJson });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetPageChoicesAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.SerializedChoices)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SavePageCharactersAsync(string slotName, int pageIndex, string charactersJson, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { SerializedCharacters = charactersJson });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetPageCharactersAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.SerializedCharacters)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SavePageSecretsAsync(string slotName, int pageIndex, string secretsJson, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { SerializedSecrets = secretsJson });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetPageSecretsAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.SerializedSecrets)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetAllPageSecretsAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.SerializedSecrets != null)
            .OrderBy(p => p.PageIndex)
            .Select(p => p.SerializedSecrets!)
            .ToListAsync(ct);
    }

    public async Task SavePageImageAsync(string slotName, int pageIndex, string imagePath, string imagePrompt, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { ImagePath = imagePath, ImagePrompt = imagePrompt });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetPageImagePromptAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.ImagePrompt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetPageImageAsync(string slotName, int pageIndex, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.PageIndex == pageIndex)
            .Select(p => p.ImagePath)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string> GetStoryTextAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var texts = await db.PageSnapshots
            .AsNoTracking()
            .Where(p => p.SlotName == slotName && p.NarrativeText != null)
            .OrderBy(p => p.PageIndex)
            .Select(p => p.NarrativeText!)
            .ToListAsync(ct);

        return string.Join("\n\n", texts);
    }

    public async Task<string> GetDisplayNameAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var metadata = await db.SaveSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.SlotName == slotName, ct);
        return metadata?.DisplayName ?? slotName;
    }

    public async Task SaveStoryWorldAsync(string slotName, string worldJson, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var metadata = await db.SaveSlots.FirstOrDefaultAsync(m => m.SlotName == slotName, ct);
        if (metadata == null)
            return;

        db.Entry(metadata).CurrentValues.SetValues(metadata with { SerializedWorld = worldJson });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetStoryWorldAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.SaveSlots
            .AsNoTracking()
            .Where(m => m.SlotName == slotName)
            .Select(m => m.SerializedWorld)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SetStoryUniverseAsync(string slotName, string? universeId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var metadata = await db.SaveSlots.FirstOrDefaultAsync(m => m.SlotName == slotName, ct);
        if (metadata == null)
            return;

        db.Entry(metadata).CurrentValues.SetValues(metadata with { UniverseId = universeId });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetStoryUniverseAsync(string slotName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        return await db.SaveSlots
            .AsNoTracking()
            .Where(m => m.SlotName == slotName)
            .Select(m => m.UniverseId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task RenameStoryAsync(string slotName, string displayName, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var metadata = await db.SaveSlots.FirstOrDefaultAsync(m => m.SlotName == slotName, ct);
        if (metadata == null)
            return;

        db.Entry(metadata).CurrentValues.SetValues(metadata with { DisplayName = displayName });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePageTextAsync(
        string slotName, int pageIndex, string narrativeText, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var page = await db.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);
        if (page == null)
            return;

        db.Entry(page).CurrentValues.SetValues(page with { NarrativeText = narrativeText });
        await db.SaveChangesAsync(ct);
    }
}
