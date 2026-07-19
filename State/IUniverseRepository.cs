namespace Narratum.Core;

/// <summary>
/// Persistence for universes — the reusable setting (world, tone, cast, places, opening) that
/// stories are runs of. Kept apart from <see cref="IStoryRepository"/>: a universe is its own
/// aggregate, edited on its own and outliving any single run.
/// </summary>
public interface IUniverseRepository
{
    /// <summary>Creates a universe and returns it.</summary>
    Task<Universe> CreateAsync(Universe universe, CancellationToken ct = default);

    /// <summary>All universes, most recently created first.</summary>
    Task<IReadOnlyList<Universe>> ListAsync(CancellationToken ct = default);

    /// <summary>A single universe, or null when it no longer exists.</summary>
    Task<Universe?> GetAsync(string universeId, CancellationToken ct = default);

    /// <summary>Updates the editable fields of a universe.</summary>
    Task UpdateAsync(Universe universe, CancellationToken ct = default);

    /// <summary>
    /// Deletes a universe. Its runs are left in place but become unattached — deleting the
    /// setting must never destroy the stories written in it.
    /// </summary>
    Task DeleteAsync(string universeId, CancellationToken ct = default);
}

/// <summary>
/// A reusable setting. <paramref name="Characters"/> and <paramref name="Locations"/> travel as
/// serialized JSON: the persistence layer stays agnostic of the Web layer's shapes, exactly as it
/// already does for choices, characters and secrets on a page.
/// </summary>
public record Universe(
    string UniverseId,
    string Name,
    string GenreStyle,
    string? Description = null,
    string? NarrativeStyle = null,
    string? SerializedCharacters = null,
    string? SerializedLocations = null,
    string? OpeningAction = null,
    string? DefaultModel = null,
    DateTime CreatedAt = default);
