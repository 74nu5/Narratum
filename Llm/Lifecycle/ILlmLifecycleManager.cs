using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Lifecycle;

/// <summary>
/// Interface de gestion du cycle de vie d'un fournisseur LLM local.
/// </summary>
public interface ILlmLifecycleManager : IAsyncDisposable
{
    /// <summary>
    /// Liste les modèles connus du fournisseur (ids concrets, variantes incluses).
    /// </summary>
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si le service LLM est en cours d'exécution.
    /// </summary>
    Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// S'assure que le modèle est disponible (téléchargé et chargé) et retourne
    /// l'identifiant concret servi par le fournisseur (à utiliser tel quel dans la requête).
    /// </summary>
    Task<string> EnsureModelAvailableAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne l'URL de base du service LLM.
    /// </summary>
    Task<string> GetBaseUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Nom du fournisseur.
    /// </summary>
    string ProviderName { get; }
}
