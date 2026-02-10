using System.Text;
using Narratum.Core;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts.Templates;

/// <summary>
/// Template de prompt pour l'agent de cohérence.
///
/// Génère des prompts pour vérifier et maintenir la cohérence
/// narrative avec les faits établis.
/// </summary>
public sealed class ConsistencyPromptTemplate : PromptTemplateBase
{
    public override string Name => "ConsistencyPrompt";
    public override AgentType TargetAgent => AgentType.Consistency;

    // L'agent de cohérence peut gérer tous les types d'intentions
    // car il vérifie la sortie des autres agents
    public override IReadOnlySet<IntentType> SupportedIntents { get; } = new HashSet<IntentType>
    {
        IntentType.ContinueNarrative,
        IntentType.DescribeScene,
        IntentType.GenerateDialogue,
        IntentType.Summarize,
        IntentType.CreateTension,
        IntentType.ResolveConflict,
        IntentType.IntroduceEvent
    };

    public override string BuildSystemPrompt(NarrativeContext context)
    {
        return """
            You are a narrative consistency checker for an interactive story engine.

            ROLE:
            - Verify that generated text respects established facts
            - Detect contradictions, anachronisms, and impossibilities
            - Suggest corrections for identified issues

            CONSISTENCY RULES:
            1. Dead characters cannot act, speak, or be present (unless in flashback)
            2. Characters can only know what they've learned in the story
            3. Location descriptions must match established facts
            4. Timeline events must be logically ordered
            5. Character traits and relationships must be consistent

            ANALYSIS APPROACH:
            - Compare generated text against established facts
            - Check for logical impossibilities
            - Verify character knowledge boundaries
            - Detect temporal inconsistencies

            OUTPUT FORMAT:
            For each issue found, report:
            - ISSUE: [Brief description]
            - SEVERITY: [Minor/Moderate/Severe]
            - TEXT: [The problematic portion]
            - SUGGESTION: [How to fix it]

            If no issues found, report:
            - CONSISTENT: The text respects all established facts.

            IMPORTANT:
            - Be thorough but not overly pedantic
            - Focus on factual contradictions, not style preferences
            - Consider context and reasonable inferences
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Check the following text for consistency with established facts:");
        sb.AppendLine();

        // Le texte à vérifier sera passé via intent.Description ou parameters
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine("TEXT TO VERIFY:");
            sb.AppendLine("---");
            sb.AppendLine(intent.Description);
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Faits établis
        sb.AppendLine("ESTABLISHED FACTS:");
        sb.AppendLine();

        // État des personnages
        sb.AppendLine("Character States:");
        foreach (var character in context.ActiveCharacters)
        {
            sb.AppendLine($"  - {character.Name}: {character.Status}");
            if (character.CharacterTraits.Count > 0)
            {
                sb.AppendLine($"    Traits: {string.Join(", ", character.CharacterTraits)}");
            }
            if (character.KnownFacts.Count > 0)
            {
                sb.AppendLine($"    Known facts: {string.Join("; ", character.KnownFacts)}");
            }
        }
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine("Current Location:");
            sb.AppendLine($"  - Name: {context.CurrentLocation.Name}");
            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
            {
                sb.AppendLine($"  - Description: {context.CurrentLocation.Description}");
            }
            if (context.CurrentLocation.PresentCharacterIds.Count > 0)
            {
                var presentNames = context.ActiveCharacters
                    .Where(c => context.CurrentLocation.PresentCharacterIds.Contains(c.CharacterId))
                    .Select(c => c.Name);
                sb.AppendLine($"  - Characters present: {string.Join(", ", presentNames)}");
            }
            sb.AppendLine();
        }

        // Événements récents
        if (context.RecentEvents.Count > 0)
        {
            sb.AppendLine("Recent Events (for timeline reference):");
            sb.AppendLine(FormatEventList(context.RecentEvents));
            sb.AppendLine();
        }

        // Résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("Recent Summary:");
            sb.AppendLine($"  {context.RecentSummary}");
            sb.AppendLine();
        }

        sb.AppendLine("Analyze the text for consistency issues.");

        return sb.ToString();
    }

    public override IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var baseVars = base.GetVariables(context);
        var vars = new Dictionary<string, string>(baseVars)
        {
            { "check_type", "full_consistency" },
            { "severity_threshold", "minor" }
        };

        // Compter les personnages morts pour le contexte
        var deadCount = context.ActiveCharacters
            .Count(c => c.Status == VitalStatus.Dead);
        vars["dead_character_count"] = deadCount.ToString();

        // Ajouter la liste des personnages morts comme référence rapide
        var deadNames = context.ActiveCharacters
            .Where(c => c.Status == VitalStatus.Dead)
            .Select(c => c.Name);
        vars["dead_characters"] = string.Join(", ", deadNames);

        return vars;
    }
}
