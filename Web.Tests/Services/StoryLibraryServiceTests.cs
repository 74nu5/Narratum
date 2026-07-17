using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Narratum.Core;
using Narratum.Web.Services;
using Xunit;

namespace Narratum.Web.Tests.Services;

public class StoryLibraryServiceTests
{
    private readonly Mock<IStoryRepository> _mockRepository;
    private readonly StoryLibraryService _service;

    public StoryLibraryServiceTests()
    {
        _mockRepository = new Mock<IStoryRepository>();
        _service = new StoryLibraryService(
            _mockRepository.Object,
            NullLogger<StoryLibraryService>.Instance);
    }

    [Fact]
    public async Task ListStoriesAsync_WhenStoriesExist_ReturnsStoryList()
    {
        // Arrange
        var expectedStories = new List<StoryEntry>
        {
            new StoryEntry("slot1", "Story 1", "Fantasy tale", 5, DateTime.UtcNow.AddDays(-1), "Fantasy"),
            new StoryEntry("slot2", "Story 2", "Sci-fi adventure", 3, DateTime.UtcNow, "Sci-Fi")
        };

        _mockRepository
            .Setup(r => r.ListStoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStories);

        // Act
        var result = await _service.ListStoriesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].SlotName.Should().Be("slot1");
        result[0].DisplayName.Should().Be("Story 1");
        result[1].SlotName.Should().Be("slot2");
        result[1].DisplayName.Should().Be("Story 2");
    }

    [Fact]
    public async Task ListStoriesAsync_WhenNoStories_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.ListStoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoryEntry>());

        // Act
        var result = await _service.ListStoriesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteStoryAsync_CallsRepository()
    {
        // Arrange
        var slotName = "test-slot";

        _mockRepository
            .Setup(r => r.DeleteStoryAsync(slotName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteStoryAsync(slotName);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteStoryAsync(slotName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListStoriesAsync_MapsRepositoryModelToWebModel()
    {
        // Arrange
        var repositoryStory = new StoryEntry(
            "test-slot",
            "Test Story",
            "A test description",
            10,
            DateTime.UtcNow,
            "Fantasy");

        _mockRepository
            .Setup(r => r.ListStoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoryEntry> { repositoryStory });

        // Act
        var result = await _service.ListStoriesAsync();

        // Assert
        result.Should().HaveCount(1);
        var webModel = result[0];
        webModel.SlotName.Should().Be(repositoryStory.SlotName);
        webModel.DisplayName.Should().Be(repositoryStory.DisplayName);
        webModel.Description.Should().Be(repositoryStory.Description);
        webModel.PageCount.Should().Be(repositoryStory.PageCount);
        webModel.LastModified.Should().Be(repositoryStory.LastUpdated);
        webModel.GenreStyle.Should().Be(repositoryStory.GenreStyle);
    }
}
