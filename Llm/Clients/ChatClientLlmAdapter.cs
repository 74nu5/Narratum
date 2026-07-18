using System.ClientModel;
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
public sealed class ChatClientLlmAdapter : ILlmClient, IStreamingLlmClient, IModelCatalogProvider, IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly LlmClientConfig _config;
    private readonly ILogger _logger;

    // Ensures a requested model is loaded before use and maps it to the concrete id the
    // provider actually serves. Foundry Local rejects requests for models it hasn't loaded,
    // and its OpenAI endpoint serves by concrete id (e.g. "Phi-4-mini-...-cpu:5"), not alias.
    private readonly ILlmLifecycleManager? _lifecycleManager;
    private readonly Dictionary<string, string> _servedIdCache = new(StringComparer.OrdinalIgnoreCase);
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
    }

    /// <summary>
    /// Ensures the requested model is loaded and returns the concrete id the provider
    /// serves it under (which is what the request must send). Cached per adapter so each
    /// model is resolved/loaded at most once. Without a lifecycle manager (e.g. Ollama),
    /// the requested name is returned unchanged.
    /// </summary>
    private async Task<string> ResolveServedModelIdAsync(string requestedModel, CancellationToken cancellationToken)
    {
        if (_lifecycleManager is null || string.IsNullOrWhiteSpace(requestedModel))
            return requestedModel;

        if (_servedIdCache.TryGetValue(requestedModel, out var cached))
            return cached;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_servedIdCache.TryGetValue(requestedModel, out cached))
                return cached;

            var servedId = await _lifecycleManager.EnsureModelAvailableAsync(requestedModel, cancellationToken);
            _servedIdCache[requestedModel] = servedId;
            return servedId;
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
            var servedModelId = await ResolveServedModelIdAsync(modelName, cancellationToken);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, request.SystemPrompt),
                new(ChatRole.User, request.UserPrompt)
            };

            var stopwatch = Stopwatch.StartNew();
            var chatResponse = await GetResponseWithParamFallbackAsync(messages, servedModelId, request, cancellationToken);
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
    /// Builds the chat options. The <paramref name="full"/> set carries our tuning (temperature,
    /// top-p, max tokens, stop). The minimal set is just the model id, for the retry against models
    /// that reject extra parameters — GPT-5 reasoning models reject a custom temperature, some
    /// Mistral deployments reject max_completion_tokens — so only the model id is universally safe.
    /// </summary>
    private static ChatOptions BuildChatOptions(string servedModelId, LlmRequest request, bool full)
    {
        if (!full)
            return new ChatOptions { ModelId = servedModelId };

        return new ChatOptions
        {
            ModelId = servedModelId,
            Temperature = (float)request.Parameters.Temperature,
            MaxOutputTokens = request.Parameters.MaxTokens,
            TopP = (float)request.Parameters.TopP,
            StopSequences = request.Parameters.StopTokens.Count > 0
                ? request.Parameters.StopTokens.ToList()
                : null
        };
    }

    /// <summary>True for HTTP 400/422 — the model rejected a request parameter.</summary>
    private static bool IsUnsupportedParameterError(Exception ex)
        => ex is ClientResultException { Status: 400 or 422 };

    /// <summary>
    /// Calls the model with sampling parameters; if it rejects them (400/422), retries once with the
    /// model's defaults so picky cloud models still work.
    /// </summary>
    private async Task<ChatResponse> GetResponseWithParamFallbackAsync(
        List<ChatMessage> messages, string servedModelId, LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _chatClient.GetResponseAsync(
                messages, BuildChatOptions(servedModelId, request, full: true), cancellationToken);
        }
        catch (Exception ex) when (IsUnsupportedParameterError(ex))
        {
            _logger.LogInformation(
                "Model {Model} rejected sampling parameters ({Message}); retrying with model defaults",
                servedModelId, ex.Message);
            return await _chatClient.GetResponseAsync(
                messages, BuildChatOptions(servedModelId, request, full: false), cancellationToken);
        }
    }

    /// <summary>
    /// Structured output with a native strict JSON schema (via <c>ChatOptions.ResponseFormat</c>,
    /// derived from <typeparamref name="T"/>). Small local models often ignore the schema, so on
    /// any failure we fall back to the shared tolerant prompt-based path.
    /// </summary>
    public async Task<Result<T>> GenerateStructuredAsync<T>(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var modelName = ResolveModelName(request);

        try
        {
            var servedModelId = await ResolveServedModelIdAsync(modelName, cancellationToken);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, request.SystemPrompt),
                new(ChatRole.User, request.UserPrompt)
            };

            var options = BuildChatOptions(servedModelId, request, full: true);

            try
            {
                var typed = await _chatClient.GetResponseAsync<T>(messages, options, cancellationToken: cancellationToken);
                if (typed.TryGetResult(out var value))
                    return Result<T>.Ok(value);

                // Native call succeeded but the payload wasn't parseable as T — try tolerant parse.
                if (StructuredLlm.TryDeserialize<T>(typed.Text, out var parsed))
                    return Result<T>.Ok(parsed!);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Native structured output failed; falling back to tolerant prompt path");
            }

            // Fallback: schema-in-prompt + tolerant parse + retry, over this adapter's GenerateAsync.
            return await StructuredLlm.GenerateViaPromptAsync<T>(this, request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Fail("Request cancelled");
        }
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
        _logger.LogInformation("Streaming with model {Model} via {Provider}", modelName, _config.Provider);

        var servedModelId = await ResolveServedModelIdAsync(modelName, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, request.SystemPrompt),
            new(ChatRole.User, request.UserPrompt)
        };

        await foreach (var chunk in StreamWithParamFallbackAsync(messages, servedModelId, request, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Streams with sampling parameters; if the model rejects them on the first chunk (400/422),
    /// re-streams once with the model's defaults. Only a finally (never a catch) wraps a yield, so
    /// the first MoveNext is driven manually to catch the parameter rejection.
    /// </summary>
    private async IAsyncEnumerable<string> StreamWithParamFallbackAsync(
        List<ChatMessage> messages, string servedModelId, LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = _chatClient
            .GetStreamingResponseAsync(messages, BuildChatOptions(servedModelId, request, full: true), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var first = true;
        var retryWithoutSampling = false;
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (Exception ex) when (first && IsUnsupportedParameterError(ex))
                {
                    _logger.LogInformation(
                        "Model {Model} rejected sampling parameters while streaming; retrying with model defaults",
                        servedModelId);
                    retryWithoutSampling = true;
                    break;
                }

                if (!moved)
                    yield break;

                first = false;
                if (!string.IsNullOrEmpty(enumerator.Current.Text))
                    yield return enumerator.Current.Text;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (!retryWithoutSampling)
            yield break;

        await foreach (var update in _chatClient
            .GetStreamingResponseAsync(messages, BuildChatOptions(servedModelId, request, full: false), cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        // For a local provider, "healthy" means the service endpoint is reachable — no
        // specific model needs to be loaded. (IsRunningAsync handles its own errors.)
        if (_lifecycleManager is not null)
            return await _lifecycleManager.IsRunningAsync(cancellationToken);

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

    public async Task<IReadOnlyList<LlmModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_lifecycleManager is null)
            return Array.Empty<LlmModelInfo>();

        return await _lifecycleManager.ListModelsAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadLock.Dispose();
    }
}
