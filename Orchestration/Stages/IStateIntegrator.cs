using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour intégrer les résultats dans l'état du monde.
///
/// Le StateIntegrator est responsable de :
/// - Convertir les sorties brutes en sortie narrative finale
/// - Extraire les événements générés
/// - Identifier les changements d'état
/// - Créer le memorandum correspondant
/// </summary>
public interface IStateIntegrator
{
    /// <summary>
    /// Intègre la sortie brute dans une sortie narrative finale.
    /// </summary>
    /// <param name="rawOutput">Sortie brute des agents.</param>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Sortie narrative finale.</returns>
    Task<NarrativeOutput> IntegrateAsync(
        RawOutput rawOutput,
        NarrativeContext context,
        CancellationToken cancellationToken = default);
}
