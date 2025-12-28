namespace Narratum.Memory.Store;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Narratum.Memory;

/// <summary>
/// Repository interface for CRUD operations on Memorandum entities.
/// Provides the data access abstraction for memory persistence.
/// </summary>
public interface IMemoryRepository
{
    /// <summary>
    /// Retrieves a memorandum by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the memorandum.</param>
    /// <returns>The memorandum if found; null otherwise.</returns>
    Task<Memorandum?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieves all memoranda for a specific story world.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <returns>List of memoranda for the world, ordered by creation date descending.</returns>
    Task<IReadOnlyList<Memorandum>> GetByWorldAsync(Guid worldId);

    /// <summary>
    /// Retrieves all memoranda matching a title pattern.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <param name="titlePattern">The title pattern to search for.</param>
    /// <returns>List of memoranda matching the pattern.</returns>
    Task<IReadOnlyList<Memorandum>> GetByTitleAsync(Guid worldId, string titlePattern);

    /// <summary>
    /// Saves a single memorandum to the database.
    /// </summary>
    /// <param name="memorandum">The memorandum to save.</param>
    Task SaveAsync(Memorandum memorandum);

    /// <summary>
    /// Saves multiple memoranda in a single transaction.
    /// </summary>
    /// <param name="memoria">The list of memoranda to save.</param>
    Task SaveAsync(IReadOnlyList<Memorandum> memoria);

    /// <summary>
    /// Deletes a memorandum (soft delete).
    /// </summary>
    /// <param name="id">The unique identifier of the memorandum.</param>
    /// <returns>True if deleted; false if not found.</returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Queries memoranda using advanced filtering criteria.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <returns>List of memoranda matching the criteria.</returns>
    Task<IReadOnlyList<Memorandum>> QueryAsync(MemoryQuery query);
}
