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
    /// Génère une réponse structurée désérialisée en <typeparamref name="T"/>.
    /// L'implémentation par défaut injecte un JSON Schema dans le prompt puis parse de façon
    /// tolérante (avec une nouvelle tentative). Un adaptateur disposant d'un support natif du
    /// schéma peut surcharger cette méthode pour un mode strict, en conservant ce filet tolérant.
    /// </summary>
    /// <typeparam name="T">Type cible de la désérialisation.</typeparam>
    /// <param name="request">Requête LLM.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat contenant l'objet désérialisé ou une erreur.</returns>
    Task<Result<T>> GenerateStructuredAsync<T>(
        LlmRequest request,
        CancellationToken cancellationToken = default)
        => StructuredLlm.GenerateViaPromptAsync<T>(this, request, cancellationToken);

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
}
