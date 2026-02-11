using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Factory;

/// <summary>
/// Factory pour créer et configurer les clients LLM.
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// Crée un client LLM configuré selon la configuration courante.
    /// </summary>
    Task<ILlmClient> CreateClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne la configuration courante.
    /// </summary>
    LlmClientConfig Config { get; }
}
