namespace Narratum.Web.Models;

/// <summary>
/// Métadonnées d'une histoire pour la liste/bibliothèque.
/// </summary>
public record StoryEntry
{
    public required string SlotName { get; init; }
    public required string DisplayName { get; init; }
    public required DateTime LastModified { get; init; }
    public required int PageCount { get; init; }
    public required string GenreStyle { get; init; }
    public string? Description { get; init; }
    public int TotalWordCount { get; init; }
}

/// <summary>
/// État complet d'une page d'histoire pour l'UI.
/// </summary>
public record PageState
{
    public required int PageIndex { get; init; }
    public required string NarrativeText { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public string? IntentDescription { get; init; }
    public string? ModelUsed { get; init; }
    public bool CanRegenerate => PageIndex > 0; // Pas de régénération pour page 0
}
