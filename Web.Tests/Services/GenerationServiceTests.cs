using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Services;
using Narratum.State;
using Narratum.Web.Models;
using Narratum.Web.Services;
using Xunit;

namespace Narratum.Web.Tests.Services;

public class GenerationServiceTests
{
    private readonly Mock<IStoryRepository> _mockRepository;
    private readonly GenerationService _service;

    public GenerationServiceTests()
    {
        _mockRepository = new Mock<IStoryRepository>();

        // FullOrchestrationService is sealed, so it cannot be mocked.
        // Build a real instance backed by a mocked ILlmClient interface;
        // the validation-focused tests below never reach the LLM anyway.
        var llmClient = new Mock<ILlmClient>().Object;
        var orchestrator = new FullOrchestrationService(llmClient);

        var modelSelector = new ModelSelectionService(new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini",
            NarratorModel = "phi-4-mini"
        });

        _service = new GenerationService(
            orchestrator,
            _mockRepository.Object,
            modelSelector,
            new Mock<ILlmClient>().Object,
            NullLogger<GenerationService>.Instance);
    }

    private static StoryCreationRequest CreateRequest(
        string worldName = "Test World",
        string genreStyle = "Fantasy",
        string? narrativeStyle = "Epic",
        params (string Name, string? Description)[] characters)
    {
        var chars = characters.Length > 0
            ? characters.ToList()
            : new List<(string Name, string? Description)> { ("Hero", "A brave warrior") };

        return new StoryCreationRequest(
            WorldName: worldName,
            GenreStyle: genreStyle,
            Characters: chars,
            WorldDescription: "A magical realm",
            NarrativeStyle: narrativeStyle);
    }

    [Fact]
    public async Task CreateStoryAsync_WhenValidRequest_ReturnsSuccess()
    {
        // Arrange
        var slotName = "test-slot";
        var request = CreateRequest();

        _mockRepository
            .Setup(r => r.CreateStoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StoryState>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StoryMetadata>.Ok(new StoryMetadata(
                slotName,
                "Test World",
                "Fantasy",
                DateTime.UtcNow,
                1)));

        // Act
        var result = await _service.CreateStoryAsync(slotName, request);

        // Assert
        var success = result.Should().BeOfType<Result<string>.Success>().Subject;
        success.Value.Should().Be(slotName);

        _mockRepository.Verify(r => r.CreateStoryAsync(
            slotName,
            "Test World",
            "Fantasy",
            "Fantasy — Epic",
            It.IsAny<StoryState>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateStoryAsync_WhenEmptySlotName_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var result = await _service.CreateStoryAsync("", request);

        // Assert
        var failure = result.Should().BeOfType<Result<string>.Failure>().Subject;
        failure.Message.Should().Contain("slot");

        _mockRepository.Verify(r => r.CreateStoryAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StoryState>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStoryAsync_WhenNoCharacters_ReturnsFailure()
    {
        // Arrange
        var request = new StoryCreationRequest(
            WorldName: "Test",
            GenreStyle: "Fantasy",
            Characters: new List<(string Name, string? Description)>());

        // Act
        var result = await _service.CreateStoryAsync("test-slot", request);

        // Assert
        var failure = result.Should().BeOfType<Result<string>.Failure>().Subject;
        failure.Message.Should().Contain("personnage");
    }

    [Fact]
    public async Task GenerateNextPageAsync_WhenEmptyIntent_ReturnsFailure()
    {
        // Arrange
        var slotName = "test-slot";

        // Act
        var result = await _service.GenerateNextPageAsync(slotName, "");

        // Assert
        var failure = result.Should().BeOfType<Result<PageInfo>.Failure>().Subject;
        failure.Message.Should().Contain("intention");

        _mockRepository.Verify(r => r.LoadLatestPageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateNextPageAsync_WhenIntentTooLong_ReturnsFailure()
    {
        // Arrange
        var slotName = "test-slot";
        var longIntent = new string('x', 1001);

        // Act
        var result = await _service.GenerateNextPageAsync(slotName, longIntent);

        // Assert
        var failure = result.Should().BeOfType<Result<PageInfo>.Failure>().Subject;
        failure.Message.Should().Contain("trop longue");
    }

    [Fact]
    public async Task GenerateNextPageAsync_WhenStoryNotFound_ReturnsFailure()
    {
        // Arrange
        var slotName = "nonexistent-slot";

        _mockRepository
            .Setup(r => r.LoadLatestPageAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Fail("Story not found"));

        // Act
        var result = await _service.GenerateNextPageAsync(slotName, "Valid intent");

        // Assert
        result.Should().BeOfType<Result<PageInfo>.Failure>();
    }

    [Fact]
    public async Task LoadPageAsync_WhenPageExists_ReturnsPageInfo()
    {
        // Arrange
        var slotName = "test-slot";
        var pageIndex = 1;
        var storyState = StoryState.Create(Id.New(), "Test World");

        _mockRepository
            .Setup(r => r.LoadPageAsync(slotName, pageIndex, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName,
                pageIndex,
                "Test narrative text",
                "Test intent",
                "phi-4-mini",
                DateTime.UtcNow,
                storyState)));

        // Act
        var result = await _service.LoadPageAsync(slotName, pageIndex);

        // Assert
        var success = result.Should().BeOfType<Result<PageInfo>.Success>().Subject;
        success.Value.PageIndex.Should().Be(pageIndex);
        success.Value.NarrativeText.Should().Be("Test narrative text");
    }

    [Fact]
    public async Task GetPageHistoryAsync_ReturnsPageIndices()
    {
        // Arrange
        var slotName = "test-slot";
        var expectedIndices = new List<int> { 0, 1, 2, 3 };

        _mockRepository
            .Setup(r => r.GetPageHistoryAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIndices);

        // Act
        var result = await _service.GetPageHistoryAsync(slotName);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedIndices);
    }

    [Fact]
    public async Task GenerateNextPageStreamingAsync_YieldsEachChunkAndSavesTheConcatenatedText()
    {
        // Arrange
        var slotName = "test-slot";
        var state = StoryState.Create(Id.New(), "Test World");

        _mockRepository
            .Setup(r => r.LoadLatestPageAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 0, "Previous page", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        string? savedText = null;
        _mockRepository
            .Setup(r => r.SavePageAsync(slotName, 1,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoryState>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, string, string, string, StoryState, CancellationToken>(
                (_, _, text, _, _, _, _) => savedText = text)
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 1, "saved", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        var service = new GenerationService(
            new FullOrchestrationService(new Mock<ILlmClient>().Object),
            _mockRepository.Object,
            new ModelSelectionService(new LlmClientConfig { DefaultModel = "phi-4-mini", NarratorModel = "phi-4-mini" }),
            new FakeStreamingLlmClient("Once ", "upon ", "a time."),
            NullLogger<GenerationService>.Instance);

        // Act
        var received = new List<string>();
        await foreach (var chunk in service.GenerateNextPageStreamingAsync(slotName, "Continue the story"))
            received.Add(chunk);

        // Assert
        received.Should().Equal("Once ", "upon ", "a time.");
        savedText.Should().Be("Once upon a time.");
        _mockRepository.Verify(r => r.SavePageAsync(slotName, 1, "Once upon a time.",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoryState>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateNextPageStreamingAsync_WhenIntentTooLong_Throws()
    {
        var service = new GenerationService(
            new FullOrchestrationService(new Mock<ILlmClient>().Object),
            _mockRepository.Object,
            new ModelSelectionService(new LlmClientConfig { DefaultModel = "phi-4-mini" }),
            new FakeStreamingLlmClient("x"),
            NullLogger<GenerationService>.Instance);

        var act = async () =>
        {
            await foreach (var _ in service.GenerateNextPageStreamingAsync("slot", new string('x', 1001)))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateNextPageStreamingAsync_RunsAllAgents_AndPersistsTheirTraces()
    {
        // Arrange — a story with a character so the Character agent also runs (4 agents total)
        var slotName = "test-slot";
        var state = StoryState.Create(Id.New(), "Test World")
            .WithCharacters(new CharacterState(Id.New(), "Alice"));

        _mockRepository
            .Setup(r => r.LoadLatestPageAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 0, "Previous page", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        _mockRepository
            .Setup(r => r.SavePageAsync(slotName, 1, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<StoryState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 1, "saved", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        string? capturedExpertJson = null;
        _mockRepository
            .Setup(r => r.SavePageExpertDataAsync(slotName, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, string, CancellationToken>((_, _, json, _) => capturedExpertJson = json)
            .Returns(Task.CompletedTask);

        var fakeClient = new FakeStreamingLlmClient("Il ", "était ", "une fois.");
        var service = new GenerationService(
            new FullOrchestrationService(new Mock<ILlmClient>().Object),
            _mockRepository.Object,
            new ModelSelectionService(new LlmClientConfig { DefaultModel = "phi-4-mini", NarratorModel = "phi-4-mini" }),
            fakeClient,
            NullLogger<GenerationService>.Instance);

        // Act — request an explicit model for this page
        var received = new List<string>();
        await foreach (var chunk in service.GenerateNextPageStreamingAsync(slotName, "Continue", "phi-4"))
            received.Add(chunk);

        // Assert — user sees only the streamed narrator prose
        string.Concat(received).Should().Be("Il était une fois.");

        // Every prompt sent to the model carries the French-only directive...
        fakeClient.Requests.Should().NotBeEmpty();
        fakeClient.Requests.Should().OnlyContain(r =>
            (r.SystemPrompt + r.UserPrompt).Contains("EXCLUSIVEMENT en français"));

        // ...and the chosen model actually reaches every request (llm.model metadata).
        fakeClient.Requests.Should().OnlyContain(r =>
            r.Metadata.ContainsKey("llm.model") && (string)r.Metadata["llm.model"] == "phi-4");

        // The page is saved as generated with that model.
        _mockRepository.Verify(r => r.SavePageAsync(slotName, 1, It.IsAny<string>(), It.IsAny<string>(),
            "phi-4", It.IsAny<StoryState>(), It.IsAny<CancellationToken>()), Times.Once);

        // Expert traces were persisted for all four agents
        capturedExpertJson.Should().NotBeNull();
        var traces = System.Text.Json.JsonSerializer.Deserialize<List<AgentTraceInfo>>(capturedExpertJson!);
        traces.Should().NotBeNull();
        traces!.Select(t => t.Agent).Should().Contain(new[] { "Résumé", "Narrateur", "Cohérence" });
        traces!.Should().Contain(t => t.Agent.StartsWith("Personnage"));
        traces!.Single(t => t.Agent == "Narrateur").Output.Should().Be("Il était une fois.");
        _mockRepository.Verify(r => r.SavePageExpertDataAsync(slotName, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAgentTraceAsync_DeserializesStoredTraces()
    {
        var slotName = "test-slot";
        var stored = System.Text.Json.JsonSerializer.Serialize(new List<AgentTraceInfo>
        {
            new("Résumé", "Condense", "un résumé", 12.0),
            new("Narrateur", "Prose", "le texte", 340.0),
        });

        _mockRepository
            .Setup(r => r.GetPageExpertDataAsync(slotName, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var traces = await _service.GetAgentTraceAsync(slotName, 1);

        traces.Should().HaveCount(2);
        traces[0].Agent.Should().Be("Résumé");
        traces[1].Output.Should().Be("le texte");
    }

    [Fact]
    public async Task GetAgentTraceAsync_WhenNoData_ReturnsEmpty()
    {
        _mockRepository
            .Setup(r => r.GetPageExpertDataAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var traces = await _service.GetAgentTraceAsync("slot", 0);

        traces.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateNextPageStreamingAsync_WhenPostAgentsFail_StillSavesThePage()
    {
        // Arrange — the non-streaming agent calls all throw (e.g. a timeout / exhausted retries)
        var slotName = "test-slot";
        var state = StoryState.Create(Id.New(), "Test World")
            .WithCharacters(new CharacterState(Id.New(), "Alice"));

        _mockRepository
            .Setup(r => r.LoadLatestPageAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 0, "Previous page", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        _mockRepository
            .Setup(r => r.SavePageAsync(slotName, 1, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<StoryState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Narratum.Core.PageSnapshot>.Ok(new Narratum.Core.PageSnapshot(
                slotName, 1, "saved", "intent", "phi-4-mini", DateTime.UtcNow, state)));

        var service = new GenerationService(
            new FullOrchestrationService(new Mock<ILlmClient>().Object),
            _mockRepository.Object,
            new ModelSelectionService(new LlmClientConfig { DefaultModel = "phi-4-mini" }),
            new ThrowingAgentsStreamingClient("Il ", "était ", "une fois."),
            NullLogger<GenerationService>.Instance);

        // Act
        var received = new List<string>();
        await foreach (var chunk in service.GenerateNextPageStreamingAsync(slotName, "Continue"))
            received.Add(chunk);

        // Assert — the streamed narrative reached the user AND was persisted, despite the
        // Summary/Consistency/Character agents all failing.
        string.Concat(received).Should().Be("Il était une fois.");
        _mockRepository.Verify(r => r.SavePageAsync(slotName, 1, "Il était une fois.",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoryState>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Streams the narrator fine, but every non-streaming agent call throws.</summary>
    private sealed class ThrowingAgentsStreamingClient(params string[] chunks) : ILlmClient, IStreamingLlmClient
    {
        public string ClientName => "Throwing";

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result<LlmResponse>> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
            => throw new TimeoutException("simulated agent timeout");

        public async IAsyncEnumerable<string> GenerateStreamingAsync(
            LlmRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in chunks)
            {
                await Task.Yield();
                yield return chunk;
            }
        }
    }

    /// <summary>Fake client that streams a fixed set of fragments and records the requests it receives.</summary>
    private sealed class FakeStreamingLlmClient(params string[] chunks) : ILlmClient, IStreamingLlmClient
    {
        public List<LlmRequest> Requests { get; } = new();

        public string ClientName => "Fake";

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result<LlmResponse>> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result<LlmResponse>.Ok(new LlmResponse(request.RequestId, string.Concat(chunks))));
        }

        public async IAsyncEnumerable<string> GenerateStreamingAsync(
            LlmRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            foreach (var chunk in chunks)
            {
                await Task.Yield();
                yield return chunk;
            }
        }
    }

    [Fact]
    public async Task GetDisplayNameAsync_ReturnsDisplayName()
    {
        // Arrange
        var slotName = "test-slot";
        var expectedName = "My Epic Story";

        _mockRepository
            .Setup(r => r.GetDisplayNameAsync(slotName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedName);

        // Act
        var result = await _service.GetDisplayNameAsync(slotName);

        // Assert
        result.Should().Be(expectedName);
    }
}
