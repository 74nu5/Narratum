using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Azure.Core;

using Microsoft.Extensions.Logging;

using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Clients;

/// <summary>
/// Route chaque requête vers le fournisseur local (Foundry Local) ou vers Azure AI Foundry (cloud),
/// selon le modèle demandé. Un modèle Azure est encodé dans <c>llm.model</c> sous la forme
/// <c>azure:{endpoint}::{deployment}</c>, ce qui lui permet de traverser toute la plomberie
/// existante sans changement — le routeur intercepte, résout le client Azure de l'endpoint
/// (créé et mis en cache une fois par endpoint) et réécrit le modèle en nom de déploiement nu.
/// </summary>
internal sealed class RoutingLlmClient : ILlmClient, IStreamingLlmClient, IModelCatalogProvider, IAsyncDisposable, IDisposable
{
    /// <summary>Préfixe marquant un modèle servi par Azure AI Foundry dans <c>llm.model</c>.</summary>
    public const string AzurePrefix = "azure:";

    private const string EndpointDeploymentSeparator = "::";

    private readonly ILlmClient _local;
    private readonly LlmClientConfig _config;
    private readonly TokenCredential? _credential;
    private readonly ILoggerFactory? _loggerFactory;

    private readonly ConcurrentDictionary<string, Lazy<Task<ILlmClient>>> _azureClients =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<LlmClientFactory> _azureFactories = [];
    private bool _disposed;

    public RoutingLlmClient(
        ILlmClient local,
        LlmClientConfig config,
        TokenCredential? credential,
        ILoggerFactory? loggerFactory)
    {
        this._local = local ?? throw new ArgumentNullException(nameof(local));
        this._config = config ?? throw new ArgumentNullException(nameof(config));
        this._credential = credential;
        this._loggerFactory = loggerFactory;
    }

    public string ClientName => "Routing(local+azure)";

    /// <summary>Vrai si l'identifiant de modèle désigne un déploiement Azure.</summary>
    public static bool IsAzureModel(string? model)
        => model is not null && model.StartsWith(AzurePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Construit l'identifiant composite d'un déploiement Azure pour l'UI et les métadonnées.</summary>
    public static string AzureModelId(string endpoint, string deployment)
        => $"{AzurePrefix}{endpoint}{EndpointDeploymentSeparator}{deployment}";

    private static (string Endpoint, string Deployment) ParseAzureModel(string model)
    {
        var body = model[AzurePrefix.Length..];
        var separatorIndex = body.IndexOf(EndpointDeploymentSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            throw new InvalidOperationException($"Malformed Azure model id: '{model}'");

        return (body[..separatorIndex], body[(separatorIndex + EndpointDeploymentSeparator.Length)..]);
    }

    public async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request, CancellationToken cancellationToken = default)
    {
        var (client, routed) = await this.ResolveAsync(request, cancellationToken);
        return await client.GenerateAsync(routed, cancellationToken);
    }

    public async Task<Result<T>> GenerateStructuredAsync<T>(
        LlmRequest request, CancellationToken cancellationToken = default)
    {
        var (client, routed) = await this.ResolveAsync(request, cancellationToken);
        return await client.GenerateStructuredAsync<T>(routed, cancellationToken);
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (client, routed) = await this.ResolveAsync(request, cancellationToken);

        if (client is IStreamingLlmClient streaming)
        {
            await foreach (var chunk in streaming.GenerateStreamingAsync(routed, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            var result = await client.GenerateAsync(routed, cancellationToken);
            if (result is Result<LlmResponse>.Success success)
                yield return success.Value.Content;
        }
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        => this._local.IsHealthyAsync(cancellationToken);

    public async Task<IReadOnlyList<LlmModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => this._local is IModelCatalogProvider provider
            ? await provider.GetModelsAsync(cancellationToken)
            : Array.Empty<LlmModelInfo>();

    /// <summary>Choisit le client cible et, pour Azure, réécrit <c>llm.model</c> en déploiement nu.</summary>
    private async Task<(ILlmClient Client, LlmRequest Request)> ResolveAsync(
        LlmRequest request, CancellationToken cancellationToken)
    {
        var model = request.Metadata.TryGetValue(ChatClientLlmAdapter.ModelMetadataKey, out var value)
            && value is string s
                ? s
                : null;

        if (!IsAzureModel(model))
            return (this._local, request);

        var (endpoint, deployment) = ParseAzureModel(model!);
        var client = await this.GetAzureClientAsync(endpoint, cancellationToken);

        var metadata = new Dictionary<string, object>(request.Metadata)
        {
            [ChatClientLlmAdapter.ModelMetadataKey] = deployment,
        };
        var routed = new LlmRequest(request.SystemPrompt, request.UserPrompt, request.Parameters, metadata);
        return (client, routed);
    }

    private Task<ILlmClient> GetAzureClientAsync(string endpoint, CancellationToken cancellationToken)
    {
        var lazy = this._azureClients.GetOrAdd(
            endpoint,
            ep => new Lazy<Task<ILlmClient>>(() => this.CreateAzureClientAsync(ep, cancellationToken)));
        return lazy.Value;
    }

    private async Task<ILlmClient> CreateAzureClientAsync(string endpoint, CancellationToken cancellationToken)
    {
        // One Azure client per account endpoint; the per-request llm.model overrides the deployment.
        var azureConfig = this._config with
        {
            Provider = LlmProviderType.AzureFoundry,
            AzureFoundry = this._config.AzureFoundry with { Endpoint = endpoint },
        };

        var factory = new LlmClientFactory(azureConfig, this._loggerFactory, this._credential);
        this._azureFactories.Add(factory);
        return await factory.CreateClientAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        (this._local as IDisposable)?.Dispose();

        foreach (var factory in this._azureFactories)
            await factory.DisposeAsync();
    }

    /// <summary>
    /// Synchronous disposal, for containers disposed synchronously (Blazor Server uses async
    /// disposal in practice). The Azure factories hold no Foundry lifecycle, so their DisposeAsync
    /// completes synchronously — no blocking.
    /// </summary>
    public void Dispose()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        (this._local as IDisposable)?.Dispose();

        foreach (var factory in this._azureFactories)
            factory.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
