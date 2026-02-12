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
    /// Liste toutes les histoires disponibles.
    /// </summary>
    public async Task<List<StoryEntry>> ListStoriesAsync()
    {
        var stories = await _dbContext.SaveSlots
            .OrderByDescending(s => s.LastSavedAt)
            .ToListAsync();

        return stories.Select(s => new StoryEntry
        {
            SlotName = s.SlotName,
            DisplayName = s.DisplayName ?? s.SlotName,
            LastModified = s.LastSavedAt,
            PageCount = 0, // TODO: Compter depuis PageSnapshots
            GenreStyle = "Fantasy", // TODO: Lire depuis PageSnapshot
            Description = s.Description,
            TotalWordCount = 0 // TODO: Calculer
        }).ToList();
    }

    /// <summary>
    /// Supprime une histoire.
    /// </summary>
    public async Task DeleteStoryAsync(string slotName)
    {
        var slot = await _dbContext.SaveSlots.FindAsync(slotName);
        if (slot != null)
        {
            _dbContext.SaveSlots.Remove(slot);
            
            // Supprimer aussi le snapshot
            var snapshot = await _dbContext.SavedStates
                .FirstOrDefaultAsync(s => s.SlotName == slotName);
            if (snapshot != null)
            {
                _dbContext.SavedStates.Remove(snapshot);
            }

            // Supprimer tous les PageSnapshots
            var pages = await _dbContext.PageSnapshots
                .Where(p => p.SlotName == slotName)
                .ToListAsync();
            _dbContext.PageSnapshots.RemoveRange(pages);

            await _dbContext.SaveChangesAsync();
        }
    }
}
