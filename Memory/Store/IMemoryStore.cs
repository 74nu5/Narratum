namespace Narratum.Memory.Store;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Narratum.Memory;

/// <summary>
/// Query criteria for advanced memoranda filtering.
/// </summary>
public record MemoryQuery(
    Guid? WorldId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? TitleFilter = null
);

/// <summary>
/// Interface for querying memoranda from persistent storage.
/// Provides high-level query operations on top of IMemoryRepository.
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Retrieves a memorandum by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the memorandum.</param>
    /// <returns>The memorandum if found; null otherwise.</returns>
    Task<Memorandum?> RetrieveAsync(Guid id);

    /// <summary>
    /// Queries memoranda using advanced filtering criteria.
    /// </summary>
    /// <param name="query">The query criteria (world, date range, title).</param>
    /// <returns>List of memoranda matching the criteria.</returns>
    Task<IReadOnlyList<Memorandum>> QueryAsync(MemoryQuery query);

    /// <summary>
    /// Retrieves all memoranda for a specific story world.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <returns>List of memoranda for the world.</returns>
    Task<IReadOnlyList<Memorandum>> GetByWorldAsync(Guid worldId);

    /// <summary>
    /// Retrieves all memoranda matching a title pattern.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <param name="titlePattern">The title pattern to search for.</param>
    /// <returns>List of memoranda matching the pattern.</returns>
    Task<IReadOnlyList<Memorandum>> GetByTitleAsync(Guid worldId, string titlePattern);
}
