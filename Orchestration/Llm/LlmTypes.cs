using Narratum.Core;

namespace Narratum.Orchestration.Llm;

/// <summary>
/// Paramètres de génération pour le LLM.
/// </summary>
public sealed record LlmParameters
{
    /// <summary>
    /// Température de génération (0.0 = déterministe, 1.0 = créatif).
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Nombre maximum de tokens à générer.
    /// </summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// Top-p (nucleus sampling).
    /// </summary>
    public double TopP { get; init; } = 0.9;

    /// <summary>
    /// Tokens d'arrêt.
    /// </summary>
    public IReadOnlyList<string> StopTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Paramètres par défaut.
    /// </summary>
    public static LlmParameters Default => new();

    /// <summary>
    /// Paramètres déterministes (température = 0).
    /// </summary>
    public static LlmParameters Deterministic => new() { Temperature = 0.0 };

    /// <summary>
    /// Paramètres créatifs (température élevée).
    /// </summary>
    public static LlmParameters Creative => new() { Temperature = 0.9, TopP = 0.95 };
}

/// <summary>
/// Requête vers le LLM.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>
    /// Identifiant unique de la requête.
    /// </summary>
    public Id RequestId { get; }

    /// <summary>
    /// Prompt système (instructions générales).
    /// </summary>
    public string SystemPrompt { get; }

    /// <summary>
    /// Prompt utilisateur (contenu spécifique).
    /// </summary>
    public string UserPrompt { get; }

    /// <summary>
    /// Paramètres de génération.
    /// </summary>
    public LlmParameters Parameters { get; }

    /// <summary>
    /// Métadonnées de la requête.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Timestamp de création.
    /// </summary>
    public DateTime CreatedAt { get; }

    public LlmRequest(
        string systemPrompt,
        string userPrompt,
        LlmParameters? parameters = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        RequestId = Id.New();
        SystemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        UserPrompt = userPrompt ?? throw new ArgumentNullException(nameof(userPrompt));
        Parameters = parameters ?? LlmParameters.Default;
        Metadata = metadata ?? new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Crée une requête simple.
    /// </summary>
    public static LlmRequest Simple(string prompt)
        => new("You are a helpful assistant.", prompt);
}

/// <summary>
/// Réponse du LLM.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Identifiant unique de la réponse.
    /// </summary>
    public Id ResponseId { get; }

    /// <summary>
    /// Identifiant de la requête correspondante.
    /// </summary>
    public Id RequestId { get; }

    /// <summary>
    /// Contenu textuel généré.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Nombre de tokens dans le prompt.
    /// </summary>
    public int PromptTokens { get; }

    /// <summary>
    /// Nombre de tokens générés.
    /// </summary>
    public int CompletionTokens { get; }

    /// <summary>
    /// Durée de génération.
    /// </summary>
    public TimeSpan GenerationDuration { get; }

    /// <summary>
    /// Métadonnées de la réponse.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Timestamp de création.
    /// </summary>
    public DateTime CreatedAt { get; }

    public LlmResponse(
        Id requestId,
        string content,
        int promptTokens = 0,
        int completionTokens = 0,
        TimeSpan? generationDuration = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ResponseId = Id.New();
        RequestId = requestId;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        GenerationDuration = generationDuration ?? TimeSpan.Zero;
        Metadata = metadata ?? new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Nombre total de tokens utilisés.
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
