namespace Narratum.Orchestration.Models;

/// <summary>
/// Une information secrète de l'histoire. <see cref="IsRevealed"/> distingue ce que le lecteur
/// découvre (révélé) de ce que seul le narrateur sait (caché) — les secrets cachés préparent des
/// rebondissements et sont réinjectés dans les pages suivantes sans être montrés au joueur.
/// </summary>
public sealed record StorySecret(string Content, string Category, bool IsRevealed);

/// <summary>
/// Enveloppe de sortie structurée pour l'agent Secrets (racine objet, plus fiable qu'un tableau nu).
/// </summary>
public sealed record SecretSet(IReadOnlyList<StorySecret> Secrets);

/// <summary>Aides autour des secrets : nettoyage et normalisation de catégorie.</summary>
public static class StorySecrets
{
    /// <summary>Catégories reconnues.</summary>
    public static readonly IReadOnlyList<string> Categories = ["plot", "character", "location"];

    /// <summary>
    /// Écarte les secrets sans contenu, trime, et ramène la catégorie à l'une des
    /// <see cref="Categories"/> (défaut « plot »).
    /// </summary>
    public static IReadOnlyList<StorySecret> Clean(IEnumerable<StorySecret>? secrets)
    {
        var result = new List<StorySecret>();

        foreach (var s in secrets ?? [])
        {
            if (s is null || string.IsNullOrWhiteSpace(s.Content))
                continue;

            var category = (s.Category ?? string.Empty).Trim().ToLowerInvariant();
            if (!Categories.Contains(category))
                category = "plot";

            result.Add(new StorySecret(s.Content.Trim(), category, s.IsRevealed));
        }

        return result;
    }
}
