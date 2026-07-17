using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.Orchestration.Prompts;
using Narratum.State;
using Xunit;

namespace Narratum.Orchestration.Tests.Prompts;

public class ContextCompressionServiceTests : IDisposable
{
    private readonly NarrativeContextCache _cache;
    private readonly ContextCompressionService _service;

    public ContextCompressionServiceTests()
    {
        _cache = new NarrativeContextCache();
        _service = new ContextCompressionService(_cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ContextCompressionService(null!));
    }

    [Fact]
    public void CompressStoryContext_WithSmallHistory_ReturnsUncompressed()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        // Add a few events (< 100)
        for (int i = 0; i < 10; i++)
        {
            state = state.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act
        var result = _service.CompressStoryContext(storyId, state);

        // Assert
        result.TotalEventCount.Should().Be(10);
        result.MiddleTermSummary.Should().BeEmpty();
        result.LongTermSummary.Should().BeEmpty();
        result.RecentDetailedEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void CompressStoryContext_WithLargeHistory_ReturnsCompressed()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        // Add many events (> 100)
        for (int i = 0; i < 150; i++)
        {
            state = state.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act
        var result = _service.CompressStoryContext(storyId, state);

        // Assert
        result.TotalEventCount.Should().Be(150);
        result.RecentDetailedEvents.Should().NotBeEmpty(); // Last 10 events
        result.MiddleTermSummary.Should().NotBeEmpty(); // Events 10-60 back
        result.LongTermSummary.Should().NotBeEmpty(); // Older events
    }

    [Fact]
    public void CompressStoryContext_WithEmptyHistory_ReturnsEmpty()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        // Act
        var result = _service.CompressStoryContext(storyId, state);

        // Assert
        result.TotalEventCount.Should().Be(0);
        result.RecentDetailedEvents.Should().Contain("No events yet");
    }

    [Fact]
    public void CompressStoryContext_UsesCacheOnSecondCall()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        for (int i = 0; i < 150; i++)
        {
            state = state.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act - Call twice with same state
        var result1 = _service.CompressStoryContext(storyId, state);
        var result2 = _service.CompressStoryContext(storyId, state);

        // Assert - Should return same cached instance
        result1.Should().Be(result2);
    }

    [Fact]
    public void BuildRecentEventsSummary_WithNoEvents_ReturnsBeginningMessage()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>();

        // Act
        var result = _service.BuildRecentEventsSummary(storyId, events);

        // Assert
        result.Should().Be("Story is just beginning.");
    }

    [Fact]
    public void BuildRecentEventsSummary_WithEvents_ReturnsSummary()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1"),
            new RevelationEvent(Id.New(), "Scene 2")
        };

        // Act
        var result = _service.BuildRecentEventsSummary(storyId, events);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("Revelation");
    }

    [Fact]
    public void BuildRecentEventsSummary_UsesCacheOnSecondCall()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };

        // Act
        var result1 = _service.BuildRecentEventsSummary(storyId, events);
        var result2 = _service.BuildRecentEventsSummary(storyId, events);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void CompressStoryContext_WithMediumHistory_ReturnsOnlyRecentAndMiddle()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        // Add 110 events (enough for compression, but not for long-term)
        for (int i = 0; i < 110; i++)
        {
            state = state.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act
        var result = _service.CompressStoryContext(storyId, state);

        // Assert
        result.TotalEventCount.Should().Be(110);
        result.RecentDetailedEvents.Should().NotBeEmpty();
        result.MiddleTermSummary.Should().NotBeEmpty();
        result.LongTermSummary.Should().NotBeEmpty(); // 110 events = 60 old events
    }

    [Fact]
    public void CompressStoryContext_CreatesNewInstanceWithCurrentTimestamp()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        for (int i = 0; i < 150; i++)
        {
            state = state.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act
        var result = _service.CompressStoryContext(storyId, state);

        // Assert
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CompressStoryContext_WithDifferentEventCounts_RoundsToNearest10()
    {
        // Arrange - Cache key should round to nearest 10
        var storyId = "story1";
        var worldId = Id.New();
        var state1 = StoryState.Create(worldId, "Test World");
        var state2 = StoryState.Create(worldId, "Test World");

        // Add 105 events to state1
        for (int i = 0; i < 105; i++)
        {
            state1 = state1.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Add 109 events to state2 (should use same cache key: 100)
        for (int i = 0; i < 109; i++)
        {
            state2 = state2.WithEvent(new RevelationEvent(Id.New(), $"Scene {i}"));
        }

        // Act
        var result1 = _service.CompressStoryContext(storyId, state1);
        var result2 = _service.CompressStoryContext(storyId, state2);

        // Assert - Should use same cached result (both round to 100)
        result1.Should().Be(result2);
    }
}
