using Narratum.Web.Models;
using Narratum.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Narratum.Web.Services;

/// <summary>
/// Service de gestion de la bibliothèque d'histoires.
/// Liste toutes les histoires sauvegardées.
/// </summary>
public class StoryLibraryService
{
    private readonly NarrativumDbContext _dbContext;

    public StoryLibraryService(NarrativumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Liste toutes les histoires disponibles en se basant sur les PageSnapshots.
    /// </summary>
    public async Task<List<StoryEntry>> ListStoriesAsync()
    {
        // Load all page snapshots in memory for grouping (avoids EF GroupBy limitations)
        var allPages = await _dbContext.PageSnapshots
            .Select(p => new { p.SlotName, p.PageIndex, p.GeneratedAt, p.GenreStyle })
            .ToListAsync();

        if (allPages.Count == 0)
            return new List<StoryEntry>();

        // Load display names from SaveSlots (may be absent for old data)
        var slots = await _dbContext.SaveSlots
            .ToDictionaryAsync(s => s.SlotName);

        return allPages
            .GroupBy(p => p.SlotName)
            .Select(g =>
            {
                var initPage = g.FirstOrDefault(p => p.PageIndex == 0);
                slots.TryGetValue(g.Key, out var slot);
                return new StoryEntry
                {
                    SlotName = g.Key,
                    DisplayName = slot?.DisplayName ?? g.Key,
                    LastModified = g.Max(p => p.GeneratedAt),
                    PageCount = g.Count(p => p.PageIndex > 0),
                    GenreStyle = initPage?.GenreStyle ?? "Fantasy",
                    Description = slot?.Description
                };
            })
            .OrderByDescending(s => s.LastModified)
            .ToList();
    }

    /// <summary>
    /// Supprime une histoire et toutes ses pages.
    /// </summary>
    public async Task DeleteStoryAsync(string slotName)
    {
        // Remove SaveSlots entry if present
        var slot = await _dbContext.SaveSlots.FindAsync(slotName);
        if (slot != null)
        {
            _dbContext.SaveSlots.Remove(slot);
        }

        // Remove SavedStates entry if present
        var snapshot = await _dbContext.SavedStates
            .FirstOrDefaultAsync(s => s.SlotName == slotName);
        if (snapshot != null)
        {
            _dbContext.SavedStates.Remove(snapshot);
        }

        // Always remove all PageSnapshots for this slot
        var pages = await _dbContext.PageSnapshots
            .Where(p => p.SlotName == slotName)
            .ToListAsync();
        _dbContext.PageSnapshots.RemoveRange(pages);

        await _dbContext.SaveChangesAsync();
    }
}
