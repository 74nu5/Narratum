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
            Tu es un vérificateur de cohérence narrative pour un moteur d'histoire interactif.

            RÔLE :
            - Vérifier que le texte généré respecte les faits établis
            - Détecter les contradictions, anachronismes et impossibilités
            - Proposer des corrections pour les problèmes identifiés

            RÈGLES DE COHÉRENCE :
            1. Les personnages morts ne peuvent pas agir, parler ou être présents (sauf en flashback)
            2. Les personnages ne peuvent savoir que ce qu'ils ont appris dans l'histoire
            3. Les descriptions de lieux doivent correspondre aux faits établis
            4. Les événements de la chronologie doivent être logiquement ordonnés
            5. Les traits et relations des personnages doivent être cohérents

            APPROCHE D'ANALYSE :
            - Comparer le texte généré aux faits établis
            - Vérifier les impossibilités logiques
            - Vérifier les limites de connaissance des personnages
            - Détecter les incohérences temporelles

            FORMAT DE SORTIE :
            Pour chaque problème trouvé, signaler :
            - PROBLÈME : [Description brève]
            - SÉVÉRITÉ : [Mineur/Modéré/Grave]
            - TEXTE : [La portion problématique]
            - SUGGESTION : [Comment le corriger]

            Si aucun problème trouvé, signaler :
            - COHÉRENT : Le texte respecte tous les faits établis.

            IMPORTANT :
            - Être approfondi mais pas trop pointilleux
            - Se concentrer sur les contradictions factuelles, pas les préférences stylistiques
            - Prendre en compte le contexte et les inférences raisonnables
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Vérifiez la cohérence du texte suivant avec les faits établis :");
        sb.AppendLine();

        // Le texte à vérifier sera passé via intent.Description ou parameters
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine("TEXTE À VÉRIFIER :");
            sb.AppendLine("---");
            sb.AppendLine(intent.Description);
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Faits établis
        sb.AppendLine("FAITS ÉTABLIS :");
        sb.AppendLine();

        // État des personnages
        sb.AppendLine("États des personnages :");
        foreach (var character in context.ActiveCharacters)
        {
            sb.AppendLine($"  - {character.Name}: {character.Status}");
            if (character.CharacterTraits.Count > 0)
            {
                sb.AppendLine($"    Traits : {string.Join(", ", character.CharacterTraits)}");
            }
            if (character.KnownFacts.Count > 0)
            {
                sb.AppendLine($"    Faits connus : {string.Join("; ", character.KnownFacts)}");
            }
        }
        sb.AppendLine();

        // Lieu actuel
        if (context.CurrentLocation != null)
        {
            sb.AppendLine("Lieu actuel :");
            sb.AppendLine($"  - Nom : {context.CurrentLocation.Name}");
            if (!string.IsNullOrEmpty(context.CurrentLocation.Description))
            {
                sb.AppendLine($"  - Description : {context.CurrentLocation.Description}");
            }
            if (context.CurrentLocation.PresentCharacterIds.Count > 0)
            {
                var presentNames = context.ActiveCharacters
                    .Where(c => context.CurrentLocation.PresentCharacterIds.Contains(c.CharacterId))
                    .Select(c => c.Name);
                sb.AppendLine($"  - Personnages présents : {string.Join(", ", presentNames)}");
            }
            sb.AppendLine();
        }

        // Événements récents
        if (context.RecentEvents.Count > 0)
        {
            sb.AppendLine("Événements récents (référence chronologique) :");
            sb.AppendLine(FormatEventList(context.RecentEvents));
            sb.AppendLine();
        }

        // Résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("Résumé récent :");
            sb.AppendLine($"  {context.RecentSummary}");
            sb.AppendLine();
        }

        sb.AppendLine("Analysez le texte pour détecter les incohérences.");

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
