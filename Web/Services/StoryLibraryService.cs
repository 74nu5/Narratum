using Microsoft.Extensions.Logging;
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
    private readonly ILogger<StoryLibraryService> _logger;

    public StoryLibraryService(
        IStoryRepository storyRepository,
        ILogger<StoryLibraryService> logger)
    {
        _storyRepository = storyRepository ?? throw new ArgumentNullException(nameof(storyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Liste toutes les histoires disponibles.
    /// </summary>
    public async Task<List<Narratum.Web.Models.StoryEntry>> ListStoriesAsync()
    {
        _logger.LogDebug("Loading story library");

        var stories = await _storyRepository.ListStoriesAsync();

        _logger.LogInformation("Story library loaded: {StoryCount} stories found", stories.Count);

        // Map Core.StoryEntry to Web.Models.StoryEntry
        return [.. stories.Select(s => new Narratum.Web.Models.StoryEntry
        {
            SlotName = s.SlotName,
            DisplayName = s.DisplayName,
            LastModified = s.LastUpdated,
            PageCount = s.PageCount,
            GenreStyle = s.GenreStyle,
            Description = s.Description
        })];
    }

    /// <summary>
    /// Supprime une histoire et toutes ses pages.
    /// </summary>
    public async Task DeleteStoryAsync(string slotName)
    {
        _logger.LogInformation("Deleting story: {SlotName}", slotName);

        await _storyRepository.DeleteStoryAsync(slotName);

        _logger.LogInformation("Story deleted successfully: {SlotName}", slotName);
    }
}
