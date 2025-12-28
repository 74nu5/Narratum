using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents a persistent character entity in the narrative world.
/// Characters have fixed traits (immutable) and mutable state (location, relationships).
/// </summary>
public class Character
{
    public Id Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, string> Traits { get; } // Immutable character traits
    public IReadOnlyDictionary<Id, Relationship> Relationships { get; private set; } = new Dictionary<Id, Relationship>();
    public VitalStatus VitalStatus { get; private set; }
    public Id? CurrentLocationId { get; private set; }

    /// <summary>
    /// Creates a new character with specified traits.
    /// </summary>
    public Character(string name, IReadOnlyDictionary<string, string>? traits = null, Id? initialLocationId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = Id.New();
        Name = name;
        Traits = traits ?? new Dictionary<string, string>();
        VitalStatus = VitalStatus.Alive;
        CurrentLocationId = initialLocationId;
    }

    /// <summary>
    /// Internal constructor for deserialization.
    /// </summary>
    internal Character(Id id, string name, IReadOnlyDictionary<string, string> traits,
        IReadOnlyDictionary<Id, Relationship> relationships, VitalStatus vitalStatus, Id? currentLocationId)
    {
        Id = id;
        Name = name;
        Traits = traits;
        Relationships = relationships;
        VitalStatus = vitalStatus;
        CurrentLocationId = currentLocationId;
    }

    /// <summary>
    /// Moves the character to a new location.
    /// Dead characters cannot move.
    /// </summary>
    public void MoveTo(Id locationId)
    {
        if (VitalStatus == VitalStatus.Dead)
            throw new InvalidOperationException("A dead character cannot move.");
        if (locationId == null)
            throw new ArgumentNullException(nameof(locationId));

        CurrentLocationId = locationId;
    }

    /// <summary>
    /// Marks the character as dead.
    /// Invariant: Dead characters cannot be ressurected.
    /// </summary>
    public void Die()
    {
        if (VitalStatus == VitalStatus.Dead)
            throw new InvalidOperationException("Character is already dead.");

        VitalStatus = VitalStatus.Dead;
    }

    /// <summary>
    /// Establishes or updates a relationship with another character.
    /// Relationships are automatically bidirectional.
    /// </summary>
    public void SetRelationship(Id otherCharacterId, Relationship relationship)
    {
        if (otherCharacterId == null)
            throw new ArgumentNullException(nameof(otherCharacterId));
        if (otherCharacterId == Id)
            throw new InvalidOperationException("Cannot create a relationship with yourself.");
        if (relationship == null)
            throw new ArgumentNullException(nameof(relationship));

        var relationships = new Dictionary<Id, Relationship>(Relationships)
        {
            [otherCharacterId] = relationship
        };
        Relationships = relationships.AsReadOnly();
    }

    /// <summary>
    /// Gets the relationship with another character, if it exists.
    /// </summary>
    public Relationship? GetRelationship(Id otherCharacterId)
    {
        return Relationships.TryGetValue(otherCharacterId, out var relationship) ? relationship : null;
    }

    /// <summary>
    /// Removes a relationship with another character.
    /// </summary>
    public void RemoveRelationship(Id otherCharacterId)
    {
        if (otherCharacterId == null)
            throw new ArgumentNullException(nameof(otherCharacterId));

        var relationships = new Dictionary<Id, Relationship>(Relationships);
        relationships.Remove(otherCharacterId);
        Relationships = relationships.AsReadOnly();
    }
}
