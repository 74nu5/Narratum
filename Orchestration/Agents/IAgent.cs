using Narratum.Core;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Interface de base pour tous les agents de génération narrative.
///
/// Un agent est responsable de générer un type spécifique de contenu narratif.
/// Chaque agent a une responsabilité unique et bien définie.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Type de cet agent.
    /// </summary>
    AgentType Type { get; }

    /// <summary>
    /// Nom de cet agent (pour le logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Traite un prompt et génère une réponse.
    /// </summary>
    /// <param name="prompt">Prompt à traiter.</param>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Réponse de l'agent.</returns>
    Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si cet agent peut traiter une intention donnée.
    /// </summary>
    /// <param name="intent">Intention narrative.</param>
    /// <returns>True si l'agent peut traiter cette intention.</returns>
    bool CanHandle(NarrativeIntent intent);
}
