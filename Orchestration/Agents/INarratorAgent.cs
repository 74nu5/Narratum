using Narratum.Core;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Agent spécialisé dans la génération de prose narrative.
///
/// Responsabilités :
/// - Générer des descriptions de scènes
/// - Créer la prose qui lie les dialogues
/// - Décrire les actions et mouvements
/// </summary>
public interface INarratorAgent : IAgent
{
    /// <summary>
    /// Génère une prose narrative.
    /// </summary>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="summary">Résumé des événements récents.</param>
    /// <param name="style">Style narratif souhaité.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Texte narratif généré.</returns>
    Task<Result<string>> GenerateNarrativeAsync(
        NarrativeContext context,
        string summary,
        NarrativeStyle style = NarrativeStyle.Descriptive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Décrit une scène à un lieu donné.
    /// </summary>
    /// <param name="location">Contexte du lieu.</param>
    /// <param name="presentCharacters">Personnages présents.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Description de la scène.</returns>
    Task<Result<string>> DescribeSceneAsync(
        LocationContext location,
        IReadOnlyList<CharacterContext> presentCharacters,
        CancellationToken cancellationToken = default);
}
