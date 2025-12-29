namespace Narratum.Core;

/// <summary>
/// Generic repository interface for persistent storage.
/// Used as a port for the hexagonal architecture.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The identity type.</typeparam>
public interface IRepository<TEntity, TId>
{
    /// <summary>
    /// Retrieves an entity by its identifier.
    /// </summary>
    Task<Result<TEntity>> GetByIdAsync(TId id);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    Task<Result<IReadOnlyList<TEntity>>> GetAllAsync();

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    Task<Result<TEntity>> AddAsync(TEntity entity);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task<Result<TEntity>> UpdateAsync(TEntity entity);

    /// <summary>
    /// Deletes an entity by its identifier.
    /// </summary>
    Task<Result<Unit>> DeleteAsync(TId id);
}
