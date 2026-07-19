namespace Narratum.Web.Models;

/// <summary>
/// Données collectées par le Wizard pour créer une nouvelle histoire.
/// </summary>
public record StoryCreationRequest(
    string WorldName,
    string GenreStyle,
    List<(string Name, string? Description)> Characters,
    string? WorldDescription = null,
    string? NarrativeStyle = null,
    List<(string Name, string? Description)>? Locations = null,
    string? Model = null);

/// <summary>
/// La « bible » de l'univers : tout ce que l'auteur a défini à la création et qui ne change
/// plus ensuite. Persistée avec l'histoire et réinjectée dans les prompts, pour que le monde,
/// les lieux, les personnages et le ton pèsent réellement sur chaque page.
/// </summary>
public record StoryWorld(
    string WorldName,
    string GenreStyle,
    string? WorldDescription = null,
    string? NarrativeStyle = null,
    IReadOnlyList<WorldCharacter>? Characters = null,
    IReadOnlyList<WorldPlace>? Locations = null)
{
    /// <summary>Rendu lisible pour un prompt : rien n'est émis quand la bible est vide.</summary>
    public string ToPromptSection()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Monde : ").Append(this.WorldName);

        if (!string.IsNullOrWhiteSpace(this.GenreStyle))
            sb.Append(" (").Append(this.GenreStyle).Append(')');

        if (!string.IsNullOrWhiteSpace(this.WorldDescription))
            sb.Append('\n').Append(this.WorldDescription);

        if (!string.IsNullOrWhiteSpace(this.NarrativeStyle))
            sb.Append("\nStyle narratif à tenir : ").Append(this.NarrativeStyle);

        var characters = (this.Characters ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        if (characters.Count > 0)
        {
            sb.Append("\nPersonnages :");
            foreach (var c in characters)
                sb.Append("\n- ").Append(c.Name)
                  .Append(string.IsNullOrWhiteSpace(c.Description) ? string.Empty : $" — {c.Description}");
        }

        var places = (this.Locations ?? []).Where(l => !string.IsNullOrWhiteSpace(l.Name)).ToList();
        if (places.Count > 0)
        {
            sb.Append("\nLieux :");
            foreach (var l in places)
                sb.Append("\n- ").Append(l.Name)
                  .Append(string.IsNullOrWhiteSpace(l.Description) ? string.Empty : $" — {l.Description}");
        }

        return sb.ToString();
    }

    /// <summary>The bible as discrete statements, for the consistency agent's fact list.</summary>
    public IEnumerable<string> ToFacts()
    {
        if (!string.IsNullOrWhiteSpace(this.WorldDescription))
            yield return $"Univers : {this.WorldDescription}";

        foreach (var c in (this.Characters ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Description)))
            yield return $"{c.Name} : {c.Description}";

        foreach (var l in (this.Locations ?? []).Where(l => !string.IsNullOrWhiteSpace(l.Description)))
            yield return $"{l.Name} : {l.Description}";
    }
}

/// <summary>Un personnage tel que défini à la création (nom + description libre).</summary>
public record WorldCharacter(string Name, string? Description = null);

/// <summary>Un lieu tel que défini à la création (nom + description libre).</summary>
public record WorldPlace(string Name, string? Description = null);

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
