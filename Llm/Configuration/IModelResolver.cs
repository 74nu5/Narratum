using Narratum.Orchestration.Stages;

namespace Narratum.Llm.Configuration;

/// <summary>
/// Interface pour résoudre dynamiquement le modèle LLM à utiliser pour un agent.
/// Permet la sélection de modèle à runtime (mutable).
/// </summary>
public interface IModelResolver
{
    /// <summary>
    /// Résout le modèle à utiliser pour un type d'agent donné.
    /// </summary>
    string ResolveModel(AgentType agentType);
}
