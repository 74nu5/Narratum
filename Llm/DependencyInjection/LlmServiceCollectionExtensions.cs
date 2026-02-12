using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Narratum.Llm.Clients;
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
        
        // Register ILlmClient as SCOPED to avoid blocking DI startup with async init
        // Foundry Local initialization is LAZY and happens on first usage
        services.TryAddScoped<ILlmClient>(sp =>
        {
            var factory = sp.GetRequiredService<ILlmClientFactory>();
            // LAZY: CreateClientAsync will be called on first use
            // DO NOT call .GetAwaiter().GetResult() here - it blocks app startup!
            return new LazyLlmClient(factory);
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
