using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Narratum.Llm.Azure;
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

        // Singleton on purpose: ILlmClient is scoped, so a per-client cache would forget everything
        // at each Blazor circuit and pay the rejection round trip again.
        services.TryAddSingleton<ModelParameterCapabilities>();
        services.TryAddSingleton<ILlmClientFactory, LlmClientFactory>();

        // Entra ID credential for Azure AI Foundry. ManagedIdentity is excluded because on
        // machines with the Azure Arc agent it fails hard instead of falling through to az login.
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential(
            new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true }));

        // Azure discovery (subscriptions + deployments) for the model picker / subscription switcher.
        services.TryAddSingleton<IAzureFoundryDirectory>(sp => new AzureFoundryDirectory(
            sp.GetRequiredService<TokenCredential>(),
            sp.GetService<ILogger<AzureFoundryDirectory>>()));

        // Cloud image generation (Azure AI Foundry, Entra ID).
        services.TryAddSingleton<IImageGenerator>(sp => new AzureImageGenerator(
            sp.GetRequiredService<TokenCredential>(),
            timeoutSeconds: config.TimeoutSeconds,
            logger: sp.GetService<ILogger<AzureImageGenerator>>()));

        // ILlmClient is the routing client: it dispatches each request to the local provider or to
        // Azure (per the model id). Scoped + lazy so Foundry Local init never blocks startup and the
        // DI container owns disposal.
        services.TryAddScoped<ILlmClient>(sp =>
        {
#pragma warning disable IDISP005 // Disposal is handled by the DI container for scoped registrations
            var local = new LazyLlmClient(sp.GetRequiredService<ILlmClientFactory>());
            return new RoutingLlmClient(
                local,
                sp.GetRequiredService<LlmClientConfig>(),
                sp.GetRequiredService<TokenCredential>(),
                sp.GetService<ILoggerFactory>(),
                sp.GetRequiredService<ModelParameterCapabilities>());
#pragma warning restore IDISP005
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
