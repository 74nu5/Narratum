using Narratum.Core;
using Narratum.Domain;

namespace Narratum.State;

/// <summary>
/// Represents the state of a character at a specific point in the narrative.
/// Immutable or controlled mutation for deterministic transitions.
/// </summary>
public record CharacterState
{
    public Id CharacterId { get; init; }
    public string Name { get; init; }
    public VitalStatus VitalStatus { get; init; }
    public Id? CurrentLocationId { get; init; }
    public IReadOnlySet<string> KnownFacts { get; init; } = new HashSet<string>();
    public Id? LastEventId { get; init; }
    public DateTime LastUpdatedAt { get; init; }

    public CharacterState(
        Id characterId, 
        string name, 
        VitalStatus vitalStatus = VitalStatus.Alive,
        Id? currentLocationId = null)
    {
        CharacterId = characterId;
        Name = name;
        VitalStatus = vitalStatus;
        CurrentLocationId = currentLocationId;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a known fact about this character.
    /// </summary>
    public CharacterState WithKnownFact(string fact)
    {
        var facts = new HashSet<string>(KnownFacts) { fact };
        return this with { KnownFacts = facts.AsReadOnly() };
    }

    /// <summary>
    /// Updates the character's vital status.
    /// </summary>
    public CharacterState WithVitalStatus(VitalStatus status)
    {
        return this with 
        { 
            VitalStatus = status,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Moves the character to a new location.
    /// </summary>
    public CharacterState MoveTo(Id locationId)
    {
        if (VitalStatus == VitalStatus.Dead)
            throw new InvalidOperationException("Dead characters cannot move.");

        return this with 
        { 
            CurrentLocationId = locationId,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records an event that involved this character.
    /// </summary>
    public CharacterState WithLastEvent(Id eventId)
    {
        return this with 
        { 
            LastEventId = eventId,
            LastUpdatedAt = DateTime.UtcNow
        };
    }
}
