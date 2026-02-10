using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour construire le contexte narratif enrichi.
///
/// Le ContextBuilder est responsable de :
/// - Collecter les informations pertinentes depuis l'état actuel
/// - Récupérer la mémoire récente (memoranda)
/// - Identifier les personnages actifs
/// - Déterminer le lieu actuel
/// - Construire un contexte complet pour les agents
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Construit le contexte narratif à partir de l'état et de l'intention.
    /// </summary>
    /// <param name="currentState">État actuel de l'histoire.</param>
    /// <param name="intent">Intention narrative demandée.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Contexte narratif enrichi.</returns>
    Task<Result<NarrativeContext>> BuildAsync(
        StoryState currentState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default);
}
