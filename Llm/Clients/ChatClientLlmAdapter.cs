using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Stages;

namespace Narratum.Llm.Clients;

/// <summary>
/// Adaptateur qui connecte un IChatClient (Microsoft.Extensions.AI)
/// à l'interface ILlmClient de Narratum.
/// Supporte le routing par agent via les métadonnées de LlmRequest.
/// </summary>
public sealed class ChatClientLlmAdapter : ILlmClient
{
    private readonly IChatClient _chatClient;
    private readonly LlmClientConfig _config;
    private readonly ILogger _logger;

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

    public bool IsMock => false;

    public ChatClientLlmAdapter(
        IChatClient chatClient,
        LlmClientConfig config,
        ILogger<ChatClientLlmAdapter>? logger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<ChatClientLlmAdapter>.Instance;
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
                ["isMock"] = false,
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
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "LLM error: {Message}", ex.Message);
            return Result<LlmResponse>.Fail($"LLM error: {ex.Message}");
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
        catch
        {
            return false;
        }
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
}
