using Narratum.Core;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour construire les prompts destinés aux agents.
///
/// Le PromptBuilder est responsable de :
/// - Sélectionner les agents appropriés selon l'intention
/// - Construire les prompts système et utilisateur
/// - Injecter les variables contextuelles
/// - Définir l'ordre d'exécution
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Construit l'ensemble des prompts pour les agents.
    /// </summary>
    /// <param name="context">Contexte narratif enrichi.</param>
    /// <param name="intent">Intention narrative demandée.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Ensemble de prompts à exécuter.</returns>
    Task<Result<PromptSet>> BuildAsync(
        NarrativeContext context,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default);
}
