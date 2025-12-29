using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Represents an action that can be performed in the narrative simulation.
/// All actions are immutable and timestamped for deterministic replay.
/// </summary>
public abstract record StoryAction
{
    public Id Id { get; } = Id.New();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Action to move a character to a new location.
/// </summary>
public record MoveCharacterAction(Id CharacterId, Id ToLocationId) : StoryAction;

/// <summary>
/// Action to end the current chapter and progress to the next.
/// </summary>
public record EndChapterAction(Id ChapterId) : StoryAction;

/// <summary>
/// Action to trigger an encounter between characters.
/// </summary>
public record TriggerEncounterAction(Id Character1Id, Id Character2Id, Id LocationId) : StoryAction;

/// <summary>
/// Action to record a character death.
/// </summary>
public record RecordCharacterDeathAction(Id CharacterId, Id LocationId, string Cause) : StoryAction;

/// <summary>
/// Action to advance narrative time.
/// </summary>
public record AdvanceTimeAction(TimeSpan Duration) : StoryAction;

/// <summary>
/// Action to establish or update a relationship between characters.
/// </summary>
public record UpdateRelationshipAction(Id Character1Id, Id Character2Id, Relationship Relationship) : StoryAction;

/// <summary>
/// Action to record a revelation event.
/// </summary>
public record RecordRevelationAction(Id CharacterId, string RevelationContent) : StoryAction;
