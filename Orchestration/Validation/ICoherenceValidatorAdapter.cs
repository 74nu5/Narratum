using Narratum.Memory;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Validation;

/// <summary>
/// Interface pour l'adaptateur de validation de cohérence.
///
/// Encapsule l'intégration avec le ICoherenceValidator de Phase 2
/// et traduit les résultats en types utilisables par l'orchestration.
/// </summary>
public interface ICoherenceValidatorAdapter
{
    /// <summary>
    /// Valide la cohérence d'une sortie par rapport au contexte narratif.
    /// </summary>
    /// <param name="output">La sortie à valider.</param>
    /// <param name="context">Le contexte narratif courant.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Le résultat de la validation de cohérence.</returns>
    Task<CoherenceValidationResult> ValidateAsync(
        RawOutput output,
        NarrativeContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide la cohérence de l'état canonique seul.
    /// </summary>
    /// <param name="canonicalState">L'état canonique à valider.</param>
    /// <returns>Le résultat de la validation.</returns>
    CoherenceValidationResult ValidateState(CanonicalState canonicalState);

    /// <summary>
    /// Valide une transition d'état.
    /// </summary>
    /// <param name="previousState">L'état précédent.</param>
    /// <param name="newState">Le nouvel état.</param>
    /// <returns>Le résultat de la validation.</returns>
    CoherenceValidationResult ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState);
}

/// <summary>
/// Résultat de la validation de cohérence.
/// </summary>
public sealed record CoherenceValidationResult(
    bool IsCoherent,
    IReadOnlyList<CoherenceIssue> Issues,
    IReadOnlyDictionary<string, object> Metadata)
{
    public static CoherenceValidationResult Coherent()
        => new(true, Array.Empty<CoherenceIssue>(), new Dictionary<string, object>());

    public static CoherenceValidationResult Incoherent(params CoherenceIssue[] issues)
        => new(false, issues, new Dictionary<string, object>());

    public static CoherenceValidationResult FromViolations(IReadOnlyList<CoherenceViolation> violations)
    {
        var issues = violations.Select(CoherenceIssue.FromViolation).ToList();
        var hasErrors = violations.Any(v => v.Severity == CoherenceSeverity.Error);
        return new CoherenceValidationResult(
            !hasErrors,
            issues,
            new Dictionary<string, object>
            {
                ["violationCount"] = violations.Count,
                ["errorCount"] = violations.Count(v => v.Severity == CoherenceSeverity.Error),
                ["warningCount"] = violations.Count(v => v.Severity == CoherenceSeverity.Warning)
            });
    }

    public bool HasErrors => Issues.Any(i => i.Severity == CoherenceIssueSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == CoherenceIssueSeverity.Warning);

    public IEnumerable<CoherenceIssue> Errors =>
        Issues.Where(i => i.Severity == CoherenceIssueSeverity.Error);

    public IEnumerable<CoherenceIssue> Warnings =>
        Issues.Where(i => i.Severity == CoherenceIssueSeverity.Warning);

    public CoherenceValidationResult Merge(CoherenceValidationResult other)
    {
        var allIssues = Issues.Concat(other.Issues).ToList();
        var merged = new Dictionary<string, object>(Metadata);
        foreach (var (key, value) in other.Metadata)
        {
            merged[key] = value;
        }

        return new CoherenceValidationResult(
            !allIssues.Any(i => i.Severity == CoherenceIssueSeverity.Error),
            allIssues,
            merged);
    }
}

/// <summary>
/// Problème de cohérence détecté.
/// </summary>
public sealed record CoherenceIssue(
    CoherenceIssueType IssueType,
    CoherenceIssueSeverity Severity,
    string Description,
    string? Resolution,
    IReadOnlySet<Guid> InvolvedFactIds)
{
    public static CoherenceIssue FromViolation(CoherenceViolation violation)
    {
        var issueType = violation.ViolationType switch
        {
            CoherenceViolationType.StatementContradiction => CoherenceIssueType.Contradiction,
            CoherenceViolationType.SequenceViolation => CoherenceIssueType.TimelineViolation,
            CoherenceViolationType.EntityInconsistency => CoherenceIssueType.EntityInconsistency,
            CoherenceViolationType.LocationInconsistency => CoherenceIssueType.LocationInconsistency,
            _ => CoherenceIssueType.Other
        };

        var severity = violation.Severity switch
        {
            CoherenceSeverity.Error => CoherenceIssueSeverity.Error,
            CoherenceSeverity.Warning => CoherenceIssueSeverity.Warning,
            _ => CoherenceIssueSeverity.Info
        };

        return new CoherenceIssue(
            issueType,
            severity,
            violation.Description,
            violation.Resolution,
            violation.InvolvedFactIds);
    }

    public static CoherenceIssue Contradiction(string description, string? resolution = null)
        => new(CoherenceIssueType.Contradiction, CoherenceIssueSeverity.Error,
            description, resolution, new HashSet<Guid>());

    public static CoherenceIssue TimelineViolation(string description, string? resolution = null)
        => new(CoherenceIssueType.TimelineViolation, CoherenceIssueSeverity.Error,
            description, resolution, new HashSet<Guid>());

    public static CoherenceIssue DeadCharacterAction(string characterName, string action)
        => new(CoherenceIssueType.DeadCharacterAction, CoherenceIssueSeverity.Error,
            $"Dead character '{characterName}' appears to be performing action: {action}",
            $"Remove or rephrase actions attributed to {characterName}",
            new HashSet<Guid>());
}

/// <summary>
/// Types de problèmes de cohérence.
/// </summary>
public enum CoherenceIssueType
{
    /// <summary>
    /// Contradiction entre faits.
    /// </summary>
    Contradiction,

    /// <summary>
    /// Violation de la timeline.
    /// </summary>
    TimelineViolation,

    /// <summary>
    /// Incohérence d'entité.
    /// </summary>
    EntityInconsistency,

    /// <summary>
    /// Incohérence de lieu.
    /// </summary>
    LocationInconsistency,

    /// <summary>
    /// Personnage mort agissant.
    /// </summary>
    DeadCharacterAction,

    /// <summary>
    /// Autre problème.
    /// </summary>
    Other
}

/// <summary>
/// Sévérité d'un problème de cohérence.
/// </summary>
public enum CoherenceIssueSeverity
{
    /// <summary>
    /// Information seulement.
    /// </summary>
    Info,

    /// <summary>
    /// Avertissement.
    /// </summary>
    Warning,

    /// <summary>
    /// Erreur bloquante.
    /// </summary>
    Error
}
