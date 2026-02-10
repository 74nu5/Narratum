using System.Diagnostics;
using System.Text;
using Narratum.Core;
using Narratum.Domain;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents.Mock;

/// <summary>
/// Implémentation mock de ICharacterAgent.
///
/// Génère des dialogues structurellement valides mais génériques.
/// Utilisé pour valider l'architecture sans LLM réel.
/// </summary>
public sealed class MockCharacterAgent : ICharacterAgent
{
    private readonly ILlmClient _llmClient;
    private readonly MockCharacterConfig _config;

    public AgentType Type => AgentType.Character;
    public string Name => "MockCharacterAgent";

    public MockCharacterAgent(ILlmClient llmClient, MockCharacterConfig? config = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? MockCharacterConfig.Default;
    }

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        // Prendre le premier personnage actif comme locuteur
        var speaker = context.ActiveCharacters.FirstOrDefault();
        if (speaker == null)
        {
            stopwatch.Stop();
            return Result<AgentResponse>.Ok(AgentResponse.CreateFailure(
                Type,
                "No active character available for dialogue",
                stopwatch.Elapsed));
        }

        // Prendre le second comme auditeur
        var listener = context.ActiveCharacters.Skip(1).FirstOrDefault();

        // Créer une situation de dialogue
        var situation = DialogueSituation.Neutral(
            "General conversation",
            "current situation",
            "recent events");

        var dialogueResult = await GenerateDialogueAsync(
            speaker,
            listener,
            situation,
            cancellationToken);

        stopwatch.Stop();

        if (dialogueResult is Result<string>.Success success)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateSuccess(
                Type,
                success.Value,
                stopwatch.Elapsed)
                .WithMetadata("speaker", speaker.Name)
                .WithMetadata("mock", true));
        }
        else if (dialogueResult is Result<string>.Failure failure)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateFailure(
                Type,
                failure.Message,
                stopwatch.Elapsed));
        }

        return Result<AgentResponse>.Fail("Unknown error in MockCharacterAgent");
    }

    public bool CanHandle(NarrativeIntent intent)
    {
        return intent.Type == IntentType.GenerateDialogue;
    }

    public Task<Result<string>> GenerateDialogueAsync(
        CharacterContext speaker,
        CharacterContext? listener,
        DialogueSituation situation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(situation);

        var dialogue = BuildMockDialogue(speaker, listener, situation);
        return Task.FromResult(Result<string>.Ok(dialogue));
    }

    public Task<Result<string>> GenerateReactionAsync(
        CharacterContext character,
        Event triggeringEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(triggeringEvent);

        var reaction = BuildMockReaction(character, triggeringEvent);
        return Task.FromResult(Result<string>.Ok(reaction));
    }

    /// <summary>
    /// Construit un dialogue mock.
    /// </summary>
    private string BuildMockDialogue(
        CharacterContext speaker,
        CharacterContext? listener,
        DialogueSituation situation)
    {
        var sb = new StringBuilder();

        // Ligne d'ouverture basée sur le ton
        var opening = GetOpeningLine(situation.Tone);
        sb.AppendLine($"\"{opening}\" said {speaker.Name}.");

        // Si auditeur présent, ajouter une réponse
        if (listener != null)
        {
            var response = GetResponseLine(situation.Tone);
            sb.AppendLine($"{listener.Name} considered the words carefully.");
            sb.AppendLine($"\"{response}\" {listener.Name} replied.");
        }

        // Ajouter contexte sur les sujets
        if (situation.TopicsToAddress.Count > 0)
        {
            var topic = situation.TopicsToAddress.First();
            sb.AppendLine($"The conversation turned to {topic}.");
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Construit une réaction mock.
    /// </summary>
    private string BuildMockReaction(CharacterContext character, Event triggeringEvent)
    {
        var sb = new StringBuilder();

        // Réaction basée sur le type d'événement
        var reactionType = GetReactionType(triggeringEvent.Type);

        sb.AppendLine($"{character.Name}'s expression {reactionType}.");

        // Ajouter une pensée ou action
        var action = GetReactionAction(triggeringEvent.Type);
        sb.AppendLine($"{action}");

        return sb.ToString().Trim();
    }

    private string GetOpeningLine(EmotionalTone tone)
    {
        return tone switch
        {
            EmotionalTone.Neutral => "I've been thinking about what you said.",
            EmotionalTone.Friendly => "It's good to see you again!",
            EmotionalTone.Hostile => "We need to talk. Now.",
            EmotionalTone.Fearful => "Something isn't right here...",
            EmotionalTone.Excited => "You won't believe what I just discovered!",
            EmotionalTone.Sad => "I wish things had turned out differently.",
            _ => "There's something I need to tell you."
        };
    }

    private string GetResponseLine(EmotionalTone tone)
    {
        return tone switch
        {
            EmotionalTone.Neutral => "I understand. What do you propose?",
            EmotionalTone.Friendly => "Always a pleasure. What brings you here?",
            EmotionalTone.Hostile => "Choose your next words carefully.",
            EmotionalTone.Fearful => "What do you mean? What's happening?",
            EmotionalTone.Excited => "Tell me everything!",
            EmotionalTone.Sad => "I know. I feel the same way.",
            _ => "Go on, I'm listening."
        };
    }

    private string GetReactionType(string eventType)
    {
        return eventType switch
        {
            "CharacterDeath" => "paled with shock",
            "CharacterEncounter" => "shifted with surprise",
            "CharacterMoved" => "registered the movement",
            "Revelation" => "widened with understanding",
            _ => "changed subtly"
        };
    }

    private string GetReactionAction(string eventType)
    {
        return eventType switch
        {
            "CharacterDeath" => "Words failed, replaced by a heavy silence.",
            "CharacterEncounter" => "Recognition flickered in their eyes.",
            "CharacterMoved" => "They tracked the movement with keen interest.",
            "Revelation" => "The implications sank in slowly.",
            _ => "They processed what had just occurred."
        };
    }
}

/// <summary>
/// Configuration pour MockCharacterAgent.
/// </summary>
public sealed record MockCharacterConfig
{
    /// <summary>
    /// Nombre de lignes de dialogue par défaut.
    /// </summary>
    public int DefaultDialogueLines { get; init; } = 3;

    /// <summary>
    /// Inclure les pensées internes.
    /// </summary>
    public bool IncludeInternalThoughts { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MockCharacterConfig Default => new();
}
