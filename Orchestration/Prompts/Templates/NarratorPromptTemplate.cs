namespace Narratum.Orchestration.Prompts.Templates;

using System.Text;

using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

/// <summary>
///     Template de prompt pour l'agent narrateur.
///     Génère des prompts pour créer de la prose narrative
///     qui avance l'histoire tout en respectant les faits établis.
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
        IntentType.ResolveConflict,
    };

    public override string BuildSystemPrompt(NarrativeContext context)
        => """
           Tu es un auteur narratif pour un moteur d'histoire interactif.

           RÔLE :

           * Générer une narration centrée sur les actions, décisions et conséquences
           * Faire progresser l’intrigue à chaque paragraphe
           * Maintenir la cohérence avec tous les faits établis
           * Adapter la narration à la personne grammaticale demandée (première ou troisième)

           STYLE :

           * Utiliser **strictement** la personne grammaticale spécifiée dans le contexte (première ou troisième)
           * Temps : passé composé, imparfait ou présent narratif selon la continuité existante
           * Rythme soutenu, phrases dynamiques
           * Priorité aux verbes d’action, aux choix et aux interactions
           * Limiter les descriptions statiques de décor ou d’ambiance
           * Exprimer émotions et tensions à travers les actions et décisions

           RÈGLES :

           1. NE JAMAIS changer la personne grammaticale en cours de génération
           2. NE JAMAIS contredire les faits établis
           3. NE JAMAIS tuer des personnages sans instruction explicite
           4. NE JAMAIS introduire de nouveaux personnages ou lieux non mentionnés
           5. TOUJOURS nommer les personnages par leurs noms établis (sauf en narration à la première personne si le narrateur est ce personnage)
           6. TOUJOURS respecter les traits et relations des personnages
           7. Chaque paragraphe doit contenir au moins un événement, une décision ou une action qui modifie la situation
           8. Garder le contenu généré entre 150 et 300 mots

           FORMAT :

           * 2 à 3 paragraphes de prose
           * Transitions brèves et fonctionnelles
           * Terminer par une action imminente, une décision critique ou une tension immédiate

           INTERDIT :

           * Longues descriptions contemplatives
           * Monologues introspectifs prolongés sans action
           * Briser le quatrième mur
           * Anachronismes modernes dans les univers fantastiques
           * Actions contraires au personnage
           * Résolutions deus ex machina
           """;

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        // En-tête basé sur l'intention
        var header = intent.Type switch
        {
            IntentType.DescribeScene   => "Décrivez la scène suivante :",
            IntentType.CreateTension   => "Créez une tension dans la scène suivante :",
            IntentType.ResolveConflict => "Résolvez le conflit dans la scène suivante :",
            _                          => "Continuez la narration :",
        };

        sb.AppendLine(header);
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"LIEU : {context.CurrentLocation.Name}");

            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
                sb.AppendLine($"  Description : {context.CurrentLocation.Description}");

            sb.AppendLine();
        }

        // Personnages présents
        sb.AppendLine("PERSONNAGES PRÉSENTS :");
        sb.AppendLine(this.FormatCharacterList(context.ActiveCharacters));
        sb.AppendLine();

        // Résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("RÉSUMÉ DES ÉVÉNEMENTS RÉCENTS :");
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
            sb.AppendLine("FAITS ÉTABLIS :");
            sb.AppendLine(this.FormatKnownFacts(new HashSet<string>(allKnownFacts)));
            sb.AppendLine();
        }

        // Intention spécifique
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"DIRECTION NARRATIVE : {intent.Description}");
            sb.AppendLine();
        }

        // Focus sur un personnage cible si spécifié
        if (intent.TargetCharacterIds.Count > 0)
        {
            var targetCharacters = context.ActiveCharacters
                                          .Where(c => intent.TargetCharacterIds.Contains(c.CharacterId))
                                          .Select(c => c.Name);

            sb.AppendLine($"FOCUS SUR : {string.Join(", ", targetCharacters)}");
            sb.AppendLine();
        }

        sb.AppendLine("Générez la narration.");

        return sb.ToString();
    }

    public override IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var baseVars = base.GetVariables(context);
        var vars = new Dictionary<string, string>(baseVars)
        {
            { "narrative_style", "descriptive" },
            { "min_words", "150" },
            { "max_words", "300" },
        };

        // Ajouter le lieu s'il existe
        if (context.CurrentLocation != null)
        {
            vars["location_description"] = context.CurrentLocation.Description ?? "";
        }

        return vars;
    }
}
