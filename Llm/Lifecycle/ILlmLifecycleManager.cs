namespace Narratum.Llm.Lifecycle;

/// <summary>
/// Interface de gestion du cycle de vie d'un fournisseur LLM local.
/// </summary>
public interface ILlmLifecycleManager : IAsyncDisposable
{
    /// <summary>
    /// Vérifie si le service LLM est en cours d'exécution.
    /// </summary>
    Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// S'assure que le modèle est disponible (téléchargé et chargé).
    /// </summary>
    Task EnsureModelAvailableAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne l'URL de base du service LLM.
    /// </summary>
    Task<string> GetBaseUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Nom du fournisseur.
    /// </summary>
    string ProviderName { get; }
}
