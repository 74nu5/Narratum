namespace Narratum.Llm.Configuration;

/// <summary>
/// Type de fournisseur LLM local.
/// </summary>
public enum LlmProviderType
{
    /// <summary>
    /// Microsoft Foundry Local (DirectML, gestion de lifecycle intégrée).
    /// </summary>
    FoundryLocal,

    /// <summary>
    /// Ollama (service externe, API OpenAI-compatible).
    /// </summary>
    Ollama
}
