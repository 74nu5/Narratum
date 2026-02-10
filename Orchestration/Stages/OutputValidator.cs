using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Implémentation de l'OutputValidator.
///
/// Valide les sorties des agents en vérifiant :
/// - Structure (contenu non vide, longueur minimale)
/// - Cohérence (intégration avec ICoherenceValidator de Phase 2)
/// - Logique narrative (personnages morts ne parlent pas, etc.)
/// </summary>
public class OutputValidator : IOutputValidator
{
    private readonly ICoherenceValidator? _coherenceValidator;
    private readonly ILogger<OutputValidator>? _logger;
    private readonly OutputValidatorConfig _config;

    public OutputValidator(
        ICoherenceValidator? coherenceValidator = null,
        OutputValidatorConfig? config = null,
        ILogger<OutputValidator>? logger = null)
    {
        _coherenceValidator = coherenceValidator;
        _config = config ?? OutputValidatorConfig.Default;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(
        RawOutput output,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(context);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        _logger?.LogDebug("Validating output with {ResponseCount} responses", output.Responses.Count);

        // 1. Validation structurelle
        ValidateStructure(output, errors, warnings);

        // 2. Validation du contenu
        ValidateContent(output, context, errors, warnings);

        // 3. Validation de la cohérence (si disponible)
        if (_coherenceValidator != null)
        {
            await ValidateCoherenceAsync(output, context, errors, warnings, cancellationToken);
        }

        // 4. Validation narrative
        ValidateNarrativeLogic(output, context, errors, warnings);

        var isValid = !errors.Any(e => e.Severity == ErrorSeverity.Critical);

        _logger?.LogDebug(
            "Validation completed: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
            isValid, errors.Count, warnings.Count);

        return new ValidationResult(
            isValid,
            errors,
            warnings,
            new Dictionary<string, object>
            {
                ["validatedAt"] = DateTime.UtcNow,
                ["responseCount"] = output.Responses.Count
            });
    }

    /// <summary>
    /// Valide la structure des sorties.
    /// </summary>
    private void ValidateStructure(
        RawOutput output,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        // Vérifier qu'il y a au moins une réponse
        if (output.Responses.Count == 0)
        {
            errors.Add(ValidationError.Critical("No agent responses received"));
            return;
        }

        // Vérifier les réponses individuelles
        foreach (var (agentType, response) in output.Responses)
        {
            if (!response.Success)
            {
                errors.Add(ValidationError.Major(
                    $"Agent {agentType} failed: {response.ErrorMessage}",
                    "Retry the agent execution"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                errors.Add(ValidationError.Critical(
                    $"Agent {agentType} returned empty content",
                    "Agent must generate non-empty content"));
            }
            else if (response.Content.Length < _config.MinContentLength)
            {
                errors.Add(ValidationError.Major(
                    $"Agent {agentType} content too short ({response.Content.Length} chars, min {_config.MinContentLength})",
                    "Generate more detailed content"));
            }
        }
    }

    /// <summary>
    /// Valide le contenu des sorties.
    /// </summary>
    private void ValidateContent(
        RawOutput output,
        NarrativeContext context,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        foreach (var (agentType, response) in output.Responses)
        {
            if (!response.Success || string.IsNullOrEmpty(response.Content))
                continue;

            var content = response.Content;

            // Vérifier les mots interdits
            foreach (var forbidden in _config.ForbiddenPatterns)
            {
                if (content.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(new ValidationWarning(
                        $"Content contains potentially problematic pattern: '{forbidden}'",
                        $"Agent: {agentType}"));
                }
            }

            // Vérifier la longueur maximale
            if (content.Length > _config.MaxContentLength)
            {
                warnings.Add(new ValidationWarning(
                    $"Content exceeds recommended length ({content.Length} chars, max {_config.MaxContentLength})",
                    $"Agent: {agentType}"));
            }
        }
    }

    /// <summary>
    /// Valide la cohérence avec le système de mémoire.
    /// </summary>
    private Task ValidateCoherenceAsync(
        RawOutput output,
        NarrativeContext context,
        List<ValidationError> errors,
        List<ValidationWarning> warnings,
        CancellationToken cancellationToken)
    {
        if (_coherenceValidator == null || context.CanonicalState == null)
            return Task.CompletedTask;

        try
        {
            // ValidateState est synchrone
            var violations = _coherenceValidator.ValidateState(context.CanonicalState);

            foreach (var violation in violations)
            {
                var severity = violation.Severity switch
                {
                    CoherenceSeverity.Error => ErrorSeverity.Critical,
                    CoherenceSeverity.Warning => ErrorSeverity.Major,
                    _ => ErrorSeverity.Minor
                };

                if (severity == ErrorSeverity.Critical)
                {
                    errors.Add(new ValidationError(
                        $"Coherence violation: {violation.Description}",
                        severity,
                        violation.Resolution));
                }
                else
                {
                    warnings.Add(new ValidationWarning(
                        $"Coherence warning: {violation.Description}",
                        violation.Resolution));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Coherence validation failed, continuing without it");
            warnings.Add(new ValidationWarning(
                "Coherence validation could not be performed",
                ex.Message));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Valide la logique narrative.
    /// </summary>
    private void ValidateNarrativeLogic(
        RawOutput output,
        NarrativeContext context,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        // Vérifier que les personnages morts ne sont pas mentionnés comme actifs
        var deadCharacters = context.ActiveCharacters
            .Where(c => c.Status == VitalStatus.Dead)
            .Select(c => c.Name)
            .ToList();

        foreach (var (agentType, response) in output.Responses)
        {
            if (!response.Success || string.IsNullOrEmpty(response.Content))
                continue;

            // Vérifier les personnages morts parlant/agissant
            foreach (var deadName in deadCharacters)
            {
                // Patterns qui suggèrent qu'un personnage mort agit
                var actionPatterns = new[]
                {
                    $"{deadName} said",
                    $"{deadName} spoke",
                    $"{deadName} walked",
                    $"{deadName} ran",
                    $"{deadName} looked"
                };

                foreach (var pattern in actionPatterns)
                {
                    if (response.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(ValidationError.Critical(
                            $"Dead character '{deadName}' appears to be performing actions",
                            $"Remove or rephrase actions attributed to {deadName}"));
                    }
                }
            }
        }

        // Vérifier la cohérence des lieux
        if (context.CurrentLocation != null)
        {
            var locationName = context.CurrentLocation.Name;

            // Avertir si le lieu n'est jamais mentionné (optionnel)
            var anyMentionsLocation = output.Responses.Values
                .Where(r => r.Success && !string.IsNullOrEmpty(r.Content))
                .Any(r => r.Content.Contains(locationName, StringComparison.OrdinalIgnoreCase));

            if (!anyMentionsLocation && context.ActiveCharacters.Count > 0)
            {
                warnings.Add(new ValidationWarning(
                    $"Current location '{locationName}' not mentioned in output",
                    "Consider adding location context"));
            }
        }
    }
}

/// <summary>
/// Configuration pour le validateur de sortie.
/// </summary>
public sealed record OutputValidatorConfig
{
    /// <summary>
    /// Longueur minimale du contenu (en caractères).
    /// </summary>
    public int MinContentLength { get; init; } = 10;

    /// <summary>
    /// Longueur maximale recommandée du contenu.
    /// </summary>
    public int MaxContentLength { get; init; } = 10000;

    /// <summary>
    /// Patterns interdits dans le contenu.
    /// </summary>
    public IReadOnlyList<string> ForbiddenPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static OutputValidatorConfig Default => new();

    /// <summary>
    /// Configuration stricte pour les tests.
    /// </summary>
    public static OutputValidatorConfig Strict => new()
    {
        MinContentLength = 50,
        MaxContentLength = 5000,
        ForbiddenPatterns = new[] { "[ERROR]", "[TODO]", "PLACEHOLDER" }
    };
}
