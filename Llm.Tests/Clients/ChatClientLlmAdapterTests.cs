using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Narratum.Core;
using Narratum.Llm.Clients;
using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Llm.Tests.Clients;

/// <summary>
/// Tests unitaires pour ChatClientLlmAdapter.
/// Vérifie la conversion LlmRequest → ChatMessage/ChatOptions → LlmResponse,
/// le routing de modèle, et la gestion d'erreurs.
/// </summary>
public sealed class ChatClientLlmAdapterTests
{
    private static LlmClientConfig DefaultConfig => new()
    {
        Provider = LlmProviderType.FoundryLocal,
        DefaultModel = "phi-4-mini"
    };

    private static ChatClientLlmAdapter CreateAdapter(
        IChatClient chatClient,
        LlmClientConfig? config = null)
    {
        return new ChatClientLlmAdapter(chatClient, config ?? DefaultConfig);
    }

    private static IChatClient CreateMockChatClient(
        string responseText = "Generated text",
        string? modelId = null,
        int inputTokens = 10,
        int outputTokens = 20)
    {
        var chatClient = Substitute.For<IChatClient>();

        var chatResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, responseText))
        {
            ModelId = modelId,
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens
            }
        };

        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        return chatClient;
    }

    private static LlmResponse AssertSuccess(Result<LlmResponse> result)
    {
        result.Should().BeOfType<Result<LlmResponse>.Success>();
        return ((Result<LlmResponse>.Success)result).Value;
    }

    private static string AssertFailure(Result<LlmResponse> result)
    {
        result.Should().BeOfType<Result<LlmResponse>.Failure>();
        return ((Result<LlmResponse>.Failure)result).Message;
    }

    #region ClientName et IsMock

    [Fact]
    public void ClientName_FoundryLocal_ReturnsFoundryLocalClient()
    {
        var config = new LlmClientConfig { Provider = LlmProviderType.FoundryLocal };
        var adapter = CreateAdapter(Substitute.For<IChatClient>(), config);

        adapter.ClientName.Should().Be("FoundryLocalClient");
    }

    [Fact]
    public void ClientName_Ollama_ReturnsOllamaClient()
    {
        var config = new LlmClientConfig { Provider = LlmProviderType.Ollama };
        var adapter = CreateAdapter(Substitute.For<IChatClient>(), config);

        adapter.ClientName.Should().Be("OllamaClient");
    }

    [Fact]
    public void IsMock_ReturnsFalse()
    {
        var adapter = CreateAdapter(Substitute.For<IChatClient>());
        adapter.IsMock.Should().BeFalse();
    }

    #endregion

    #region GenerateAsync — Succès

    [Fact]
    public async Task GenerateAsync_ValidRequest_ReturnsSuccessResult()
    {
        var chatClient = CreateMockChatClient("Hello world");
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System prompt", "User prompt");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task GenerateAsync_SetsCorrectRequestId()
    {
        var chatClient = CreateMockChatClient();
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.RequestId.Should().Be(request.RequestId);
    }

    [Fact]
    public async Task GenerateAsync_SetsTokenCounts()
    {
        var chatClient = CreateMockChatClient(inputTokens: 42, outputTokens: 100);
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.PromptTokens.Should().Be(42);
        response.CompletionTokens.Should().Be(100);
        response.TotalTokens.Should().Be(142);
    }

    [Fact]
    public async Task GenerateAsync_SetsMetadata()
    {
        var chatClient = CreateMockChatClient(modelId: "test-model");
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.Metadata.Should().ContainKey("isMock");
        response.Metadata["isMock"].Should().Be(false);
        response.Metadata.Should().ContainKey("provider");
        response.Metadata["provider"].Should().Be("FoundryLocal");
        response.Metadata.Should().ContainKey("model");
        response.Metadata["model"].Should().Be("test-model");
    }

    [Fact]
    public async Task GenerateAsync_WithNullModelId_OmitsModelFromMetadata()
    {
        var chatClient = CreateMockChatClient(modelId: null);
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.Metadata.Should().NotContainKey("model");
    }

    [Fact]
    public async Task GenerateAsync_SetsGenerationDuration()
    {
        var chatClient = CreateMockChatClient();
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.GenerationDuration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task GenerateAsync_SendsSystemAndUserMessages()
    {
        var chatClient = CreateMockChatClient();
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("My system prompt", "My user prompt");

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Count() == 2 &&
                msgs.First().Role == ChatRole.System &&
                msgs.First().Text == "My system prompt" &&
                msgs.Last().Role == ChatRole.User &&
                msgs.Last().Text == "My user prompt"),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_PassesParametersAsChatOptions()
    {
        var chatClient = CreateMockChatClient();
        var adapter = CreateAdapter(chatClient);
        var parameters = new LlmParameters
        {
            Temperature = 0.5,
            MaxTokens = 512,
            TopP = 0.8,
            StopTokens = new[] { "END", "STOP" }
        };
        var request = new LlmRequest("System", "User", parameters);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts =>
                opts != null &&
                opts.Temperature == 0.5f &&
                opts.MaxOutputTokens == 512 &&
                opts.TopP == 0.8f &&
                opts.StopSequences != null &&
                opts.StopSequences.Contains("END") &&
                opts.StopSequences.Contains("STOP")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_EmptyStopTokens_SetsNullStopSequences()
    {
        var chatClient = CreateMockChatClient();
        var adapter = CreateAdapter(chatClient);
        var parameters = new LlmParameters { StopTokens = Array.Empty<string>() };
        var request = new LlmRequest("System", "User", parameters);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts =>
                opts != null && opts.StopSequences == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_NullUsage_DefaultsToZeroTokens()
    {
        var chatClient = Substitute.For<IChatClient>();
        var chatResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "response"))
        {
            Usage = null
        };
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var response = AssertSuccess(result);
        response.PromptTokens.Should().Be(0);
        response.CompletionTokens.Should().Be(0);
    }

    #endregion

    #region GenerateAsync — Échecs

    [Fact]
    public async Task GenerateAsync_EmptyContent_ReturnsFailure()
    {
        var chatClient = CreateMockChatClient(responseText: "");
        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var error = AssertFailure(result);
        error.Should().Contain("empty");
    }

    [Fact]
    public async Task GenerateAsync_HttpRequestException_ReturnsFailure()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var error = AssertFailure(result);
        error.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task GenerateAsync_InvalidOperationException_ReturnsFailure()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Bad state"));

        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var error = AssertFailure(result);
        error.Should().Contain("Bad state");
    }

    [Fact]
    public async Task GenerateAsync_Cancelled_ReturnsFailure()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request, cts.Token);

        var error = AssertFailure(result);
        error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task GenerateAsync_Timeout_ReturnsFailureWithTimeoutMessage()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout", null, CancellationToken.None));

        var adapter = CreateAdapter(chatClient);
        var request = new LlmRequest("System", "User");

        var result = await adapter.GenerateAsync(request);

        var error = AssertFailure(result);
        error.Should().Contain("timed out");
    }

    [Fact]
    public async Task GenerateAsync_NullRequest_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter(Substitute.For<IChatClient>());

        var act = () => adapter.GenerateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Routing de modèle

    [Fact]
    public async Task GenerateAsync_DefaultModel_SetsModelIdInOptions()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig { DefaultModel = "my-default" };
        var adapter = CreateAdapter(chatClient, config);
        var request = new LlmRequest("System", "User");

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "my-default"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WithAgentTypeMetadata_UsesAgentSpecificModel()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "default",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Summary] = "summary-model"
            }
        };
        var adapter = CreateAdapter(chatClient, config);
        var metadata = new Dictionary<string, object>
        {
            [ChatClientLlmAdapter.AgentTypeMetadataKey] = AgentType.Summary
        };
        var request = new LlmRequest("System", "User", metadata: metadata);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "summary-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WithExplicitModelMetadata_OverridesAll()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "default",
            NarratorModel = "narrator",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Narrator] = "mapped"
            }
        };
        var adapter = CreateAdapter(chatClient, config);
        var metadata = new Dictionary<string, object>
        {
            [ChatClientLlmAdapter.ModelMetadataKey] = "explicit-model",
            [ChatClientLlmAdapter.AgentTypeMetadataKey] = AgentType.Narrator
        };
        var request = new LlmRequest("System", "User", metadata: metadata);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "explicit-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_NarratorModel_OverridesMapping()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "default",
            NarratorModel = "narrator-special",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Narrator] = "should-be-ignored"
            }
        };
        var adapter = CreateAdapter(chatClient, config);
        var metadata = new Dictionary<string, object>
        {
            [ChatClientLlmAdapter.AgentTypeMetadataKey] = AgentType.Narrator
        };
        var request = new LlmRequest("System", "User", metadata: metadata);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "narrator-special"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_UnmappedAgent_FallsToDefaultModel()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "fallback-model",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Summary] = "summary-only"
            }
        };
        var adapter = CreateAdapter(chatClient, config);
        var metadata = new Dictionary<string, object>
        {
            [ChatClientLlmAdapter.AgentTypeMetadataKey] = AgentType.Consistency
        };
        var request = new LlmRequest("System", "User", metadata: metadata);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "fallback-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_EmptyExplicitModel_FallsToAgentRouting()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "default",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Summary] = "summary-model"
            }
        };
        var adapter = CreateAdapter(chatClient, config);
        var metadata = new Dictionary<string, object>
        {
            [ChatClientLlmAdapter.ModelMetadataKey] = "",
            [ChatClientLlmAdapter.AgentTypeMetadataKey] = AgentType.Summary
        };
        var request = new LlmRequest("System", "User", metadata: metadata);

        await adapter.GenerateAsync(request);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == "summary-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_AllFourAgentTypes_RouteCorrectly()
    {
        var chatClient = CreateMockChatClient();
        var config = new LlmClientConfig
        {
            DefaultModel = "default",
            NarratorModel = "narrator-model",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Summary] = "summary-model",
                [AgentType.Character] = "character-model",
                [AgentType.Consistency] = "consistency-model"
            }
        };
        var adapter = CreateAdapter(chatClient, config);

        foreach (var (agentType, expectedModel) in new[]
        {
            (AgentType.Summary, "summary-model"),
            (AgentType.Narrator, "narrator-model"),
            (AgentType.Character, "character-model"),
            (AgentType.Consistency, "consistency-model")
        })
        {
            chatClient.ClearReceivedCalls();
            var metadata = new Dictionary<string, object>
            {
                [ChatClientLlmAdapter.AgentTypeMetadataKey] = agentType
            };
            var request = new LlmRequest("System", "User", metadata: metadata);

            await adapter.GenerateAsync(request);

            await chatClient.Received(1).GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Is<ChatOptions?>(opts => opts != null && opts.ModelId == expectedModel),
                Arg.Any<CancellationToken>());
        }
    }

    #endregion

    #region IsHealthyAsync

    [Fact]
    public async Task IsHealthyAsync_SuccessfulResponse_ReturnsTrue()
    {
        var chatClient = Substitute.For<IChatClient>();
        var chatResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "pong"));
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var adapter = CreateAdapter(chatClient);

        var healthy = await adapter.IsHealthyAsync();

        healthy.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_EmptyResponse_ReturnsFalse()
    {
        var chatClient = Substitute.For<IChatClient>();
        var chatResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, ""));
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var adapter = CreateAdapter(chatClient);

        var healthy = await adapter.IsHealthyAsync();

        healthy.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_Exception_ReturnsFalse()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No connection"));

        var adapter = CreateAdapter(chatClient);

        var healthy = await adapter.IsHealthyAsync();

        healthy.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_SendsPingWithMaxTokens1()
    {
        var chatClient = Substitute.For<IChatClient>();
        var chatResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "ok"));
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var adapter = CreateAdapter(chatClient);
        await adapter.IsHealthyAsync();

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Count() == 1 &&
                msgs.First().Text == "ping"),
            Arg.Is<ChatOptions?>(opts => opts != null && opts.MaxOutputTokens == 1),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Constructeur

    [Fact]
    public void Constructor_NullChatClient_ThrowsArgumentNullException()
    {
        var act = () => new ChatClientLlmAdapter(null!, DefaultConfig);

        act.Should().Throw<ArgumentNullException>().WithParameterName("chatClient");
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        var chatClient = Substitute.For<IChatClient>();
        var act = () => new ChatClientLlmAdapter(chatClient, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    #endregion

    #region Constantes de métadonnées

    [Fact]
    public void ModelMetadataKey_HasExpectedValue()
    {
        ChatClientLlmAdapter.ModelMetadataKey.Should().Be("llm.model");
    }

    [Fact]
    public void AgentTypeMetadataKey_HasExpectedValue()
    {
        ChatClientLlmAdapter.AgentTypeMetadataKey.Should().Be("llm.agentType");
    }

    #endregion
}
