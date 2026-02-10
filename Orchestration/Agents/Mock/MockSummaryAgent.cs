using System.Diagnostics;
using System.Text;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents.Mock;

/// <summary>
/// Implémentation mock de ISummaryAgent.
///
/// Génère des résumés structurellement valides mais génériques.
/// Utilisé pour valider l'architecture sans LLM réel.
/// </summary>
public sealed class MockSummaryAgent : ISummaryAgent
{
    private readonly ILlmClient _llmClient;
    private readonly MockSummaryConfig _config;

    public AgentType Type => AgentType.Summary;
    public string Name => "MockSummaryAgent";

    public MockSummaryAgent(ILlmClient llmClient, MockSummaryConfig? config = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _config = config ?? MockSummaryConfig.Default;
    }

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        // Extraire les événements du contexte (RecentEvents est IReadOnlyList<object>)
        var events = context.RecentEvents.OfType<Event>().ToList();

        // Générer le résumé basé sur les événements récents
        var summaryResult = await SummarizeEventsAsync(
            events,
            _config.DefaultSummaryLength,
            cancellationToken);

        stopwatch.Stop();

        if (summaryResult is Result<string>.Success success)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateSuccess(
                Type,
                success.Value,
                stopwatch.Elapsed)
                .WithMetadata("eventCount", context.RecentEvents.Count)
                .WithMetadata("mock", true));
        }
        else if (summaryResult is Result<string>.Failure failure)
        {
            return Result<AgentResponse>.Ok(AgentResponse.CreateFailure(
                Type,
                failure.Message,
                stopwatch.Elapsed));
        }

        return Result<AgentResponse>.Fail("Unknown error in MockSummaryAgent");
    }

    public bool CanHandle(NarrativeIntent intent)
    {
        return intent.Type == IntentType.Summarize;
    }

    public Task<Result<string>> SummarizeEventsAsync(
        IReadOnlyList<Event> events,
        int targetLength = 150,
        CancellationToken cancellationToken = default)
    {
        if (events == null || events.Count == 0)
        {
            return Task.FromResult(Result<string>.Ok(
                "No significant events occurred during this period."));
        }

        var summary = BuildEventSummary(events, targetLength);
        return Task.FromResult(Result<string>.Ok(summary));
    }

    public Task<Result<string>> SummarizeChapterAsync(
        StoryChapter chapter,
        IReadOnlyList<Event> chapterEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var sb = new StringBuilder();

        sb.AppendLine($"Chapter {chapter.Index + 1}");
        sb.AppendLine();

        if (chapterEvents == null || chapterEvents.Count == 0)
        {
            sb.AppendLine("This chapter contained no recorded events.");
        }
        else
        {
            sb.AppendLine(BuildEventSummary(chapterEvents, 200));
        }

        return Task.FromResult(Result<string>.Ok(sb.ToString()));
    }

    /// <summary>
    /// Construit un résumé des événements.
    /// </summary>
    private string BuildEventSummary(IReadOnlyList<Event> events, int targetLength)
    {
        var sb = new StringBuilder();

        // Grouper par type d'événement
        var eventsByType = events.GroupBy(e => e.Type).ToList();

        foreach (var group in eventsByType)
        {
            var eventType = group.Key;
            var count = group.Count();

            sb.Append(FormatEventGroup(eventType, count, group.ToList()));
        }

        // Ajouter une conclusion générique
        sb.Append(" The narrative progressed as expected.");

        return TruncateToLength(sb.ToString(), targetLength);
    }

    /// <summary>
    /// Formate un groupe d'événements.
    /// </summary>
    private string FormatEventGroup(string eventType, int count, IReadOnlyList<Event> events)
    {
        return eventType switch
        {
            "CharacterEncounter" => FormatEncounters(events),
            "CharacterDeath" => FormatDeaths(events),
            "CharacterMoved" => FormatMovements(events),
            "Revelation" => FormatRevelations(events),
            _ => $"{count} {eventType} event(s) occurred. "
        };
    }

    private string FormatEncounters(IReadOnlyList<Event> events)
    {
        if (events.Count == 1)
        {
            return "Two characters met and interacted. ";
        }
        return $"Several encounters took place ({events.Count} in total). ";
    }

    private string FormatDeaths(IReadOnlyList<Event> events)
    {
        if (events.Count == 1)
        {
            var evt = events[0];
            return $"A character met their end. ";
        }
        return $"Multiple characters perished ({events.Count} deaths). ";
    }

    private string FormatMovements(IReadOnlyList<Event> events)
    {
        return $"Characters moved between locations ({events.Count} movements). ";
    }

    private string FormatRevelations(IReadOnlyList<Event> events)
    {
        if (events.Count == 1)
        {
            return "An important truth was revealed. ";
        }
        return $"Multiple revelations came to light ({events.Count}). ";
    }

    /// <summary>
    /// Tronque le texte à la longueur cible (en mots approximatifs).
    /// </summary>
    private string TruncateToLength(string text, int targetWords)
    {
        var words = text.Split(' ');
        if (words.Length <= targetWords)
        {
            return text;
        }

        return string.Join(' ', words.Take(targetWords)) + "...";
    }
}

/// <summary>
/// Configuration pour MockSummaryAgent.
/// </summary>
public sealed record MockSummaryConfig
{
    /// <summary>
    /// Longueur par défaut des résumés (en mots).
    /// </summary>
    public int DefaultSummaryLength { get; init; } = 100;

    /// <summary>
    /// Inclure les noms des personnages.
    /// </summary>
    public bool IncludeCharacterNames { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MockSummaryConfig Default => new();
}
