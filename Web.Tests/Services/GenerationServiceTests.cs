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
