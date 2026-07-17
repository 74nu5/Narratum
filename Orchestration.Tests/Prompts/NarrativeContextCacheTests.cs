using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.Orchestration.Prompts;
using Narratum.State;
using Xunit;

namespace Narratum.Orchestration.Tests.Prompts;

public class NarrativeContextCacheTests : IDisposable
{
    private readonly NarrativeContextCache _cache;

    public NarrativeContextCacheTests()
    {
        _cache = new NarrativeContextCache();
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public void GetOrCreateSummary_WithNewKey_CallsSummarizer()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        var summarizerCalled = false;
        string Summarizer(IEnumerable<Event> e)
        {
            summarizerCalled = true;
            return "Summary";
        }

        // Act
        var result = _cache.GetOrCreateSummary(storyId, events, Summarizer);

        // Assert
        summarizerCalled.Should().BeTrue();
        result.SummaryText.Should().Be("Summary");
        result.EventCount.Should().Be(1);
    }

    [Fact]
    public void GetOrCreateSummary_WithSameKey_ReturnsCachedValue()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        var callCount = 0;
        string Summarizer(IEnumerable<Event> e)
        {
            callCount++;
            return $"Summary {callCount}";
        }

        // Act
        var result1 = _cache.GetOrCreateSummary(storyId, events, Summarizer);
        var result2 = _cache.GetOrCreateSummary(storyId, events, Summarizer);

        // Assert
        callCount.Should().Be(1); // Only called once
        result1.Should().Be(result2);
        result2.SummaryText.Should().Be("Summary 1"); // Cached value
    }

    [Fact]
    public void GetOrCreateSummary_WithEmptyEvents_CachesCorrectly()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>();
        string Summarizer(IEnumerable<Event> e) => "Empty";

        // Act
        var result = _cache.GetOrCreateSummary(storyId, events, Summarizer);

        // Assert
        result.SummaryText.Should().Be("Empty");
        result.EventCount.Should().Be(0);
    }

    [Fact]
    public void GetOrCreateCompressedContext_WithNewKey_CallsCompressor()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test");
        var compressorCalled = false;
        CompressedContext Compressor(StoryState s)
        {
            compressorCalled = true;
            return new CompressedContext("Recent", "Middle", "Long", 0, DateTime.UtcNow);
        }

        // Act
        var result = _cache.GetOrCreateCompressedContext(storyId, state, Compressor);

        // Assert
        compressorCalled.Should().BeTrue();
        result.RecentDetailedEvents.Should().Be("Recent");
    }

    [Fact]
    public void GetOrCreateCompressedContext_WithSameKey_ReturnsCachedValue()
    {
        // Arrange
        var storyId = "story1";
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test");
        var callCount = 0;
        CompressedContext Compressor(StoryState s)
        {
            callCount++;
            return new CompressedContext($"Recent{callCount}", "Middle", "Long", 0, DateTime.UtcNow);
        }

        // Act
        var result1 = _cache.GetOrCreateCompressedContext(storyId, state, Compressor);
        var result2 = _cache.GetOrCreateCompressedContext(storyId, state, Compressor);

        // Assert
        callCount.Should().Be(1); // Only called once
        result1.Should().Be(result2);
        result2.RecentDetailedEvents.Should().Be("Recent1"); // Cached value
    }

    [Fact]
    public void ClearStoryCache_RemovesAllEntriesForStory()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test");
        _cache.GetOrCreateSummary(storyId, events, _ => "Summary");
        _cache.GetOrCreateCompressedContext(storyId, state, _ =>
            new CompressedContext("R", "M", "L", 0, DateTime.UtcNow));

        // Act
        _cache.ClearStoryCache(storyId);

        // Assert
        var stats = _cache.GetStatistics();
        stats.SummaryCacheSize.Should().Be(0);
        stats.ContextCacheSize.Should().Be(0);
    }

    [Fact]
    public void ClearStoryCache_OnlyRemovesSpecifiedStory()
    {
        // Arrange
        var story1 = "story1";
        var story2 = "story2";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        _cache.GetOrCreateSummary(story1, events, _ => "Summary1");
        _cache.GetOrCreateSummary(story2, events, _ => "Summary2");

        // Act
        _cache.ClearStoryCache(story1);

        // Assert
        var stats = _cache.GetStatistics();
        stats.SummaryCacheSize.Should().Be(1); // story2 still cached
    }

    [Fact]
    public async Task CleanupStaleEntriesAsync_RemovesOldEntries()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        _cache.GetOrCreateSummary(storyId, events, _ => "Summary");

        // Act - cleanup entries older than 0 seconds (should remove all)
        await _cache.CleanupStaleEntriesAsync(TimeSpan.Zero);

        // Assert
        var stats = _cache.GetStatistics();
        stats.SummaryCacheSize.Should().Be(0);
    }

    [Fact]
    public async Task CleanupStaleEntriesAsync_KeepsRecentEntries()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        _cache.GetOrCreateSummary(storyId, events, _ => "Summary");

        // Act - cleanup entries older than 1 hour (should keep recent)
        await _cache.CleanupStaleEntriesAsync(TimeSpan.FromHours(1));

        // Assert
        var stats = _cache.GetStatistics();
        stats.SummaryCacheSize.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        _cache.GetOrCreateSummary("story1", events, _ => "Summary1");
        _cache.GetOrCreateSummary("story2", events, _ => "Summary2");

        // Act
        var stats = _cache.GetStatistics();

        // Assert
        stats.SummaryCacheSize.Should().Be(2);
        stats.TotalCachedEvents.Should().Be(2);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var cache = new NarrativeContextCache();

        // Act & Assert - Should not throw
        cache.Dispose();
        cache.Dispose();
    }

    [Fact]
    public void AfterDispose_OperationsThrowObjectDisposedException()
    {
        // Arrange
        var cache = new NarrativeContextCache();
        cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            cache.GetOrCreateSummary("test", new List<Event>(), _ => "test"));
    }

    [Fact]
    public async Task CleanupStaleEntriesAsync_ConcurrentCalls_HandleGracefully()
    {
        // Arrange
        var storyId = "story1";
        var events = new List<Event>
        {
            new RevelationEvent(Id.New(), "Scene 1")
        };
        _cache.GetOrCreateSummary(storyId, events, _ => "Summary");

        // Act - Fire 5 concurrent cleanup calls
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => _cache.CleanupStaleEntriesAsync(TimeSpan.FromHours(1))))
            .ToArray();

        // Assert - Should not throw
        await Task.WhenAll(tasks);
    }
}
