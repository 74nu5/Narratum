using System.Text;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts.Templates;

/// <summary>
/// Template de prompt pour l'agent de résumé.
///
/// Génère des prompts pour créer des résumés concis et factuels
/// des événements narratifs.
/// </summary>
public sealed class SummaryPromptTemplate : PromptTemplateBase
{
    public override string Name => "SummaryPrompt";
    public override AgentType TargetAgent => AgentType.Summary;

    public override IReadOnlySet<IntentType> SupportedIntents { get; } = new HashSet<IntentType>
    {
        IntentType.Summarize
    };

    public override string BuildSystemPrompt(NarrativeContext context)
    {
        return """
            You are a narrative summarizer for an interactive story engine.

            ROLE:
            - Create concise, factual summaries of story events
            - Preserve the essential facts and character states
            - Maintain chronological order of events

            RULES:
            1. Be factual and objective - no embellishment
            2. Include ALL major events (deaths, encounters, revelations)
            3. Mention characters by their exact names
            4. Note any state changes explicitly
            5. Keep the summary under 500 words

            FORMAT:
            - Plain prose, past tense, third person
            - One paragraph per major event group
            - End with current state of key characters

            FORBIDDEN:
            - Adding details not in the source events
            - Speculation about character motivations
            - Future predictions
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Summarize the following narrative events:");
        sb.AppendLine();

        // Liste des événements
        sb.AppendLine("EVENTS:");
        if (context.RecentEvents.Count == 0)
        {
            sb.AppendLine("- No events to summarize.");
        }
        else
        {
            sb.AppendLine(FormatEventList(context.RecentEvents));
        }
        sb.AppendLine();

        // Personnages actifs
        sb.AppendLine("ACTIVE CHARACTERS:");
        sb.AppendLine(FormatCharacterList(context.ActiveCharacters));
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"CURRENT LOCATION: {context.CurrentLocation.Name}");
            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
            {
                sb.AppendLine($"  {context.CurrentLocation.Description}");
            }
            sb.AppendLine();
        }

        // Directive spécifique si présente
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"SPECIFIC FOCUS: {intent.Description}");
            sb.AppendLine();
        }

        sb.AppendLine("Generate a summary of these events.");

        return sb.ToString();
    }

    public override IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var baseVars = base.GetVariables(context);
        var vars = new Dictionary<string, string>(baseVars)
        {
            { "summary_type", "chapter" },
            { "max_words", "500" }
        };

        return vars;
    }
}
