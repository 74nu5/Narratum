namespace Narratum.Orchestration.Models;

/// <summary>
/// Une proposition de suite présentée au joueur : une action courte et sa conséquence.
/// </summary>
public sealed record StoryChoice(string Text, string Description);

/// <summary>
/// Enveloppe de sortie structurée pour l'agent de choix (racine objet, plus fiable
/// qu'un tableau nu face à un schéma JSON).
/// </summary>
public sealed record ProposedChoices(IReadOnlyList<StoryChoice> Choices);

/// <summary>
/// Aides autour des choix proposés : garantit qu'exactement <see cref="RequiredCount"/>
/// options valides sont toujours présentées à l'UI, quels que soient les caprices du modèle.
/// </summary>
public static class StoryChoices
{
    /// <summary>Nombre de choix toujours présentés au joueur.</summary>
    public const int RequiredCount = 3;

    /// <summary>Choix de repli génériques, utilisés pour compléter si le modèle en produit trop peu.</summary>
    public static IReadOnlyList<StoryChoice> Fallback { get; } =
    [
        new("Continuer prudemment", "Avancer sans précipitation et observer ce qui vient."),
        new("Tenter une approche audacieuse", "Prendre un risque pour changer le cours des choses."),
        new("Marquer une pause et réfléchir", "Prendre le temps d'évaluer la situation avant d'agir."),
    ];

    /// <summary>
    /// Ramène une liste de choix (potentiellement vide, incomplète ou trop longue) à exactement
    /// <see cref="RequiredCount"/> entrées valides : les entrées sans texte sont écartées, le
    /// surplus est tronqué, et le manque est complété par des choix de repli.
    /// </summary>
    public static IReadOnlyList<StoryChoice> NormalizeToThree(IEnumerable<StoryChoice>? choices)
    {
        var valid = (choices ?? [])
            .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Text))
            .Select(c => new StoryChoice(c.Text.Trim(), (c.Description ?? string.Empty).Trim()))
            .Take(RequiredCount)
            .ToList();

        for (var i = valid.Count; i < RequiredCount; i++)
            valid.Add(Fallback[i]);

        return valid;
    }
}
