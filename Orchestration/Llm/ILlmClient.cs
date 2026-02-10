using Narratum.Core;

namespace Narratum.Orchestration.Llm;

/// <summary>
/// Interface d'abstraction pour les clients LLM.
/// Le LLM est traité comme une boîte noire : entrée → sortie.
///
/// Cette abstraction permet de :
/// - Remplacer le LLM réel par un mock pour les tests
/// - Isoler complètement la logique métier de l'implémentation LLM
/// - Garantir que le système fonctionne même avec un "LLM stupide"
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Génère une réponse à partir d'une requête.
    /// </summary>
    /// <param name="request">Requête LLM.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat contenant la réponse ou une erreur.</returns>
    Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si le client est disponible et opérationnel.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>True si le client est prêt.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Nom du client (pour le logging).
    /// </summary>
    string ClientName { get; }

    /// <summary>
    /// Indique si ce client est un mock.
    /// </summary>
    bool IsMock { get; }
}
