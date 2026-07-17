using System.Collections.Concurrent;
using Narratum.State;
using Narratum.Domain;

namespace Narratum.Orchestration.Prompts;

/// <summary>
/// Cache service for narrative summaries and compressed context.
/// Reduces LLM payload size for long story histories.
/// </summary>
public class NarrativeContextCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedSummary> _summaryCache = new();
    private readonly ConcurrentDictionary<string, CompressedContext> _contextCache = new();
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Gets or creates a cached summary for recent events.
    /// </summary>
    public CachedSummary GetOrCreateSummary(
        string storyId,
        IEnumerable<Event> events,
        Func<IEnumerable<Event>, string> summarizer)
    {
        ThrowIfDisposed();

        var eventsList = events.ToList();
        var cacheKey = BuildSummaryCacheKey(storyId, eventsList);

        return _summaryCache.GetOrAdd(cacheKey, _ =>
        {
            var summary = summarizer(eventsList);
            return new CachedSummary(
                summary,
                eventsList.Count,
                DateTime.UtcNow);
        });
    }

    /// <summary>
    /// Gets or creates compressed context for a story state.
    /// Compresses long event histories into tiered summaries.
    /// </summary>
    public CompressedContext GetOrCreateCompressedContext(
        string storyId,
        StoryState state,
        Func<StoryState, CompressedContext> compressor)
    {
        ThrowIfDisposed();

        var cacheKey = BuildContextCacheKey(storyId, state.EventHistory.Count);

        return _contextCache.GetOrAdd(cacheKey, _ => compressor(state));
    }

    /// <summary>
    /// Clears all cached data for a specific story.
    /// </summary>
    public void ClearStoryCache(string storyId)
    {
        ThrowIfDisposed();

        var summaryKeys = _summaryCache.Keys.Where(k => k.StartsWith($"{storyId}:")).ToList();
        foreach (var key in summaryKeys)
        {
            _summaryCache.TryRemove(key, out _);
        }

        var contextKeys = _contextCache.Keys.Where(k => k.StartsWith($"{storyId}:")).ToList();
        foreach (var key in contextKeys)
        {
            _contextCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Removes old cache entries to prevent memory bloat.
    /// </summary>
    public async Task CleanupStaleEntriesAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _cleanupLock.WaitAsync(ct);
        try
        {
            var cutoff = DateTime.UtcNow - maxAge;

            var staleSummaries = _summaryCache
                .Where(kvp => kvp.Value.CreatedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleSummaries)
            {
                _summaryCache.TryRemove(key, out _);
            }

            var staleContexts = _contextCache
                .Where(kvp => kvp.Value.CreatedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleContexts)
            {
                _contextCache.TryRemove(key, out _);
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new CacheStatistics(
            _summaryCache.Count,
            _contextCache.Count,
            _summaryCache.Values.Sum(s => s.EventCount));
    }

    private string BuildSummaryCacheKey(string storyId, IList<Event> events)
    {
        if (!events.Any())
            return $"{storyId}:summary:empty";

        var firstTimestamp = events.First().Timestamp.Ticks;
        var lastTimestamp = events.Last().Timestamp.Ticks;
        var count = events.Count;

        return $"{storyId}:summary:{firstTimestamp}:{lastTimestamp}:{count}";
    }

    private string BuildContextCacheKey(string storyId, int eventCount)
    {
        // Round to nearest 10 events for cache efficiency
        var roundedCount = (eventCount / 10) * 10;
        return $"{storyId}:context:{roundedCount}";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NarrativeContextCache));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _summaryCache.Clear();
        _contextCache.Clear();
        _cleanupLock?.Dispose();

        _disposed = true;
    }
}

/// <summary>
/// Cached summary of story events.
/// </summary>
public record CachedSummary(
    string SummaryText,
    int EventCount,
    DateTime CreatedAt);

/// <summary>
/// Compressed context with tiered summaries.
/// Reduces token count for long histories.
/// </summary>
public record CompressedContext(
    string RecentDetailedEvents,
    string MiddleTermSummary,
    string LongTermSummary,
    int TotalEventCount,
    DateTime CreatedAt);

/// <summary>
/// Cache performance statistics.
/// </summary>
public record CacheStatistics(
    int SummaryCacheSize,
    int ContextCacheSize,
    int TotalCachedEvents);
