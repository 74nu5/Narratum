using Narratum.Llm.Configuration;
using Narratum.Orchestration.Stages;

namespace Narratum.Web.Services;

/// <summary>
/// Service de résolution de modèle LLM dynamique (mutable, Scoped).
/// Implémente IModelResolver pour permettre la sélection de modèle à runtime.
/// </summary>
public class ModelSelectionService : IModelResolver
{
    private readonly LlmClientConfig _baseConfig;
    private string? _narratorModelOverride;

    public ModelSelectionService(LlmClientConfig baseConfig)
    {
        _baseConfig = baseConfig;
    }

    /// <summary>
    /// Modèle actuel pour le narrateur (peut être changé par l'utilisateur).
    /// </summary>
    public string CurrentNarratorModel
    {
        get => _narratorModelOverride ?? _baseConfig.NarratorModel ?? _baseConfig.DefaultModel;
        set => _narratorModelOverride = value;
    }

    /// <summary>
    /// Résout le modèle pour un agent donné (IModelResolver).
    /// </summary>
    public string ResolveModel(AgentType agentType)
    {
        // Pour le narrateur, utiliser l'override si défini
        if (agentType == AgentType.Narrator && _narratorModelOverride != null)
        {
            return _narratorModelOverride;
        }

        // Sinon déléguer à la config de base
        return _baseConfig.ResolveModel(agentType);
    }
}
