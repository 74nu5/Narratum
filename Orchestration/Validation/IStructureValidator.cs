namespace Narratum.Orchestration.Validation;

using Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour la validation structurelle des sorties des agents.
///
/// Vérifie que les réponses ont la structure attendue :
/// - Contenu non vide
/// - Longueur appropriée
/// - Format correct
/// </summary>
public interface IStructureValidator
{
    /// <summary>
    /// Valide la structure d'une sortie brute.
    /// </summary>
    /// <param name="output">La sortie à valider.</param>
    /// <returns>Le résultat de la validation.</returns>
    StructureValidationResult Validate(RawOutput output);

    /// <summary>
    /// Valide la structure d'une réponse d'agent individuelle.
    /// </summary>
    /// <param name="response">La réponse à valider.</param>
    /// <returns>Le résultat de la validation.</returns>
    StructureValidationResult ValidateResponse(AgentResponse response);
}

/// <summary>
/// Résultat de la validation structurelle.
/// </summary>
public sealed record StructureValidationResult(
    bool IsValid,
    IReadOnlyList<StructureValidationError> Errors,
    IReadOnlyList<StructureValidationWarning> Warnings)
{
    public static StructureValidationResult Valid()
        => new(true, Array.Empty<StructureValidationError>(), Array.Empty<StructureValidationWarning>());

    public static StructureValidationResult Invalid(params StructureValidationError[] errors)
        => new(false, errors, Array.Empty<StructureValidationWarning>());

    public static StructureValidationResult WithWarnings(params StructureValidationWarning[] warnings)
        => new(true, Array.Empty<StructureValidationError>(), warnings);

    public StructureValidationResult Merge(StructureValidationResult other)
    {
        var errors = Errors.Concat(other.Errors).ToList();
        var warnings = Warnings.Concat(other.Warnings).ToList();
        return new StructureValidationResult(
            errors.Count == 0,
            errors,
            warnings);
    }
}

/// <summary>
/// Erreur de validation structurelle.
/// </summary>
public sealed record StructureValidationError(
    AgentType? Agent,
    StructureErrorType ErrorType,
    string Message,
    string? SuggestedFix = null)
{
    /// <summary>
    /// Sévérité dérivée du type d'erreur structurelle.
    /// </summary>
    public ErrorSeverity Severity => ErrorType switch
    {
        StructureErrorType.EmptyContent => ErrorSeverity.Critical,
        StructureErrorType.NoResponses => ErrorSeverity.Critical,
        StructureErrorType.AgentFailed => ErrorSeverity.Critical,
        StructureErrorType.ContentTooShort => ErrorSeverity.Major,
        StructureErrorType.ContentTooLong => ErrorSeverity.Major,
        StructureErrorType.InvalidFormat => ErrorSeverity.Major,
        _ => ErrorSeverity.Minor
    };

    public static StructureValidationError Empty(AgentType agent)
        => new(agent, StructureErrorType.EmptyContent,
            $"Agent {agent} returned empty content",
            "Agent must generate non-empty content");

    public static StructureValidationError TooShort(AgentType agent, int actual, int minimum)
        => new(agent, StructureErrorType.ContentTooShort,
            $"Agent {agent} content too short ({actual} chars, min {minimum})",
            "Generate more detailed content");

    public static StructureValidationError TooLong(AgentType agent, int actual, int maximum)
        => new(agent, StructureErrorType.ContentTooLong,
            $"Agent {agent} content too long ({actual} chars, max {maximum})",
            "Reduce content length");

    public static StructureValidationError NoResponses()
        => new(null, StructureErrorType.NoResponses,
            "No agent responses received",
            "Ensure at least one agent is executed");

    public static StructureValidationError AgentFailed(AgentType agent, string error)
        => new(agent, StructureErrorType.AgentFailed,
            $"Agent {agent} failed: {error}",
            "Retry the agent execution");

    public static StructureValidationError InvalidFormat(AgentType agent, string details)
        => new(agent, StructureErrorType.InvalidFormat,
            $"Agent {agent} content has invalid format: {details}",
            "Ensure content follows expected format");
}

/// <summary>
/// Avertissement de validation structurelle.
/// </summary>
public sealed record StructureValidationWarning(
    AgentType? Agent,
    string Message,
    string? Context = null);

/// <summary>
/// Types d'erreurs structurelles.
/// </summary>
public enum StructureErrorType
{
    /// <summary>
    /// Contenu vide.
    /// </summary>
    EmptyContent,

    /// <summary>
    /// Contenu trop court.
    /// </summary>
    ContentTooShort,

    /// <summary>
    /// Contenu trop long.
    /// </summary>
    ContentTooLong,

    /// <summary>
    /// Aucune réponse reçue.
    /// </summary>
    NoResponses,

    /// <summary>
    /// L'agent a échoué.
    /// </summary>
    AgentFailed,

    /// <summary>
    /// Format de contenu invalide.
    /// </summary>
    InvalidFormat
}
