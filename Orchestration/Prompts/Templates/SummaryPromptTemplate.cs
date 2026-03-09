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
            Tu es un résumeur narratif pour un moteur d'histoire interactif.

            RÔLE :
            - Créer des résumés concis et factuels des événements de l'histoire
            - Préserver les faits essentiels et les états des personnages
            - Maintenir l'ordre chronologique des événements

            RÈGLES :
            1. Être factuel et objectif — pas d'embellissement
            2. Inclure TOUS les événements majeurs (morts, rencontres, révélations)
            3. Nommer les personnages par leurs noms exacts
            4. Noter explicitement tout changement d'état
            5. Garder le résumé sous 500 mots

            FORMAT :
            - Prose simple, passé composé ou imparfait, troisième personne
            - Un paragraphe par groupe d'événements majeurs
            - Terminer par l'état actuel des personnages clés

            INTERDIT :
            - Ajouter des détails absents des événements sources
            - Spéculer sur les motivations des personnages
            - Prédictions futures
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Résumez les événements narratifs suivants :");
        sb.AppendLine();

        // Liste des événements
        sb.AppendLine("ÉVÉNEMENTS :");
        if (context.RecentEvents.Count == 0)
        {
            sb.AppendLine("- Aucun événement à résumer.");
        }
        else
        {
            sb.AppendLine(FormatEventList(context.RecentEvents));
        }
        sb.AppendLine();

        // Personnages actifs
        sb.AppendLine("PERSONNAGES ACTIFS :");
        sb.AppendLine(FormatCharacterList(context.ActiveCharacters));
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"LIEU ACTUEL : {context.CurrentLocation.Name}");
            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
            {
                sb.AppendLine($"  {context.CurrentLocation.Description}");
            }
            sb.AppendLine();
        }

        // Directive spécifique si présente
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"FOCUS SPÉCIFIQUE : {intent.Description}");
            sb.AppendLine();
        }

        sb.AppendLine("Générez un résumé de ces événements.");

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
