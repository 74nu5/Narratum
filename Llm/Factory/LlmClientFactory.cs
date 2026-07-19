using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Llm.Clients;
using Narratum.Llm.Configuration;
using Narratum.Llm.Lifecycle;
using Narratum.Orchestration.Llm;
using OllamaSharp;
using OpenAI;

namespace Narratum.Llm.Factory;

/// <summary>
/// Factory créant un ChatClientLlmAdapter configuré pour le fournisseur choisi.
/// Utilise IChatClient via le SDK OpenAI (Foundry Local) ou OllamaSharp (Ollama).
/// </summary>
public sealed class LlmClientFactory : ILlmClientFactory, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ModelParameterCapabilities _capabilities;
    private ILlmLifecycleManager? _lifecycleManager;
    private IChatClient? _chatClient;
    private TokenCredential? _azureCredential;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LlmClientConfig Config { get; }

    public LlmClientFactory(
        LlmClientConfig config,
        ILoggerFactory? loggerFactory = null,
        TokenCredential? azureCredential = null,
        ModelParameterCapabilities? capabilities = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _azureCredential = azureCredential;
        _capabilities = capabilities ?? new ModelParameterCapabilities();
    }

    public async Task<ILlmClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        // The chat client and the Foundry lifecycle manager both wrap a single, process-wide
        // local service (FoundryLocalManager is created at most once per process), so they are
        // created once and shared. Each caller gets its own thin adapter — cheap, and safe for a
        // scoped consumer (LazyLlmClient) to dispose without touching the shared resources.
        var chatClient = await GetOrCreateChatClientAsync(cancellationToken);

        return new ChatClientLlmAdapter(
            chatClient,
            Config,
            _loggerFactory.CreateLogger<ChatClientLlmAdapter>(),
            _lifecycleManager,
            _capabilities);
    }

    private async Task<IChatClient> GetOrCreateChatClientAsync(CancellationToken cancellationToken)
    {
        if (_chatClient is not null)
            return _chatClient;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_chatClient is not null)
                return _chatClient;

            _chatClient = Config.Provider switch
            {
                LlmProviderType.FoundryLocal => await CreateFoundryLocalClientAsync(cancellationToken),
                LlmProviderType.Ollama => CreateOllamaClient(),
                LlmProviderType.AzureFoundry => CreateAzureFoundryClient(),
                _ => throw new InvalidOperationException($"Unknown provider: {Config.Provider}")
            };

            return _chatClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IChatClient> CreateFoundryLocalClientAsync(CancellationToken cancellationToken)
    {
        _lifecycleManager = new FoundryLocalLifecycleManager(
            Config.FoundryLocal,
            _loggerFactory.CreateLogger<FoundryLocalLifecycleManager>());

        var baseUrl = await _lifecycleManager.GetBaseUrlAsync(cancellationToken);

        // No startup model load: the adapter loads whichever model each request needs,
        // on demand. This avoids always loading the default model even when the user
        // picks a different one.

        // OpenAI SDK pointe vers le endpoint local Foundry.
        // Le NetworkTimeout par défaut (100 s) est trop court pour de la génération locale
        // avec un gros modèle ; on l'aligne sur la config. Sur localhost, insister avec des
        // retries sur un timeout ne sert à rien — on les réduit.
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential("notneeded"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(baseUrl),
                NetworkTimeout = TimeSpan.FromSeconds(Config.TimeoutSeconds),
                RetryPolicy = new ClientRetryPolicy(maxRetries: 1)
            });

        return openAiClient
            .GetChatClient(Config.DefaultModel)
            .AsIChatClient();
    }

    private IChatClient CreateOllamaClient()
    {
        var baseUrl = Config.BaseUrl ?? "http://localhost:11434";
        return new OllamaApiClient(new Uri(baseUrl), Config.DefaultModel);
    }

    /// <summary>
    /// Azure AI Foundry (cloud) via the OpenAI-compatible <c>/openai/v1</c> endpoint, authenticated
    /// with Entra ID (bearer token, no key). Integrates like any other OpenAI chat client.
    /// </summary>
    private IChatClient CreateAzureFoundryClient()
    {
        var endpoint = Config.AzureFoundry.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException(
                "AzureFoundry.Endpoint is required for the AzureFoundry provider.");

        var v1Endpoint = new Uri($"{endpoint.TrimEnd('/')}/openai/v1/");

        // On machines with the Azure Arc agent, ManagedIdentity fails hard, so exclude it —
        // DefaultAzureCredential then falls through to the developer's az login session.
        _azureCredential ??= new DefaultAzureCredential(
            new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true });

#pragma warning disable OPENAI001 // BearerTokenPolicy auth on OpenAIClient is an evaluation API.
        var tokenPolicy = new BearerTokenPolicy(_azureCredential, Config.AzureFoundry.Scope);
        var openAiClient = new OpenAIClient(
            authenticationPolicy: tokenPolicy,
            options: new OpenAIClientOptions
            {
                Endpoint = v1Endpoint,
                NetworkTimeout = TimeSpan.FromSeconds(Config.TimeoutSeconds)
            });
#pragma warning restore OPENAI001

        return openAiClient
            .GetChatClient(Config.DefaultModel)
            .AsIChatClient();
    }

    public async ValueTask DisposeAsync()
    {
        (_chatClient as IDisposable)?.Dispose();

        if (_lifecycleManager is not null)
        {
            await _lifecycleManager.DisposeAsync();
        }

        _initLock.Dispose();
    }
}
