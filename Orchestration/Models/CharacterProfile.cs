namespace Narratum.Orchestration.Models;

/// <summary>
/// Fiche structurée d'un personnage à un instant de l'histoire.
/// </summary>
public sealed record CharacterProfile(
    string Name,
    string Role,
    string Description,
    string Evolution,
    IReadOnlyList<string> KeyFacts);

/// <summary>
/// Enveloppe de sortie structurée pour l'agent Personnages (racine objet, plus fiable
/// qu'un tableau nu face à un schéma JSON).
/// </summary>
public sealed record CharacterRoster(IReadOnlyList<CharacterProfile> Characters);

/// <summary>Aides autour du casting : nettoie une liste de fiches potentiellement bruitée.</summary>
public static class StoryCharacters
{
    /// <summary>
    /// Écarte les fiches sans nom, dédoublonne par nom (insensible à la casse), trime les champs.
    /// </summary>
    public static IReadOnlyList<CharacterProfile> Clean(IEnumerable<CharacterProfile>? characters)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CharacterProfile>();

        foreach (var c in characters ?? [])
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Name))
                continue;

            var name = c.Name.Trim();
            if (!seen.Add(name))
                continue;

            result.Add(new CharacterProfile(
                name,
                (c.Role ?? string.Empty).Trim(),
                (c.Description ?? string.Empty).Trim(),
                (c.Evolution ?? string.Empty).Trim(),
                (c.KeyFacts ?? [])
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f.Trim())
                    .ToList()));
        }

        return result;
    }
}
