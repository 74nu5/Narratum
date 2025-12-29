namespace Narratum.Memory.Store.Entities;

using System;

/// <summary>
/// Entity Framework mapping for CoherenceViolation domain type.
/// Provides persistent storage of detected logical inconsistencies.
/// </summary>
public class CoherenceViolationEntity
{
    /// <summary>
    /// Unique identifier for this violation (Guid as string).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key: The memorandum this violation was detected in.
    /// </summary>
    public string MemorandumId { get; set; } = string.Empty;

    /// <summary>
    /// Related memorandum entity (navigation property).
    /// </summary>
    public MemorandumEntity? Memorandum { get; set; }

    /// <summary>
    /// Human-readable description of the violation.
    /// Example: "Aric cannot be both alive and dead"
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of violation (0=StatementContradiction, 1=SequenceViolation, 2=EntityInconsistency, 3=LocationInconsistency).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// First conflicting fact (serialized JSON).
    /// </summary>
    public string ConflictingFact1 { get; set; } = string.Empty;

    /// <summary>
    /// Second conflicting fact (serialized JSON).
    /// </summary>
    public string ConflictingFact2 { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this violation was detected.
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Severity level (0=Info, 1=Warning, 2=Error).
    /// </summary>
    public int Severity { get; set; }

    /// <summary>
    /// Full serialized domain model (JSON).
    /// </summary>
    public string SerializedData { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp for audit purposes.
    /// </summary>
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag for retention/archival.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this record was logically deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
