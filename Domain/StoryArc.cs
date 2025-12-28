using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents an arc of the narrative - a major story structure.
/// An arc contains multiple chapters and guides the overall narrative progression.
/// </summary>
public class StoryArc
{
    public Id Id { get; }
    public Id WorldId { get; }
    public string Title { get; }
    public string Objective { get; }
    public StoryProgressStatus Status { get; private set; }
    public IReadOnlyList<StoryChapter> Chapters { get; private set; } = [];
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Creates a new story arc.
    /// </summary>
    public StoryArc(Id worldId, string title, string objective = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (worldId == null)
            throw new ArgumentNullException(nameof(worldId));

        Id = Id.New();
        WorldId = worldId;
        Title = title;
        Objective = objective;
        Status = StoryProgressStatus.NotStarted;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal constructor for deserialization.
    /// </summary>
    internal StoryArc(Id id, Id worldId, string title, string objective, StoryProgressStatus status, 
        IReadOnlyList<StoryChapter> chapters, DateTime createdAt)
    {
        Id = id;
        WorldId = worldId;
        Title = title;
        Objective = objective;
        Status = status;
        Chapters = chapters;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Starts the arc.
    /// </summary>
    public void Start()
    {
        if (Status != StoryProgressStatus.NotStarted)
            throw new InvalidOperationException($"Cannot start arc that is in status {Status}.");

        Status = StoryProgressStatus.InProgress;
    }

    /// <summary>
    /// Completes the arc.
    /// </summary>
    public void Complete()
    {
        if (Status != StoryProgressStatus.InProgress)
            throw new InvalidOperationException($"Can only complete an arc that is InProgress.");

        Status = StoryProgressStatus.Completed;
    }

    /// <summary>
    /// Adds a chapter to the arc.
    /// </summary>
    public void AddChapter(StoryChapter chapter)
    {
        if (chapter == null)
            throw new ArgumentNullException(nameof(chapter));
        if (chapter.ArcId != Id)
            throw new InvalidOperationException("Chapter does not belong to this arc.");

        var chapters = Chapters.ToList();
        chapters.Add(chapter);
        Chapters = chapters.AsReadOnly();
    }

    /// <summary>
    /// Gets the next chapter index for a new chapter.
    /// </summary>
    public int GetNextChapterIndex() => Chapters.Count;
}
