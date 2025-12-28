using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents a chapter within a story arc - the atomic unit of narrative progression.
/// </summary>
public class StoryChapter
{
    public Id Id { get; }
    public Id ArcId { get; }
    public int Index { get; }
    public StoryProgressStatus Status { get; private set; }
    public IReadOnlyList<Event> Events { get; private set; } = [];
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Creates a new story chapter.
    /// </summary>
    public StoryChapter(Id arcId, int index)
    {
        if (arcId == null)
            throw new ArgumentNullException(nameof(arcId));
        if (index < 0)
            throw new ArgumentException("Index cannot be negative.", nameof(index));

        Id = Id.New();
        ArcId = arcId;
        Index = index;
        Status = StoryProgressStatus.NotStarted;
    }

    /// <summary>
    /// Internal constructor for deserialization.
    /// </summary>
    internal StoryChapter(Id id, Id arcId, int index, StoryProgressStatus status, 
        IReadOnlyList<Event> events, DateTime? startedAt, DateTime? completedAt)
    {
        Id = id;
        ArcId = arcId;
        Index = index;
        Status = status;
        Events = events;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Starts the chapter.
    /// </summary>
    public void Start()
    {
        if (Status != StoryProgressStatus.NotStarted)
            throw new InvalidOperationException($"Cannot start chapter in status {Status}.");

        Status = StoryProgressStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes the chapter.
    /// </summary>
    public void Complete()
    {
        if (Status != StoryProgressStatus.InProgress)
            throw new InvalidOperationException($"Can only complete a chapter that is InProgress.");

        Status = StoryProgressStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds an event to the chapter's event history.
    /// </summary>
    public void AddEvent(Event storyEvent)
    {
        if (storyEvent == null)
            throw new ArgumentNullException(nameof(storyEvent));

        var events = Events.ToList();
        events.Add(storyEvent);
        Events = events.AsReadOnly();
    }
}
