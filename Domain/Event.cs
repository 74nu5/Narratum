using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents an immutable narrative event that has occurred in the story.
/// Events are the canonical facts about what happened and never disappear.
/// They form the complete history of the narrative.
/// </summary>
public abstract class Event
{
    public Id Id { get; }
    public string Type { get; }
    public DateTime Timestamp { get; }
    public IReadOnlyList<Id> ActorIds { get; } // Characters involved
    public Id? LocationId { get; } // Where it happened
    public IReadOnlyDictionary<string, object> Data { get; } // Event-specific data

    protected Event(string type, IReadOnlyList<Id> actorIds, Id? locationId = null, 
        IReadOnlyDictionary<string, object>? data = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Event type cannot be empty.", nameof(type));
        if (actorIds == null || actorIds.Count == 0)
            throw new ArgumentException("At least one actor is required.", nameof(actorIds));

        Id = Id.New();
        Type = type;
        Timestamp = DateTime.UtcNow;
        ActorIds = actorIds;
        LocationId = locationId;
        Data = data ?? new Dictionary<string, object>();
    }

    protected Event(Id id, string type, DateTime timestamp, IReadOnlyList<Id> actorIds, 
        Id? locationId, IReadOnlyDictionary<string, object> data)
    {
        Id = id;
        Type = type;
        Timestamp = timestamp;
        ActorIds = actorIds;
        LocationId = locationId;
        Data = data;
    }
}

/// <summary>
/// Represents a character encounter event.
/// </summary>
public class CharacterEncounterEvent : Event
{
    public CharacterEncounterEvent(Id character1Id, Id character2Id, Id? locationId = null)
        : base("CharacterEncounter", [character1Id, character2Id], locationId)
    {
    }

    internal CharacterEncounterEvent(Id id, DateTime timestamp, Id character1Id, Id character2Id, 
        Id? locationId, IReadOnlyDictionary<string, object> data)
        : base(id, "CharacterEncounter", timestamp, [character1Id, character2Id], locationId, data)
    {
    }
}

/// <summary>
/// Represents a character death event.
/// </summary>
public class CharacterDeathEvent : Event
{
    public CharacterDeathEvent(Id characterId, Id? locationId = null, string? cause = null)
        : base("CharacterDeath", [characterId], locationId, 
            cause != null ? new Dictionary<string, object> { { "cause", cause } } : null)
    {
    }

    internal CharacterDeathEvent(Id id, DateTime timestamp, Id characterId, Id? locationId, IReadOnlyDictionary<string, object> data)
        : base(id, "CharacterDeath", timestamp, [characterId], locationId, data)
    {
    }

    public string? GetCause() => Data.TryGetValue("cause", out var cause) ? cause as string : null;
}

/// <summary>
/// Represents a character movement event.
/// </summary>
public class CharacterMovedEvent : Event
{
    public CharacterMovedEvent(Id characterId, Id fromLocationId, Id toLocationId)
        : base("CharacterMoved", [characterId], toLocationId,
            new Dictionary<string, object> { { "fromLocation", fromLocationId } })
    {
    }

    internal CharacterMovedEvent(Id id, DateTime timestamp, Id characterId, Id fromLocationId, 
        Id toLocationId, IReadOnlyDictionary<string, object> data)
        : base(id, "CharacterMoved", timestamp, [characterId], toLocationId, data)
    {
    }

    public Id GetFromLocation() => (Id)Data["fromLocation"];
}

/// <summary>
/// Represents a revelation event (something becomes known).
/// </summary>
public class RevelationEvent : Event
{
    public RevelationEvent(Id characterId, string revelationContent)
        : base("Revelation", [characterId], null,
            new Dictionary<string, object> { { "content", revelationContent } })
    {
    }

    internal RevelationEvent(Id id, DateTime timestamp, Id characterId, IReadOnlyDictionary<string, object> data)
        : base(id, "Revelation", timestamp, [characterId], null, data)
    {
    }

    public string GetContent() => (string)Data["content"];
}
