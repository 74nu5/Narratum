using System.ClientModel;
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
    private ILlmLifecycleManager? _lifecycleManager;
    private IDisposable? _chatClientDisposable;

    public LlmClientConfig Config { get; }

    public LlmClientFactory(
        LlmClientConfig config,
        ILoggerFactory? loggerFactory = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async Task<ILlmClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        IChatClient chatClient;

        switch (Config.Provider)
        {
            case LlmProviderType.FoundryLocal:
                chatClient = await CreateFoundryLocalClientAsync(cancellationToken);
                break;

            case LlmProviderType.Ollama:
                chatClient = CreateOllamaClient();
                break;

            default:
                throw new InvalidOperationException($"Unknown provider: {Config.Provider}");
        }

        // Disposer l'ancien client avant réassignation
        _chatClientDisposable?.Dispose();
        _chatClientDisposable = chatClient as IDisposable;

        return new ChatClientLlmAdapter(
            chatClient,
            Config,
            _loggerFactory.CreateLogger<ChatClientLlmAdapter>());
    }

    private async Task<IChatClient> CreateFoundryLocalClientAsync(CancellationToken cancellationToken)
    {
        _lifecycleManager = new FoundryLocalLifecycleManager(
            Config.FoundryLocal,
            _loggerFactory.CreateLogger<FoundryLocalLifecycleManager>());

        var baseUrl = await _lifecycleManager.GetBaseUrlAsync(cancellationToken);
        await _lifecycleManager.EnsureModelAvailableAsync(
            Config.FoundryLocal.ModelAlias, cancellationToken);

        // OpenAI SDK pointe vers le endpoint local Foundry
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential("notneeded"),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        return openAiClient
            .GetChatClient(Config.DefaultModel)
            .AsIChatClient();
    }

    private IChatClient CreateOllamaClient()
    {
        var baseUrl = Config.BaseUrl ?? "http://localhost:11434";
        return new OllamaApiClient(new Uri(baseUrl), Config.DefaultModel);
    }

    public async ValueTask DisposeAsync()
    {
        _chatClientDisposable?.Dispose();

        if (_lifecycleManager is not null)
        {
            await _lifecycleManager.DisposeAsync();
        }
    }
}
