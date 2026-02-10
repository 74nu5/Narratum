using System.Diagnostics;
using System.Text;
using Narratum.Core;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents.Mock;

/// <summary>
/// Implémentation mock de INarratorAgent.
///
/// Génère des textes narratifs structurellement valides mais génériques.
/// Utilisé pour valider l'architecture sans LLM réel.
/// </summary>
public sealed class MockNarratorAgent : INarratorAgent
{
    private readonly ILlmClient _llmClient;
    private readonly MockNarratorConfig _config;

    public AgentType Type => AgentType.Narrator;
    public string Name => "MockNarratorAgent";

    public MockNarratorAgent(ILlmClient llmClient, MockNarratorConfig? config = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? MockNarratorConfig.Default;
    }

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        // Déterminer le style basé sur l'intention
        var style = DetermineStyle(context);

        // Générer la narrative
        var narrativeResult = await GenerateNarrativeAsync(
            context,
            context.RecentSummary ?? "",
            style,
            cancellationToken);

        stopwatch.Stop();

        if (narrativeResult is Result<string>.Success success)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateSuccess(
                Type,
                success.Value,
                stopwatch.Elapsed)
                .WithMetadata("style", style.ToString())
                .WithMetadata("mock", true));
        }
        else if (narrativeResult is Result<string>.Failure failure)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateFailure(
                Type,
                failure.Message,
                stopwatch.Elapsed));
        }

        return Result<AgentResponse>.Fail("Unknown error in MockNarratorAgent");
    }

    public bool CanHandle(NarrativeIntent intent)
    {
        return intent.Type is IntentType.ContinueNarrative
            or IntentType.DescribeScene
            or IntentType.CreateTension
            or IntentType.ResolveConflict;
    }

    public Task<Result<string>> GenerateNarrativeAsync(
        NarrativeContext context,
        string summary,
        NarrativeStyle style = NarrativeStyle.Descriptive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var narrative = BuildMockNarrative(context, style);
        return Task.FromResult(Result<string>.Ok(narrative));
    }

    public Task<Result<string>> DescribeSceneAsync(
        LocationContext location,
        IReadOnlyList<CharacterContext> presentCharacters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var description = BuildSceneDescription(location, presentCharacters ?? Array.Empty<CharacterContext>());
        return Task.FromResult(Result<string>.Ok(description));
    }

    /// <summary>
    /// Détermine le style narratif basé sur le contexte.
    /// </summary>
    private NarrativeStyle DetermineStyle(NarrativeContext context)
    {
        // Si beaucoup de personnages actifs, privilégier le dialogue
        if (context.ActiveCharacters.Count >= 3)
        {
            return NarrativeStyle.Dialogue;
        }

        // Si des événements récents importants, style action
        if (context.RecentEvents.Count >= 5)
        {
            return NarrativeStyle.Action;
        }

        // Par défaut, descriptif
        return NarrativeStyle.Descriptive;
    }

    /// <summary>
    /// Construit une narrative mock.
    /// </summary>
    private string BuildMockNarrative(NarrativeContext context, NarrativeStyle style)
    {
        var sb = new StringBuilder();

        // Introduction basée sur le lieu
        sb.AppendLine(BuildLocationIntro(context.CurrentLocation));

        // Mention des personnages présents
        sb.AppendLine(BuildCharacterPresence(context.ActiveCharacters, style));

        // Corps basé sur le style
        sb.AppendLine(BuildStyleContent(style));

        // Conclusion
        sb.AppendLine(BuildConclusion(style));

        return sb.ToString().Trim();
    }

    private string BuildLocationIntro(LocationContext? location)
    {
        if (location == null)
        {
            return "The scene unfolded in an unspecified location.";
        }

        return $"In {location.Name}, the air was thick with anticipation. {location.Description}";
    }

    private string BuildCharacterPresence(IReadOnlyList<CharacterContext> characters, NarrativeStyle style)
    {
        if (characters.Count == 0)
        {
            return "The place stood empty, waiting.";
        }

        var sb = new StringBuilder();

        foreach (var character in characters.Take(3))
        {
            var status = character.Status == VitalStatus.Alive ? "stood ready" : "lay motionless";
            sb.AppendLine($"{character.Name} {status}, their presence adding weight to the moment.");
        }

        if (characters.Count > 3)
        {
            sb.AppendLine($"Others watched from nearby, their roles yet to be played.");
        }

        return sb.ToString();
    }

    private string BuildStyleContent(NarrativeStyle style)
    {
        return style switch
        {
            NarrativeStyle.Descriptive =>
                "Every detail seemed significant. The light played across surfaces, revealing textures and shadows that spoke of history. Time moved slowly, each moment laden with meaning.",

            NarrativeStyle.Action =>
                "Events moved quickly. Decisions were made in heartbeats. The tension built to a crescendo as actions had immediate consequences.",

            NarrativeStyle.Introspective =>
                "Thoughts turned inward. Memories surfaced unbidden, coloring the present with shades of the past. Understanding came slowly, like dawn breaking.",

            NarrativeStyle.Dialogue =>
                "Words filled the space between them. Each phrase carried weight, each silence spoke volumes. The conversation shaped their shared reality.",

            _ =>
                "The narrative continued its inexorable progress, carrying all within it toward an uncertain future."
        };
    }

    private string BuildConclusion(NarrativeStyle style)
    {
        return style switch
        {
            NarrativeStyle.Descriptive => "The scene held its breath, waiting for what would come next.",
            NarrativeStyle.Action => "And then, everything changed.",
            NarrativeStyle.Introspective => "Some truths can only be understood in hindsight.",
            NarrativeStyle.Dialogue => "The words hung in the air, demanding response.",
            _ => "The story continued."
        };
    }

    /// <summary>
    /// Construit une description de scène.
    /// </summary>
    private string BuildSceneDescription(LocationContext location, IReadOnlyList<CharacterContext> characters)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"The {location.Name}:");
        sb.AppendLine(location.Description);
        sb.AppendLine();

        if (characters.Count > 0)
        {
            sb.AppendLine("Present:");
            foreach (var character in characters)
            {
                var status = character.Status == VitalStatus.Alive ? "alive and alert" : "no longer among the living";
                sb.AppendLine($"- {character.Name} ({status})");
            }
        }
        else
        {
            sb.AppendLine("The location stands empty.");
        }

        return sb.ToString().Trim();
    }
}

/// <summary>
/// Configuration pour MockNarratorAgent.
/// </summary>
public sealed record MockNarratorConfig
{
    /// <summary>
    /// Longueur cible du texte (en mots).
    /// </summary>
    public int TargetWordCount { get; init; } = 150;

    /// <summary>
    /// Inclure les noms des personnages.
    /// </summary>
    public bool IncludeCharacterNames { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MockNarratorConfig Default => new();
}
