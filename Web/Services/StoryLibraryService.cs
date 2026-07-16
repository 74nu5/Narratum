using Narratum.Web.Models;
using Narratum.Core;

namespace Narratum.Web.Services;

/// <summary>
/// Service de gestion de la bibliothèque d'histoires.
/// Liste toutes les histoires sauvegardées.
/// Uses IStoryRepository (hexagonal architecture).
/// </summary>
public class StoryLibraryService
{
    private readonly IStoryRepository _storyRepository;

    public StoryLibraryService(IStoryRepository storyRepository)
    {
        _storyRepository = storyRepository ?? throw new ArgumentNullException(nameof(storyRepository));
    }

    /// <summary>
    /// Liste toutes les histoires disponibles.
    /// </summary>
    public async Task<List<StoryEntry>> ListStoriesAsync()
    {
        var stories = await _storyRepository.ListStoriesAsync();

        // Map Core.StoryEntry to Web.Models.StoryEntry
        return stories.Select(s => new StoryEntry
        {
            SlotName = s.SlotName,
            DisplayName = s.DisplayName,
            LastModified = s.LastUpdated,
            PageCount = s.PageCount,
            GenreStyle = s.GenreStyle,
            Description = s.Description
        }).ToList();
    }

    /// <summary>
    /// Supprime une histoire et toutes ses pages.
    /// </summary>
    public async Task DeleteStoryAsync(string slotName)
    {
        await _storyRepository.DeleteStoryAsync(slotName);
    }
}
