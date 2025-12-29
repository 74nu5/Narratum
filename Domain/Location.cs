using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents a location in the narrative world.
/// </summary>
public class Location
{
    public Id Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Id? ParentLocationId { get; } // For hierarchical locations
    public IReadOnlyList<Id> AccessibleFromLocationIds { get; private set; } = [];

    /// <summary>
    /// Creates a new location.
    /// </summary>
    public Location(string name, string description = "", Id? parentLocationId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = Id.New();
        Name = name;
        Description = description;
        ParentLocationId = parentLocationId;
    }

    /// <summary>
    /// Internal constructor for deserialization.
    /// </summary>
    internal Location(Id id, string name, string description, Id? parentLocationId, IReadOnlyList<Id> accessibleFromLocationIds)
    {
        Id = id;
        Name = name;
        Description = description;
        ParentLocationId = parentLocationId;
        AccessibleFromLocationIds = accessibleFromLocationIds;
    }

    /// <summary>
    /// Marks another location as accessible from this one.
    /// </summary>
    public void AddAccessibleLocation(Id locationId)
    {
        if (locationId == null)
            throw new ArgumentNullException(nameof(locationId));
        if (locationId == Id)
            throw new InvalidOperationException("A location cannot be accessible from itself.");

        var locations = AccessibleFromLocationIds.ToList();
        if (!locations.Contains(locationId))
        {
            locations.Add(locationId);
            AccessibleFromLocationIds = locations.AsReadOnly();
        }
    }

    /// <summary>
    /// Checks if another location is accessible from this one.
    /// </summary>
    public bool IsAccessibleFrom(Id locationId)
    {
        return AccessibleFromLocationIds.Contains(locationId);
    }

    /// <summary>
    /// Removes an accessible location.
    /// </summary>
    public void RemoveAccessibleLocation(Id locationId)
    {
        var locations = AccessibleFromLocationIds.Where(l => l != locationId).ToList();
        AccessibleFromLocationIds = locations.AsReadOnly();
    }
}
