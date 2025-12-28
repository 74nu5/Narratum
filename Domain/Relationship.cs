using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Value object representing a relationship between two characters.
/// </summary>
public record Relationship
{
    public string Type { get; init; } // e.g., "friend", "enemy", "sibling"
    public int Trust { get; init; } // -100 to 100
    public int Affection { get; init; } // -100 to 100
    public string? Notes { get; init; }

    public Relationship(string type, int trust = 0, int affection = 0, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Relationship type cannot be empty.", nameof(type));
        if (trust < -100 || trust > 100)
            throw new ArgumentException("Trust must be between -100 and 100.", nameof(trust));
        if (affection < -100 || affection > 100)
            throw new ArgumentException("Affection must be between -100 and 100.", nameof(affection));

        Type = type;
        Trust = trust;
        Affection = affection;
        Notes = notes;
    }

    /// <summary>
    /// Updates the trust value.
    /// </summary>
    public Relationship UpdateTrust(int delta)
    {
        var newTrust = Math.Clamp(Trust + delta, -100, 100);
        return this with { Trust = newTrust };
    }

    /// <summary>
    /// Updates the affection value.
    /// </summary>
    public Relationship UpdateAffection(int delta)
    {
        var newAffection = Math.Clamp(Affection + delta, -100, 100);
        return this with { Affection = newAffection };
    }
}
