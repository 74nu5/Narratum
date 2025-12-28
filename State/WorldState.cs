using Narratum.Core;

namespace Narratum.State;

/// <summary>
/// Represents the global state of the story world at a specific point in time.
/// </summary>
public record WorldState
{
    public Id WorldId { get; init; }
    public string WorldName { get; init; }
    public DateTime NarrativeTime { get; init; }
    public Id? CurrentArcId { get; init; }
    public Id? CurrentChapterId { get; init; }
    public int TotalEventCount { get; init; }

    public WorldState(Id worldId, string worldName, DateTime? narrativeTime = null)
    {
        WorldId = worldId;
        WorldName = worldName;
        NarrativeTime = narrativeTime ?? DateTime.UtcNow;
        TotalEventCount = 0;
    }

    /// <summary>
    /// Advances the narrative time.
    /// </summary>
    public WorldState AdvanceTime(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentException("Cannot go back in time.", nameof(delta));

        return this with { NarrativeTime = NarrativeTime.Add(delta) };
    }

    /// <summary>
    /// Sets the current arc.
    /// </summary>
    public WorldState WithCurrentArc(Id arcId)
    {
        return this with { CurrentArcId = arcId };
    }

    /// <summary>
    /// Sets the current chapter.
    /// </summary>
    public WorldState WithCurrentChapter(Id chapterId)
    {
        return this with { CurrentChapterId = chapterId };
    }

    /// <summary>
    /// Increments the event counter.
    /// </summary>
    public WorldState WithEventOccurred()
    {
        return this with { TotalEventCount = TotalEventCount + 1 };
    }
}
