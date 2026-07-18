using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Llm.Lifecycle;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Stages;

namespace Narratum.Llm.Clients;

/// <summary>
/// Adaptateur qui connecte un IChatClient (Microsoft.Extensions.AI)
/// à l'interface ILlmClient de Narratum.
/// Supporte le routing par agent via les métadonnées de LlmRequest.
/// </summary>
public sealed class ChatClientLlmAdapter : ILlmClient, IStreamingLlmClient, IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly LlmClientConfig _config;
    private readonly ILogger _logger;

    // Ensures a requested model is actually loaded in the local provider before use.
    // Foundry Local rejects requests for models it hasn't loaded, so switching models
    // per request requires loading them on demand.
    private readonly ILlmLifecycleManager? _lifecycleManager;
    private readonly HashSet<string> _loadedModels = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Clé de métadonnée pour spécifier le modèle dans une LlmRequest.
    /// </summary>
    public const string ModelMetadataKey = "llm.model";

    /// <summary>
    /// Clé de métadonnée pour spécifier le type d'agent dans une LlmRequest.
    /// </summary>
    public const string AgentTypeMetadataKey = "llm.agentType";

    public string ClientName => _config.Provider switch
    {
        LlmProviderType.FoundryLocal => "FoundryLocalClient",
        LlmProviderType.Ollama => "OllamaClient",
        _ => "ChatClient"
    };

    public ChatClientLlmAdapter(
        IChatClient chatClient,
        LlmClientConfig config,
        ILogger<ChatClientLlmAdapter>? logger = null,
        ILlmLifecycleManager? lifecycleManager = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<ChatClientLlmAdapter>.Instance;
        _lifecycleManager = lifecycleManager;

        // The factory already loaded these at startup — don't reload them.
        if (!string.IsNullOrWhiteSpace(_config.FoundryLocal.ModelAlias))
            _loadedModels.Add(_config.FoundryLocal.ModelAlias);
        if (!string.IsNullOrWhiteSpace(_config.DefaultModel))
            _loadedModels.Add(_config.DefaultModel);
    }

    /// <summary>
    /// Ensures the given model is downloaded and loaded in the local provider before a
    /// request uses it. Cached per adapter so each model loads at most once.
    /// </summary>
    private async Task EnsureModelLoadedAsync(string modelName, CancellationToken cancellationToken)
    {
        if (_lifecycleManager is null || string.IsNullOrWhiteSpace(modelName))
            return;

        if (_loadedModels.Contains(modelName))
            return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loadedModels.Contains(modelName))
                return;

            _logger.LogInformation("Model {Model} not loaded yet — loading it in {Provider}...", modelName, _config.Provider);
            await _lifecycleManager.EnsureModelAvailableAsync(modelName, cancellationToken);
            _loadedModels.Add(modelName);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var modelName = ResolveModelName(request);
        _logger.LogDebug("Generating with model {Model} via {Provider}", modelName, _config.Provider);

        try
        {
            await EnsureModelLoadedAsync(modelName, cancellationToken);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, request.SystemPrompt),
                new(ChatRole.User, request.UserPrompt)
            };

            var options = new ChatOptions
            {
                ModelId = modelName,
                Temperature = (float)request.Parameters.Temperature,
                MaxOutputTokens = request.Parameters.MaxTokens,
                TopP = (float)request.Parameters.TopP,
                StopSequences = request.Parameters.StopTokens.Count > 0
                    ? request.Parameters.StopTokens.ToList()
                    : null
            };

            var stopwatch = Stopwatch.StartNew();
            var chatResponse = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            stopwatch.Stop();

            var content = chatResponse.Text;
            if (string.IsNullOrEmpty(content))
            {
                return Result<LlmResponse>.Fail("LLM returned empty content");
            }

            var promptTokens = (int)(chatResponse.Usage?.InputTokenCount ?? 0);
            var completionTokens = (int)(chatResponse.Usage?.OutputTokenCount ?? 0);

            _logger.LogDebug("Generated {Tokens} tokens in {Duration}ms",
                completionTokens, stopwatch.ElapsedMilliseconds);

            var metadata = new Dictionary<string, object>
            {
                ["provider"] = _config.Provider.ToString()
            };

            if (chatResponse.ModelId is not null)
                metadata["model"] = chatResponse.ModelId;

            return Result<LlmResponse>.Ok(new LlmResponse(
                requestId: request.RequestId,
                content: content,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                generationDuration: stopwatch.Elapsed,
                metadata: metadata));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<LlmResponse>.Fail("Request cancelled");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("LLM request timed out after {Timeout}s", _config.TimeoutSeconds);
            return Result<LlmResponse>.Fail(
                $"LLM request timed out after {_config.TimeoutSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "LLM network error: {Message}", ex.Message);
            return Result<LlmResponse>.Fail($"Network error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "LLM invalid operation: {Message}", ex.Message);
            return Result<LlmResponse>.Fail($"Invalid operation: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "LLM JSON error: {Message}", ex.Message);
            return Result<LlmResponse>.Fail($"JSON parsing error: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "LLM invalid argument: {Message}", ex.Message);
            return Result<LlmResponse>.Fail($"Invalid argument: {ex.Message}");
        }
        // Let critical exceptions (OutOfMemoryException) propagate
    }

    /// <summary>
    /// Streams the response incrementally, yielding text fragments as the model produces them.
    /// Exceptions (network, timeout, cancellation) propagate to the consumer.
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var modelName = ResolveModelName(request);
        _logger.LogDebug("Streaming with model {Model} via {Provider}", modelName, _config.Provider);

        await EnsureModelLoadedAsync(modelName, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, request.SystemPrompt),
            new(ChatRole.User, request.UserPrompt)
        };

        var options = new ChatOptions
        {
            ModelId = modelName,
            Temperature = (float)request.Parameters.Temperature,
            MaxOutputTokens = request.Parameters.MaxTokens,
            TopP = (float)request.Parameters.TopP,
            StopSequences = request.Parameters.StopTokens.Count > 0
                ? request.Parameters.StopTokens.ToList()
                : null
        };

        await foreach (var update in _chatClient
            .GetStreamingResponseAsync(messages, options, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, "ping")
            };
            var options = new ChatOptions { MaxOutputTokens = 1 };
            var response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            return !string.IsNullOrEmpty(response.Text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User cancellation - still unhealthy
            return false;
        }
        catch (HttpRequestException)
        {
            // Network error - unhealthy
            return false;
        }
        catch (InvalidOperationException)
        {
            // Client not properly initialized - unhealthy
            return false;
        }
        catch (TimeoutException)
        {
            // Timeout - unhealthy
            return false;
        }
        // Let critical exceptions propagate - they indicate serious issues
    }

    /// <summary>
    /// Résout le nom du modèle à utiliser pour une requête donnée.
    /// Priorité : métadonnée "llm.model" > routing par AgentType > DefaultModel.
    /// </summary>
    private string ResolveModelName(LlmRequest request)
    {
        if (request.Metadata.TryGetValue(ModelMetadataKey, out var explicitModel)
            && explicitModel is string modelStr
            && !string.IsNullOrEmpty(modelStr))
        {
            return modelStr;
        }

        if (request.Metadata.TryGetValue(AgentTypeMetadataKey, out var agentTypeObj)
            && agentTypeObj is AgentType agentType)
        {
            return _config.ResolveModel(agentType);
        }

        return _config.DefaultModel;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadLock.Dispose();
    }
}
