namespace Narratum.Memory.Store.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// Entity Framework mapping for Memorandum domain type.
/// Provides persistent storage of narrative memories in SQLite.
/// </summary>
public class MemorandumEntity
{
    /// <summary>
    /// Unique identifier for this memorandum (Guid as string).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key: The story world this memorandum belongs to.
    /// </summary>
    public string WorldId { get; set; } = string.Empty;

    /// <summary>
    /// Title of the memorandum.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description of the memorandum content.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Serialized canonical states (JSON) organized by memory level.
    /// </summary>
    public string CanonicalStatesData { get; set; } = string.Empty;

    /// <summary>
    /// Serialized coherence violations (JSON).
    /// </summary>
    public string ViolationsData { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this memorandum was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when this memorandum was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Full serialized domain model (JSON) for complete persistence.
    /// </summary>
    public string SerializedData { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the memorandum for integrity verification.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp for audit purposes.
    /// </summary>
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp for audit purposes.
    /// </summary>
    public DateTime? AuditUpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag for retention/archival.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this record was logically deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Related coherence violations detected in this memorandum.
    /// </summary>
    public ICollection<CoherenceViolationEntity> Violations { get; set; } = 
        new List<CoherenceViolationEntity>();
}
