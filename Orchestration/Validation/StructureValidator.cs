using Microsoft.Extensions.Logging;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Validation;

/// <summary>
/// Implémentation du validateur structurel.
///
/// Vérifie que les réponses des agents ont la structure attendue :
/// - Contenu non vide
/// - Longueur dans les limites configurées
/// - Pas de patterns interdits
/// </summary>
public sealed class StructureValidator : IStructureValidator
{
    private readonly StructureValidatorConfig _config;
    private readonly ILogger<StructureValidator>? _logger;

    public StructureValidator(
        StructureValidatorConfig? config = null,
        ILogger<StructureValidator>? logger = null)
    {
        _config = config ?? StructureValidatorConfig.Default;
        _logger = logger;
    }

    public StructureValidationResult Validate(RawOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _logger?.LogDebug("Validating structure of {Count} responses", output.Responses.Count);

        var errors = new List<StructureValidationError>();
        var warnings = new List<StructureValidationWarning>();

        // Vérifier qu'il y a au moins une réponse
        if (output.Responses.Count == 0)
        {
            errors.Add(StructureValidationError.NoResponses());
            return StructureValidationResult.Invalid(errors.ToArray());
        }

        // Valider chaque réponse
        foreach (var (agentType, response) in output.Responses)
        {
            ValidateAgentResponse(agentType, response, errors, warnings);
        }

        var isValid = errors.Count == 0;

        _logger?.LogDebug(
            "Structure validation completed: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
            isValid, errors.Count, warnings.Count);

        return new StructureValidationResult(isValid, errors, warnings);
    }

    public StructureValidationResult ValidateResponse(AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var errors = new List<StructureValidationError>();
        var warnings = new List<StructureValidationWarning>();

        ValidateAgentResponse(response.Agent, response, errors, warnings);

        return new StructureValidationResult(errors.Count == 0, errors, warnings);
    }

    private void ValidateAgentResponse(
        AgentType agentType,
        AgentResponse response,
        List<StructureValidationError> errors,
        List<StructureValidationWarning> warnings)
    {
        // Vérifier si l'agent a échoué
        if (!response.Success)
        {
            errors.Add(StructureValidationError.AgentFailed(
                agentType, response.ErrorMessage ?? "Unknown error"));
            return;
        }

        // Vérifier le contenu vide
        if (string.IsNullOrWhiteSpace(response.Content))
        {
            errors.Add(StructureValidationError.Empty(agentType));
            return;
        }

        var content = response.Content;
        var contentLength = content.Length;

        // Vérifier la longueur minimale
        var minLength = GetMinLengthForAgent(agentType);
        if (contentLength < minLength)
        {
            errors.Add(StructureValidationError.TooShort(agentType, contentLength, minLength));
        }

        // Vérifier la longueur maximale
        var maxLength = GetMaxLengthForAgent(agentType);
        if (contentLength > maxLength)
        {
            if (_config.TreatMaxLengthAsError)
            {
                errors.Add(StructureValidationError.TooLong(agentType, contentLength, maxLength));
            }
            else
            {
                warnings.Add(new StructureValidationWarning(
                    agentType,
                    $"Content exceeds recommended length ({contentLength} chars, max {maxLength})",
                    "Consider trimming the content"));
            }
        }

        // Vérifier les patterns interdits
        foreach (var pattern in _config.ForbiddenPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                if (_config.TreatForbiddenPatternsAsError)
                {
                    errors.Add(StructureValidationError.InvalidFormat(
                        agentType, $"Contains forbidden pattern: '{pattern}'"));
                }
                else
                {
                    warnings.Add(new StructureValidationWarning(
                        agentType,
                        $"Content contains potentially problematic pattern: '{pattern}'"));
                }
            }
        }

        // Vérifier les patterns requis (si configurés)
        foreach (var (agent, requiredPatterns) in _config.RequiredPatterns)
        {
            if (agent == agentType)
            {
                foreach (var pattern in requiredPatterns)
                {
                    if (!content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add(new StructureValidationWarning(
                            agentType,
                            $"Missing expected pattern: '{pattern}'"));
                    }
                }
            }
        }
    }

    private int GetMinLengthForAgent(AgentType agent)
    {
        return _config.MinLengthPerAgent.TryGetValue(agent, out var length)
            ? length
            : _config.DefaultMinLength;
    }

    private int GetMaxLengthForAgent(AgentType agent)
    {
        return _config.MaxLengthPerAgent.TryGetValue(agent, out var length)
            ? length
            : _config.DefaultMaxLength;
    }
}

/// <summary>
/// Configuration du validateur structurel.
/// </summary>
public sealed record StructureValidatorConfig
{
    /// <summary>
    /// Longueur minimale par défaut du contenu.
    /// </summary>
    public int DefaultMinLength { get; init; } = 10;

    /// <summary>
    /// Longueur maximale par défaut du contenu.
    /// </summary>
    public int DefaultMaxLength { get; init; } = 10000;

    /// <summary>
    /// Longueurs minimales par type d'agent.
    /// </summary>
    public IReadOnlyDictionary<AgentType, int> MinLengthPerAgent { get; init; }
        = new Dictionary<AgentType, int>();

    /// <summary>
    /// Longueurs maximales par type d'agent.
    /// </summary>
    public IReadOnlyDictionary<AgentType, int> MaxLengthPerAgent { get; init; }
        = new Dictionary<AgentType, int>();

    /// <summary>
    /// Patterns interdits dans le contenu.
    /// </summary>
    public IReadOnlyList<string> ForbiddenPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Patterns requis par type d'agent.
    /// </summary>
    public IReadOnlyDictionary<AgentType, IReadOnlyList<string>> RequiredPatterns { get; init; }
        = new Dictionary<AgentType, IReadOnlyList<string>>();

    /// <summary>
    /// Traiter les dépassements de longueur max comme des erreurs (sinon avertissements).
    /// </summary>
    public bool TreatMaxLengthAsError { get; init; } = false;

    /// <summary>
    /// Traiter les patterns interdits comme des erreurs (sinon avertissements).
    /// </summary>
    public bool TreatForbiddenPatternsAsError { get; init; } = false;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static StructureValidatorConfig Default => new();

    /// <summary>
    /// Configuration stricte pour les tests.
    /// </summary>
    public static StructureValidatorConfig Strict => new()
    {
        DefaultMinLength = 50,
        DefaultMaxLength = 5000,
        ForbiddenPatterns = new[] { "[ERROR]", "[TODO]", "PLACEHOLDER", "undefined", "null" },
        TreatMaxLengthAsError = true,
        TreatForbiddenPatternsAsError = true
    };

    /// <summary>
    /// Configuration pour la narration.
    /// </summary>
    public static StructureValidatorConfig Narrative => new()
    {
        DefaultMinLength = 100,
        DefaultMaxLength = 3000,
        MinLengthPerAgent = new Dictionary<AgentType, int>
        {
            [AgentType.Summary] = 50,
            [AgentType.Narrator] = 150,
            [AgentType.Character] = 50,
            [AgentType.Consistency] = 20
        },
        MaxLengthPerAgent = new Dictionary<AgentType, int>
        {
            [AgentType.Summary] = 1000,
            [AgentType.Narrator] = 3000,
            [AgentType.Character] = 2000,
            [AgentType.Consistency] = 1000
        }
    };
}
