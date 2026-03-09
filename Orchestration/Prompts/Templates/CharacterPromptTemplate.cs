using System.Text;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts.Templates;

/// <summary>
/// Template de prompt pour l'agent de personnage.
///
/// Génère des prompts pour créer des dialogues et des réactions
/// de personnages authentiques et cohérents.
/// </summary>
public sealed class CharacterPromptTemplate : PromptTemplateBase
{
    public override string Name => "CharacterPrompt";
    public override AgentType TargetAgent => AgentType.Character;

    public override IReadOnlySet<IntentType> SupportedIntents { get; } = new HashSet<IntentType>
    {
        IntentType.GenerateDialogue,
        IntentType.IntroduceEvent
    };

    public override string BuildSystemPrompt(NarrativeContext context)
    {
        return """
            Tu es un auteur de dialogues pour un moteur d'histoire interactif.

            RÔLE :
            - Générer des dialogues authentiques pour les personnages de l'histoire
            - Capturer la voix et la personnalité uniques de chaque personnage
            - Créer des échanges significatifs qui font avancer les relations

            PRINCIPES DU DIALOGUE :
            - Chaque personnage a une voix distincte basée sur ses traits
            - Le dialogue doit révéler le personnage, pas seulement transmettre de l'information
            - Le sous-texte compte — les personnages ne disent pas toujours ce qu'ils pensent
            - Les réactions doivent être proportionnelles et dans le personnage

            RÈGLES :
            1. Rester fidèle aux traits établis du personnage
            2. Tenir compte des relations entre les personnages
            3. Prendre en compte ce que les personnages savent et ne savent pas
            4. Les personnages morts ne peuvent pas parler (sauf en flashback ou souvenir)
            5. Correspondre au ton émotionnel de la scène

            FORMAT :
            - Utiliser des guillemets pour les dialogues parlés
            - Inclure de brèves indications d'action entre les répliques
            - Garder les échanges ciblés et intentionnels
            - Maximum 3 à 6 échanges de dialogue

            INTERDIT :
            - Personnages agissant en dehors de leur caractère
            - Argot moderne dans les contextes historiques ou fantastiques
            - Personnages connaissant des informations qu'ils ne devraient pas
            - Dialogue trop explicatif ("Comme tu le sais, Bob...")
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Générez un dialogue pour la scène suivante :");
        sb.AppendLine();

        // Lieu de la scène
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"CADRE : {context.CurrentLocation.Name}");
            sb.AppendLine();
        }

        // Personnages dans la scène
        sb.AppendLine("PERSONNAGES DANS LA SCÈNE :");
        foreach (var character in context.ActiveCharacters)
        {
            sb.AppendLine($"  {character.Name}:");
            sb.AppendLine($"    - Statut : {character.Status}");

            if (character.CharacterTraits.Count > 0)
            {
                sb.AppendLine($"    - Traits : {string.Join(", ", character.CharacterTraits)}");
            }

            if (!string.IsNullOrEmpty(character.CurrentMood))
            {
                sb.AppendLine($"    - Humeur actuelle : {character.CurrentMood}");
            }

            if (character.KnownFacts.Count > 0)
            {
                sb.AppendLine($"    - Sait : {string.Join("; ", character.KnownFacts.Take(3))}");
            }
        }
        sb.AppendLine();

        // Contexte de la conversation
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("CONTEXTE :");
            sb.AppendLine($"  {context.RecentSummary}");
            sb.AppendLine();
        }

        // Personnages cibles (locuteur principal si spécifié)
        if (intent.TargetCharacterIds.Count > 0)
        {
            var targetCharacters = context.ActiveCharacters
                .Where(c => intent.TargetCharacterIds.Contains(c.CharacterId))
                .Select(c => c.Name);
            sb.AppendLine($"LOCUTEUR PRINCIPAL : {string.Join(", ", targetCharacters)}");
            sb.AppendLine();
        }

        // Direction spécifique
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"OBJECTIF DU DIALOGUE : {intent.Description}");
            sb.AppendLine();
        }

        // Paramètres additionnels
        if (intent.Parameters.Count > 0)
        {
            if (intent.Parameters.TryGetValue("tone", out var tone))
            {
                sb.AppendLine($"TON ÉMOTIONNEL : {tone}");
            }
            if (intent.Parameters.TryGetValue("topic", out var topic))
            {
                sb.AppendLine($"SUJET : {topic}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Générez l'échange de dialogue.");

        return sb.ToString();
    }

    public override IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var baseVars = base.GetVariables(context);
        var vars = new Dictionary<string, string>(baseVars)
        {
            { "dialogue_type", "conversation" },
            { "max_exchanges", "6" }
        };

        // Ajouter les noms des personnages individuellement
        var characters = context.ActiveCharacters.ToList();
        for (int i = 0; i < Math.Min(characters.Count, 4); i++)
        {
            vars[$"character_{i + 1}_name"] = characters[i].Name;
            vars[$"character_{i + 1}_traits"] = string.Join(", ", characters[i].CharacterTraits);
        }

        return vars;
    }
}
