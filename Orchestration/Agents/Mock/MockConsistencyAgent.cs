using System.Diagnostics;
using System.Text;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents.Mock;

/// <summary>
/// Implémentation mock de IConsistencyAgent.
///
/// Effectue des vérifications basiques de cohérence sans LLM réel.
/// Utilisé pour valider l'architecture du système.
/// </summary>
public sealed class MockConsistencyAgent : IConsistencyAgent
{
    private readonly ILlmClient _llmClient;
    private readonly MockConsistencyConfig _config;

    public AgentType Type => AgentType.Consistency;
    public string Name => "MockConsistencyAgent";

    public MockConsistencyAgent(ILlmClient llmClient, MockConsistencyConfig? config = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? MockConsistencyConfig.Default;
    }

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        // Vérifier la cohérence si l'état canonique est disponible
        if (context.CanonicalState == null)
        {
            stopwatch.Stop();
            return Result<AgentResponse>.Ok(AgentResponse.CreateSuccess(
                Type,
                "No canonical state available for consistency check. Assuming consistent.",
                stopwatch.Elapsed)
                .WithMetadata("mock", true)
                .WithMetadata("skipped", true));
        }

        var checkResult = await CheckConsistencyAsync(
            prompt.UserPrompt,
            context.CanonicalState,
            cancellationToken);

        stopwatch.Stop();

        if (checkResult is Result<ConsistencyCheck>.Success success)
        {
            var check = success.Value;
            var response = FormatConsistencyResult(check);

            return Result<AgentResponse>.Ok(AgentResponse.CreateSuccess(
                Type,
                response,
                stopwatch.Elapsed)
                .WithMetadata("isConsistent", check.IsConsistent)
                .WithMetadata("issueCount", check.Issues.Count)
                .WithMetadata("mock", true));
        }
        else if (checkResult is Result<ConsistencyCheck>.Failure failure)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateFailure(
                Type,
                failure.Message,
                stopwatch.Elapsed));
        }

        return Result<AgentResponse>.Fail("Unknown error in MockConsistencyAgent");
    }

    public bool CanHandle(NarrativeIntent intent)
    {
        // L'agent de cohérence peut gérer toutes les intentions
        // car il vérifie la sortie, pas l'intention
        return true;
    }

    public Task<Result<ConsistencyCheck>> CheckConsistencyAsync(
        string generatedText,
        CanonicalState canonicalState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(generatedText))
        {
            return Task.FromResult(Result<ConsistencyCheck>.Ok(
                ConsistencyCheck.Consistent()));
        }

        var issues = new List<ConsistencyIssue>();

        // Vérifications basiques
        CheckDeadCharacters(generatedText, canonicalState, issues);
        CheckKnownFacts(generatedText, canonicalState, issues);

        var isConsistent = !issues.Any(i => i.Severity == IssueSeverity.Severe);
        var confidence = issues.Count == 0 ? 1.0 : Math.Max(0.0, 1.0 - (issues.Count * 0.2));

        return Task.FromResult(Result<ConsistencyCheck>.Ok(
            new ConsistencyCheck(isConsistent, issues, confidence)));
    }

    public Task<Result<string>> SuggestCorrectionsAsync(
        string text,
        IReadOnlyList<CoherenceViolation> violations,
        CancellationToken cancellationToken = default)
    {
        if (violations == null || violations.Count == 0)
        {
            return Task.FromResult(Result<string>.Ok(text));
        }

        var sb = new StringBuilder();
        sb.AppendLine("Suggested corrections:");
        sb.AppendLine();

        foreach (var violation in violations)
        {
            sb.AppendLine($"- Issue: {violation.Description}");
            if (!string.IsNullOrEmpty(violation.Resolution))
            {
                sb.AppendLine($"  Suggestion: {violation.Resolution}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Original text should be revised to address these issues.");

        return Task.FromResult(Result<string>.Ok(sb.ToString()));
    }

    /// <summary>
    /// Vérifie si des personnages morts sont mentionnés comme agissant.
    /// </summary>
    private void CheckDeadCharacters(
        string text,
        CanonicalState state,
        List<ConsistencyIssue> issues)
    {
        // Dans une vraie implémentation, on analyserait le texte
        // Ici, on fait une vérification simplifiée basée sur les Facts

        var deadPatterns = new[]
        {
            "said", "spoke", "walked", "ran", "looked", "moved",
            "stood", "sat", "reached", "grabbed"
        };

        // Extraire les personnages morts des Facts
        var deathFacts = state.Facts
            .Where(f => f.FactType == FactType.CharacterState &&
                       f.Content.Contains("dead", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var deathFact in deathFacts)
        {
            // Extraire les noms des entités référencées dans ce fait de mort
            foreach (var entityName in deathFact.EntityReferences)
            {
                foreach (var pattern in deadPatterns)
                {
                    var searchPattern = $"{entityName} {pattern}";
                    if (text.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(ConsistencyIssue.Severe(
                            $"Dead character '{entityName}' appears to be performing actions",
                            searchPattern,
                            $"Remove or rephrase actions attributed to {entityName}"));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Vérifie la cohérence avec les faits connus.
    /// </summary>
    private void CheckKnownFacts(
        string text,
        CanonicalState state,
        List<ConsistencyIssue> issues)
    {
        // Vérification simplifiée des faits
        // Dans une vraie implémentation, on utiliserait du NLP

        // Vérifier les contradictions de lieu
        if (_config.CheckLocationConsistency)
        {
            // Placeholder pour la vérification de lieu
        }

        // Vérifier les contradictions de relation
        if (_config.CheckRelationshipConsistency)
        {
            // Placeholder pour la vérification de relation
        }
    }

    /// <summary>
    /// Formate le résultat de la vérification.
    /// </summary>
    private string FormatConsistencyResult(ConsistencyCheck check)
    {
        var sb = new StringBuilder();

        if (check.IsConsistent)
        {
            sb.AppendLine("Consistency check passed.");
            sb.AppendLine($"Confidence: {check.ConfidenceScore:P0}");
        }
        else
        {
            sb.AppendLine("Consistency issues detected:");
            foreach (var issue in check.Issues)
            {
                sb.AppendLine($"- [{issue.Severity}] {issue.Description}");
                if (issue.SuggestedFix != null)
                {
                    sb.AppendLine($"  Fix: {issue.SuggestedFix}");
                }
            }
        }

        return sb.ToString().Trim();
    }
}

/// <summary>
/// Configuration pour MockConsistencyAgent.
/// </summary>
public sealed record MockConsistencyConfig
{
    /// <summary>
    /// Vérifier la cohérence des lieux.
    /// </summary>
    public bool CheckLocationConsistency { get; init; } = true;

    /// <summary>
    /// Vérifier la cohérence des relations.
    /// </summary>
    public bool CheckRelationshipConsistency { get; init; } = true;

    /// <summary>
    /// Vérifier les personnages morts.
    /// </summary>
    public bool CheckDeadCharacters { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MockConsistencyConfig Default => new();
}
