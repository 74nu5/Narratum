using Narratum.Orchestration.Stages;

namespace Narratum.Llm.Configuration;

/// <summary>
/// Configuration principale du client LLM.
/// </summary>
public sealed record LlmClientConfig
{
    /// <summary>
    /// Type de fournisseur LLM.
    /// </summary>
    public LlmProviderType Provider { get; init; } = LlmProviderType.FoundryLocal;

    /// <summary>
    /// URL de base de l'API. Automatiquement découverte pour Foundry Local.
    /// Pour Ollama : http://localhost:11434/v1
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Modèle par défaut utilisé quand aucun mapping agent n'est défini.
    /// </summary>
    public string DefaultModel { get; init; } = "phi-4-mini";

    /// <summary>
    /// Timeout réseau pour chaque requête (secondes). Généreux par défaut : la génération
    /// locale (gros modèle, longue histoire, agents en série) peut dépasser 100 s.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Mapping optionnel agent → modèle. Permet à chaque agent d'utiliser un modèle différent.
    /// Si un agent n'est pas dans le mapping, le DefaultModel est utilisé.
    /// </summary>
    public Dictionary<AgentType, string> AgentModelMapping { get; init; } = new();

    /// <summary>
    /// Modèle utilisé par l'agent narrateur. Surcharge le mapping pour AgentType.Narrator.
    /// Permet à l'utilisateur de choisir le modèle qui raconte l'histoire.
    /// </summary>
    public string? NarratorModel { get; init; }

    /// <summary>
    /// Configuration spécifique Foundry Local.
    /// </summary>
    public FoundryLocalConfig FoundryLocal { get; init; } = new();

    /// <summary>
    /// Configuration spécifique Azure AI Foundry (cloud).
    /// </summary>
    public AzureFoundryConfig AzureFoundry { get; init; } = new();

    /// <summary>
    /// Résout le modèle à utiliser pour un type d'agent donné.
    /// Priorité : NarratorModel (pour Narrator) > AgentModelMapping > DefaultModel.
    /// </summary>
    public string ResolveModel(AgentType agentType)
    {
        if (agentType == AgentType.Narrator && !string.IsNullOrEmpty(NarratorModel))
            return NarratorModel;

        if (AgentModelMapping.TryGetValue(agentType, out var model))
            return model;

        return DefaultModel;
    }

    /// <summary>
    /// Configuration par défaut pour Foundry Local avec Phi-4-mini.
    /// </summary>
    public static LlmClientConfig DefaultFoundryLocal => new()
    {
        Provider = LlmProviderType.FoundryLocal,
        DefaultModel = "phi-4-mini"
    };

    /// <summary>
    /// Configuration par défaut pour Ollama avec Phi-4-mini.
    /// </summary>
    public static LlmClientConfig DefaultOllama => new()
    {
        Provider = LlmProviderType.Ollama,
        BaseUrl = "http://localhost:11434/v1",
        DefaultModel = "phi4-mini"
    };
}

/// <summary>
/// Configuration spécifique à Microsoft Foundry Local.
/// </summary>
public sealed record FoundryLocalConfig
{
    /// <summary>
    /// Alias du modèle dans le catalogue Foundry Local.
    /// </summary>
    public string ModelAlias { get; init; } = "phi-4-mini";

    /// <summary>
    /// Répertoire de cache pour les modèles téléchargés.
    /// Null = utilise le défaut Foundry Local.
    /// </summary>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// Durée de vie du service (TTL) avant arrêt automatique.
    /// </summary>
    public TimeSpan? ServiceTtl { get; init; }

    /// <summary>
    /// Télécharger automatiquement les modèles manquants.
    /// </summary>
    public bool AutoDownload { get; init; } = true;
}

/// <summary>
/// Configuration spécifique à Azure AI Foundry (cloud).
/// L'authentification est Entra ID (aucune clé) ; le chat passe par l'endpoint OpenAI-compatible
/// <c>/openai/v1</c> de la ressource.
/// </summary>
public sealed record AzureFoundryConfig
{
    /// <summary>
    /// Endpoint de base du compte Azure AI Foundry / OpenAI
    /// (ex. https://mon-resource.cognitiveservices.azure.com/). La route <c>/openai/v1/</c> est
    /// ajoutée automatiquement. Null si le provider Azure n'est pas utilisé au démarrage.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Scope Entra ID demandé pour le jeton d'accès à l'endpoint.
    /// </summary>
    public string Scope { get; init; } = "https://ai.azure.com/.default";
}
