using System.Text;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts.Templates;

/// <summary>
/// Template de prompt pour l'agent narrateur.
///
/// Génère des prompts pour créer de la prose narrative
/// qui avance l'histoire tout en respectant les faits établis.
/// </summary>
public sealed class NarratorPromptTemplate : PromptTemplateBase
{
    public override string Name => "NarratorPrompt";
    public override AgentType TargetAgent => AgentType.Narrator;

    public override IReadOnlySet<IntentType> SupportedIntents { get; } = new HashSet<IntentType>
    {
        IntentType.ContinueNarrative,
        IntentType.DescribeScene,
        IntentType.CreateTension,
        IntentType.ResolveConflict
    };

    public override string BuildSystemPrompt(NarrativeContext context)
    {
        return """
            You are a narrative writer for an interactive story engine.

            ROLE:
            - Generate descriptive prose that advances the narrative
            - Maintain consistency with all established facts
            - Write immersive, engaging narrative content

            STYLE:
            - Third person, past tense
            - Rich descriptive language
            - Show, don't tell - use actions and sensory details
            - Vary sentence structure for rhythm

            RULES:
            1. NEVER contradict established facts
            2. NEVER kill characters without explicit instruction
            3. NEVER introduce new characters or locations not mentioned
            4. ALWAYS mention characters by their established names
            5. ALWAYS respect character traits and relationships
            6. Keep generated content between 150-300 words

            FORMAT:
            - 2-3 paragraphs of prose
            - Natural transitions between scenes
            - End with a subtle narrative hook

            FORBIDDEN:
            - Breaking the fourth wall
            - Modern anachronisms in fantasy settings
            - Out-of-character actions
            - Deus ex machina resolutions
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        // En-tête basé sur l'intention
        var header = intent.Type switch
        {
            IntentType.DescribeScene => "Describe the following scene:",
            IntentType.CreateTension => "Create tension in the following scene:",
            IntentType.ResolveConflict => "Resolve the conflict in the following scene:",
            _ => "Continue the narrative:"
        };
        sb.AppendLine(header);
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"LOCATION: {context.CurrentLocation.Name}");
            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
            {
                sb.AppendLine($"  Description: {context.CurrentLocation.Description}");
            }
            sb.AppendLine();
        }

        // Personnages présents
        sb.AppendLine("PRESENT CHARACTERS:");
        sb.AppendLine(FormatCharacterList(context.ActiveCharacters));
        sb.AppendLine();

        // Résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("RECENT EVENTS SUMMARY:");
            sb.AppendLine(context.RecentSummary);
            sb.AppendLine();
        }

        // Faits établis (si disponibles via les personnages)
        var allKnownFacts = context.ActiveCharacters
            .SelectMany(c => c.KnownFacts)
            .Distinct()
            .ToList();

        if (allKnownFacts.Count > 0)
        {
            sb.AppendLine("ESTABLISHED FACTS:");
            sb.AppendLine(FormatKnownFacts(new HashSet<string>(allKnownFacts)));
            sb.AppendLine();
        }

        // Intention spécifique
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"NARRATIVE DIRECTION: {intent.Description}");
            sb.AppendLine();
        }

        // Focus sur un personnage cible si spécifié
        if (intent.TargetCharacterIds.Count > 0)
        {
            var targetCharacters = context.ActiveCharacters
                .Where(c => intent.TargetCharacterIds.Contains(c.CharacterId))
                .Select(c => c.Name);
            sb.AppendLine($"FOCUS ON: {string.Join(", ", targetCharacters)}");
            sb.AppendLine();
        }

        sb.AppendLine("Generate the narrative.");

        return sb.ToString();
    }

    public override IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var baseVars = base.GetVariables(context);
        var vars = new Dictionary<string, string>(baseVars)
        {
            { "narrative_style", "descriptive" },
            { "min_words", "150" },
            { "max_words", "300" }
        };

        // Ajouter le lieu s'il existe
        if (context.CurrentLocation != null)
        {
            vars["location_description"] = context.CurrentLocation.Description ?? "";
        }

        return vars;
    }
}
