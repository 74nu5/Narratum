using Narratum.State;
using Narratum.Domain;

namespace Narratum.Orchestration.Prompts;

/// <summary>
/// Service for compressing long story contexts into tiered summaries.
/// Prevents token overflow on long narratives.
/// </summary>
public class ContextCompressionService
{
    private readonly NarrativeContextCache _cache;

    // Compression thresholds
    private const int RECENT_EVENTS_WINDOW = 10;
    private const int MIDDLE_TERM_WINDOW = 50;
    private const int COMPRESSION_THRESHOLD = 100;

    public ContextCompressionService(NarrativeContextCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Compresses story state into a tiered context structure.
    /// Uses cache to avoid recomputation.
    /// </summary>
    public CompressedContext CompressStoryContext(string storyId, StoryState state)
    {
        if (state.EventHistory.Count < COMPRESSION_THRESHOLD)
        {
            // Small history - no compression needed
            return new CompressedContext(
                BuildDetailedEventList(state.EventHistory),
                string.Empty,
                string.Empty,
                state.EventHistory.Count,
                DateTime.UtcNow);
        }

        return _cache.GetOrCreateCompressedContext(
            storyId,
            state,
            st => BuildCompressedContext(st));
    }

    /// <summary>
    /// Builds a summary of recent events for context window.
    /// </summary>
    public string BuildRecentEventsSummary(string storyId, IEnumerable<Event> events)
    {
        var eventsList = events.ToList();

        if (!eventsList.Any())
            return "Story is just beginning.";

        var cached = _cache.GetOrCreateSummary(
            storyId,
            eventsList,
            evts => SummarizeEvents(evts));

        return cached.SummaryText;
    }

    private CompressedContext BuildCompressedContext(StoryState state)
    {
        var totalEvents = state.EventHistory.Count;
        var events = state.EventHistory.ToList();

        // Tier 1: Recent events (detailed, last 10)
        var recentEvents = events.TakeLast(RECENT_EVENTS_WINDOW).ToList();
        var recentDetailed = BuildDetailedEventList(recentEvents);

        // Tier 2: Middle-term summary (events 10-60 back)
        var middleStart = Math.Max(0, totalEvents - MIDDLE_TERM_WINDOW);
        var middleEnd = Math.Max(0, totalEvents - RECENT_EVENTS_WINDOW);
        var middleEvents = events.Skip(middleStart).Take(middleEnd - middleStart).ToList();
        var middleSummary = middleEvents.Any()
            ? $"Middle period ({middleEvents.Count} events): {SummarizeEvents(middleEvents)}"
            : string.Empty;

        // Tier 3: Long-term summary (everything before middle)
        var longTermEvents = events.Take(middleStart).ToList();
        var longTermSummary = longTermEvents.Any()
            ? $"Early story ({longTermEvents.Count} events): {SummarizeHighLevelArcs(longTermEvents)}"
            : string.Empty;

        return new CompressedContext(
            recentDetailed,
            middleSummary,
            longTermSummary,
            totalEvents,
            DateTime.UtcNow);
    }

    private string BuildDetailedEventList(IEnumerable<Event> events)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
            return "No events yet.";

        return string.Join("\n", eventsList.Select(e =>
            $"- {e.GetType().Name.Replace("Event", "")}: {e.Timestamp:HH:mm}"));
    }

    private string SummarizeEvents(IEnumerable<Event> events)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
            return "No events.";

        // Group by event type
        var grouped = eventsList
            .GroupBy(e => e.GetType().Name.Replace("Event", ""))
            .Select(g => $"{g.Key} ({g.Count()}x)")
            .ToList();

        return string.Join(", ", grouped);
    }

    private string SummarizeHighLevelArcs(IEnumerable<Event> events)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
            return "Story beginning.";

        // Very high-level summary for old events
        var eventTypes = eventsList
            .Select(e => e.GetType().Name.Replace("Event", ""))
            .Distinct()
            .Take(5)
            .ToList();

        return $"Story foundation established through {eventsList.Count} events " +
               $"including {string.Join(", ", eventTypes)}";
    }
}
