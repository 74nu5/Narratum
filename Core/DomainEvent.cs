namespace Narratum.Core;

/// <summary>
/// Represents a narrative event that has occurred.
/// Events are immutable facts about what has happened in the story.
/// </summary>
public abstract record DomainEvent
{
    public Id Id { get; } = Id.New();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
