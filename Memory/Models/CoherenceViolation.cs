namespace Narratum.Memory;

/// <summary>
/// Record immutable représentant une violation de cohérence logique détectée.
/// Utilisée pour tracker les inconsistances entre les faits ou états canoniques.
/// </summary>
/// <param name="Id">Identifiant unique de la violation</param>
/// <param name="ViolationType">Type de violation (contradiction, séquence, etc.)</param>
/// <param name="Severity">Gravité de la violation (Info, Warning, Error)</param>
/// <param name="Description">Description textuelle de la violation</param>
/// <param name="InvolvedFactIds">IDs des faits impliqués dans la violation</param>
/// <param name="Resolution">Suggestions de résolution ou notes expliquant la violation</param>
/// <param name="MemoryLevel">Niveau hiérarchique auquel la violation a été détectée</param>
/// <param name="DetectedAt">Timestamp de la détection</param>
/// <param name="ResolvedAt">Timestamp de la résolution (null si non résolu)</param>
public sealed record CoherenceViolation(
    Guid Id,
    CoherenceViolationType ViolationType,
    CoherenceSeverity Severity,
    string Description,
    IReadOnlySet<Guid> InvolvedFactIds,
    string? Resolution = null,
    MemoryLevel? MemoryLevel = null,
    DateTime? DetectedAt = null,
    DateTime? ResolvedAt = null
)
{
    /// <summary>
    /// Crée une nouvelle violation de cohérence.
    /// </summary>
    public static CoherenceViolation Create(
        CoherenceViolationType violationType,
        CoherenceSeverity severity,
        string description,
        IEnumerable<Guid> involvedFactIds,
        string? resolution = null,
        MemoryLevel? memoryLevel = null)
    {
        return new CoherenceViolation(
            Id: Guid.NewGuid(),
            ViolationType: violationType,
            Severity: severity,
            Description: description,
            InvolvedFactIds: involvedFactIds.ToHashSet(),
            Resolution: resolution,
            MemoryLevel: memoryLevel,
            DetectedAt: DateTime.UtcNow,
            ResolvedAt: null
        );
    }

    /// <summary>
    /// Marque la violation comme résolue.
    /// </summary>
    public CoherenceViolation MarkResolved()
    {
        return this with { ResolvedAt = DateTime.UtcNow };
    }

    /// <summary>
    /// Indique si la violation a été résolue.
    /// </summary>
    public bool IsResolved => ResolvedAt.HasValue;

    /// <summary>
    /// Valide que la violation respecte les contraintes de base.
    /// </summary>
    public bool Validate()
    {
        // Description ne doit pas être vide
        if (string.IsNullOrWhiteSpace(Description))
            return false;

        // InvolvedFactIds doit contenir au moins un fait
        if (InvolvedFactIds.Count < 1)
            return false;

        // Si résolue, ResolvedAt doit être après DetectedAt
        if (IsResolved && DetectedAt.HasValue && ResolvedAt!.Value < DetectedAt.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Retourne la description complète de la violation avec sa résolution.
    /// </summary>
    public string GetFullDescription()
    {
        var parts = new List<string>
        {
            $"[{Severity}] {Description}",
            $"Implique {InvolvedFactIds.Count} fait(s)"
        };

        if (!string.IsNullOrWhiteSpace(Resolution))
            parts.Add($"Résolution: {Resolution}");

        if (IsResolved && ResolvedAt.HasValue)
            parts.Add($"Résolu le {ResolvedAt:g}");

        return string.Join(" | ", parts);
    }
}
