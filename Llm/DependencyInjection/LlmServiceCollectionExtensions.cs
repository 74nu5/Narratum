using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Narratum.Llm.Configuration;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.DependencyInjection;

/// <summary>
/// Extensions pour enregistrer les services LLM dans le conteneur DI.
/// </summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Ajoute les services LLM Narratum avec une configuration explicite.
    /// </summary>
    public static IServiceCollection AddNarratumLlm(
        this IServiceCollection services,
        LlmClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.AddSingleton(config);
        services.TryAddSingleton<ILlmClientFactory, LlmClientFactory>();
        
        // Register ILlmClient as a factory-created singleton
        services.TryAddSingleton<ILlmClient>(sp =>
        {
            var factory = sp.GetRequiredService<ILlmClientFactory>();
            // Note: CreateClientAsync is async, but DI requires sync factory
            // We need to use a workaround or change the design
            return factory.CreateClientAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Ajoute les services LLM Narratum configurés pour Foundry Local.
    /// </summary>
    public static IServiceCollection AddNarratumFoundryLocal(
        this IServiceCollection services,
        string defaultModel = "phi-4-mini",
        string? narratorModel = null)
    {
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = defaultModel,
            NarratorModel = narratorModel,
            FoundryLocal = new FoundryLocalConfig
            {
                ModelAlias = defaultModel
            }
        };

        return AddNarratumLlm(services, config);
    }

    /// <summary>
    /// Ajoute les services LLM Narratum configurés pour Ollama.
    /// </summary>
    public static IServiceCollection AddNarratumOllama(
        this IServiceCollection services,
        string baseUrl = "http://localhost:11434",
        string defaultModel = "phi4-mini",
        string? narratorModel = null)
    {
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.Ollama,
            BaseUrl = baseUrl,
            DefaultModel = defaultModel,
            NarratorModel = narratorModel
        };

        return AddNarratumLlm(services, config);
    }
}
