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
            You are a character dialogue writer for an interactive story engine.

            ROLE:
            - Generate authentic dialogue for story characters
            - Capture each character's unique voice and personality
            - Create meaningful exchanges that advance relationships

            DIALOGUE PRINCIPLES:
            - Each character has a distinct voice based on their traits
            - Dialogue should reveal character, not just convey information
            - Subtext matters - characters don't always say what they mean
            - Reactions should be proportional and in-character

            RULES:
            1. Stay true to established character traits
            2. Consider relationships between characters
            3. Account for what characters know vs. don't know
            4. Dead characters cannot speak (unless flashback/memory)
            5. Match the emotional tone of the scene

            FORMAT:
            - Use quotation marks for spoken dialogue
            - Include brief action beats between lines
            - Keep exchanges focused and purposeful
            - 3-6 dialogue exchanges maximum

            FORBIDDEN:
            - Characters acting out of character
            - Modern slang in historical/fantasy settings
            - Characters knowing information they shouldn't
            - Overly expository dialogue ("As you know, Bob...")
            """;
    }

    public override string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate dialogue for the following scene:");
        sb.AppendLine();

        // Lieu de la scène
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"SETTING: {context.CurrentLocation.Name}");
            sb.AppendLine();
        }

        // Personnages dans la scène
        sb.AppendLine("CHARACTERS IN SCENE:");
        foreach (var character in context.ActiveCharacters)
        {
            sb.AppendLine($"  {character.Name}:");
            sb.AppendLine($"    - Status: {character.Status}");

            if (character.CharacterTraits.Count > 0)
            {
                sb.AppendLine($"    - Traits: {string.Join(", ", character.CharacterTraits)}");
            }

            if (!string.IsNullOrEmpty(character.CurrentMood))
            {
                sb.AppendLine($"    - Current mood: {character.CurrentMood}");
            }

            if (character.KnownFacts.Count > 0)
            {
                sb.AppendLine($"    - Knows: {string.Join("; ", character.KnownFacts.Take(3))}");
            }
        }
        sb.AppendLine();

        // Contexte de la conversation
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("CONTEXT:");
            sb.AppendLine($"  {context.RecentSummary}");
            sb.AppendLine();
        }

        // Personnages cibles (locuteur principal si spécifié)
        if (intent.TargetCharacterIds.Count > 0)
        {
            var targetCharacters = context.ActiveCharacters
                .Where(c => intent.TargetCharacterIds.Contains(c.CharacterId))
                .Select(c => c.Name);
            sb.AppendLine($"PRIMARY SPEAKER: {string.Join(", ", targetCharacters)}");
            sb.AppendLine();
        }

        // Direction spécifique
        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"DIALOGUE OBJECTIVE: {intent.Description}");
            sb.AppendLine();
        }

        // Paramètres additionnels
        if (intent.Parameters.Count > 0)
        {
            if (intent.Parameters.TryGetValue("tone", out var tone))
            {
                sb.AppendLine($"EMOTIONAL TONE: {tone}");
            }
            if (intent.Parameters.TryGetValue("topic", out var topic))
            {
                sb.AppendLine($"TOPIC: {topic}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Generate the dialogue exchange.");

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
