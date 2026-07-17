using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Narratum.Core;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Services;
using Narratum.State;
using Narratum.Web.Models;
using Narratum.Web.Services;
using Xunit;

namespace Narratum.Web.Tests.Services;

public class GenerationServiceTests
{
    private readonly Mock<FullOrchestrationService> _mockOrchestrator;
    private readonly Mock<IStoryRepository> _mockRepository;
    private readonly Mock<ModelSelectionService> _mockModelSelector;
    private readonly GenerationService _service;

    public GenerationServiceTests()
    {
        _mockOrchestrator = new Mock<FullOrchestrationService>();
        _mockRepository = new Mock<IStoryRepository>();
        _mockModelSelector = new Mock<ModelSelectionService>();

        _mockModelSelector.Setup(m => m.CurrentNarratorModel).Returns("phi-4-mini");

        _service = new GenerationService(
            _mockOrchestrator.Object,
            _mockRepository.Object,
            _mockModelSelector.Object,
            NullLogger<GenerationService>.Instance);
    }

    [Fact]
    public async Task CreateStoryAsync_WhenValidRequest_ReturnsSuccess()
    {
        // Arrange
        var slotName = "test-slot";
        var request = new StoryCreationRequest
        {
            WorldName = "Test World",
            GenreStyle = "Fantasy",
            WorldDescription = "A magical realm",
            NarrativeStyle = "Epic",
            Characters = new[]
            {
                new CharacterDefinition("Hero", "A brave warrior")
            }
        };

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
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Match(
            onSuccess: value => value.Should().Be(slotName),
            onFailure: _ => throw new Exception("Expected success"));

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
        var request = new StoryCreationRequest
        {
            WorldName = "Test",
            GenreStyle = "Fantasy",
            Characters = new[] { new CharacterDefinition("Hero", "desc") }
        };

        // Act
        var result = await _service.CreateStoryAsync("", request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Match(
            onSuccess: _ => throw new Exception("Expected failure"),
            onFailure: error => error.Should().Contain("slot"));

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
        var request = new StoryCreationRequest
        {
            WorldName = "Test",
            GenreStyle = "Fantasy",
            Characters = Array.Empty<CharacterDefinition>()
        };

        // Act
        var result = await _service.CreateStoryAsync("test-slot", request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Match(
            onSuccess: _ => throw new Exception("Expected failure"),
            onFailure: error => error.Should().Contain("personnage"));
    }

    [Fact]
    public async Task GenerateNextPageAsync_WhenEmptyIntent_ReturnsFailure()
    {
        // Arrange
        var slotName = "test-slot";

        // Act
        var result = await _service.GenerateNextPageAsync(slotName, "");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Match(
            onSuccess: _ => throw new Exception("Expected failure"),
            onFailure: error => error.Should().Contain("intention"));

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
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Match(
            onSuccess: _ => throw new Exception("Expected failure"),
            onFailure: error => error.Should().Contain("trop longue"));
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
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
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
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Match(
            onSuccess: page =>
            {
                page.PageIndex.Should().Be(pageIndex);
                page.NarrativeText.Should().Be("Test narrative text");
            },
            onFailure: _ => throw new Exception("Expected success"));
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
