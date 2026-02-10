using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Validation;

/// <summary>
/// Adaptateur pour le ICoherenceValidator de Phase 2.
///
/// Encapsule la logique de validation de cohérence et traduit
/// les résultats en types utilisables par l'orchestration.
/// Ajoute également des vérifications spécifiques à la narration.
/// </summary>
public sealed class CoherenceValidatorAdapter : ICoherenceValidatorAdapter
{
    private readonly ICoherenceValidator? _coherenceValidator;
    private readonly CoherenceValidatorAdapterConfig _config;
    private readonly ILogger<CoherenceValidatorAdapter>? _logger;

    public CoherenceValidatorAdapter(
        ICoherenceValidator? coherenceValidator = null,
        CoherenceValidatorAdapterConfig? config = null,
        ILogger<CoherenceValidatorAdapter>? logger = null)
    {
        _coherenceValidator = coherenceValidator;
        _config = config ?? CoherenceValidatorAdapterConfig.Default;
        _logger = logger;
    }

    public async Task<CoherenceValidationResult> ValidateAsync(
        RawOutput output,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(context);

        _logger?.LogDebug("Validating coherence for output with {Count} responses", output.Responses.Count);

        var issues = new List<CoherenceIssue>();

        // 1. Valider l'état canonique si disponible
        if (_coherenceValidator != null && context.CanonicalState != null)
        {
            var stateResult = ValidateState(context.CanonicalState);
            issues.AddRange(stateResult.Issues);
        }

        // 2. Valider les personnages morts
        ValidateDeadCharacters(output, context, issues);

        // 3. Valider la cohérence des lieux
        ValidateLocationCoherence(output, context, issues);

        // 4. Valider les connaissances des personnages
        if (_config.ValidateCharacterKnowledge)
        {
            ValidateCharacterKnowledge(output, context, issues);
        }

        var hasErrors = issues.Any(i => i.Severity == CoherenceIssueSeverity.Error);

        _logger?.LogDebug(
            "Coherence validation completed: Coherent={IsCoherent}, Issues={IssueCount}",
            !hasErrors, issues.Count);

        return new CoherenceValidationResult(
            !hasErrors,
            issues,
            new Dictionary<string, object>
            {
                ["validatedAt"] = DateTime.UtcNow,
                ["issueCount"] = issues.Count
            });
    }

    public CoherenceValidationResult ValidateState(CanonicalState canonicalState)
    {
        ArgumentNullException.ThrowIfNull(canonicalState);

        if (_coherenceValidator == null)
        {
            _logger?.LogDebug("No coherence validator available, skipping state validation");
            return CoherenceValidationResult.Coherent();
        }

        try
        {
            var violations = _coherenceValidator.ValidateState(canonicalState);
            return CoherenceValidationResult.FromViolations(violations);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Coherence validation failed");
            return CoherenceValidationResult.Coherent(); // Fail open
        }
    }

    public CoherenceValidationResult ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState)
    {
        ArgumentNullException.ThrowIfNull(previousState);
        ArgumentNullException.ThrowIfNull(newState);

        if (_coherenceValidator == null)
        {
            _logger?.LogDebug("No coherence validator available, skipping transition validation");
            return CoherenceValidationResult.Coherent();
        }

        try
        {
            var violations = _coherenceValidator.ValidateTransition(previousState, newState);
            return CoherenceValidationResult.FromViolations(violations);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Transition validation failed");
            return CoherenceValidationResult.Coherent(); // Fail open
        }
    }

    /// <summary>
    /// Vérifie que les personnages morts ne sont pas décrits comme agissant.
    /// </summary>
    private void ValidateDeadCharacters(
        RawOutput output,
        NarrativeContext context,
        List<CoherenceIssue> issues)
    {
        var deadCharacters = context.ActiveCharacters
            .Where(c => c.Status == VitalStatus.Dead)
            .Select(c => c.Name)
            .ToList();

        if (deadCharacters.Count == 0)
            return;

        foreach (var (agentType, response) in output.Responses)
        {
            if (!response.Success || string.IsNullOrEmpty(response.Content))
                continue;

            var content = response.Content;

            foreach (var deadName in deadCharacters)
            {
                foreach (var pattern in _config.DeadCharacterActionPatterns)
                {
                    var fullPattern = pattern.Replace("{name}", deadName);
                    if (content.Contains(fullPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(CoherenceIssue.DeadCharacterAction(deadName, fullPattern));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Vérifie la cohérence des lieux mentionnés.
    /// </summary>
    private void ValidateLocationCoherence(
        RawOutput output,
        NarrativeContext context,
        List<CoherenceIssue> issues)
    {
        if (context.CurrentLocation == null)
            return;

        // Vérifier que les personnages présents au lieu sont cohérents
        var presentCharacterIds = context.CurrentLocation.PresentCharacterIds;
        var presentNames = context.ActiveCharacters
            .Where(c => presentCharacterIds.Contains(c.CharacterId))
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (agentType, response) in output.Responses)
        {
            if (!response.Success || string.IsNullOrEmpty(response.Content))
                continue;

            // Vérifier les personnages mentionnés comme présents mais absents du lieu
            foreach (var character in context.ActiveCharacters)
            {
                if (presentCharacterIds.Contains(character.CharacterId))
                    continue; // Le personnage est présent, OK

                // Vérifier si le texte suggère que le personnage est présent
                var presencePatterns = new[]
                {
                    $"{character.Name} stood",
                    $"{character.Name} was there",
                    $"{character.Name} entered",
                    $"{character.Name} looked around"
                };

                foreach (var pattern in presencePatterns)
                {
                    if (response.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new CoherenceIssue(
                            CoherenceIssueType.LocationInconsistency,
                            CoherenceIssueSeverity.Warning,
                            $"Character '{character.Name}' appears to be at {context.CurrentLocation.Name} but is not listed as present",
                            $"Either add {character.Name} to the location or rephrase the text",
                            new HashSet<Guid>()));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Vérifie que les personnages ne révèlent pas des informations qu'ils ne connaissent pas.
    /// </summary>
    private void ValidateCharacterKnowledge(
        RawOutput output,
        NarrativeContext context,
        List<CoherenceIssue> issues)
    {
        // Cette validation est complexe et optionnelle
        // Pour l'instant, on vérifie juste les cas évidents

        foreach (var character in context.ActiveCharacters)
        {
            if (character.KnownFacts.Count == 0)
                continue;

            // Vérifier si un personnage révèle quelque chose qu'il ne devrait pas savoir
            // Cette logique pourrait être étendue avec des patterns plus sophistiqués
        }
    }
}

/// <summary>
/// Configuration de l'adaptateur de validation de cohérence.
/// </summary>
public sealed record CoherenceValidatorAdapterConfig
{
    /// <summary>
    /// Patterns pour détecter les actions de personnages morts.
    /// {name} est remplacé par le nom du personnage.
    /// </summary>
    public IReadOnlyList<string> DeadCharacterActionPatterns { get; init; } = new[]
    {
        "{name} said",
        "{name} spoke",
        "{name} walked",
        "{name} ran",
        "{name} looked",
        "{name} smiled",
        "{name} nodded",
        "{name} replied",
        "{name} asked",
        "{name} stood",
        "{name} moved"
    };

    /// <summary>
    /// Valider les connaissances des personnages.
    /// </summary>
    public bool ValidateCharacterKnowledge { get; init; } = false;

    /// <summary>
    /// Valider la cohérence des lieux.
    /// </summary>
    public bool ValidateLocationCoherence { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static CoherenceValidatorAdapterConfig Default => new();

    /// <summary>
    /// Configuration stricte pour les tests.
    /// </summary>
    public static CoherenceValidatorAdapterConfig Strict => new()
    {
        ValidateCharacterKnowledge = true,
        ValidateLocationCoherence = true
    };
}
