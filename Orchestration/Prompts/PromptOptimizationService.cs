using Narratum.Domain.Events;
using Narratum.Orchestration.Models;
using Narratum.State;

namespace Narratum.Orchestration.Prompts;

/// <summary>
/// Service for building optimized prompts with rich context.
/// Improves narrative quality through detailed, contextual prompts.
/// </summary>
public class PromptOptimizationService
{
    /// <summary>
    /// Builds an optimized prompt for narrative generation.
    /// </summary>
    public string BuildOptimizedNarratorPrompt(
        StoryState state,
        NarrativeIntent intent,
        string? previousNarrative = null,
        string? genre = null,
        string? tone = null)
    {
        var worldName = state.WorldState.WorldName;
        var eventCount = state.EventHistory.Count;
        var characterCount = state.Characters.Count;

        var prompt = $@"You are a master storyteller crafting narrative for ""{worldName}"".

CONTEXT:
- Genre: {genre ?? "Fantasy"}
- Tone: {tone ?? "Atmospheric and engaging"}
- Story progress: {eventCount} events, {characterCount} characters
- Intent: {intent.Description}

CHARACTERS PRESENT:
{BuildCharacterContext(state, intent)}

RECENT EVENTS:
{BuildRecentEventsContext(state)}

{BuildPreviousNarrativeContext(previousNarrative)}

GUIDELINES:
- Use vivid sensory details (sight, sound, smell, touch)
- Vary sentence structure (short/long, simple/complex)
- Show character emotions through actions and reactions
- Maintain consistent tone and atmosphere
- Build tension and engagement
- Use active voice predominantly
- Keep narrative flowing naturally

Generate engaging narrative that advances the story while respecting established facts and character consistency.";

        return prompt;
    }

    /// <summary>
    /// Builds an optimized prompt for character dialogue.
    /// </summary>
    public string BuildOptimizedCharacterPrompt(
        CharacterState character,
        StoryState state,
        NarrativeIntent intent,
        string? characterPersonality = null)
    {
        var knownFacts = character.KnownFacts.TakeLast(5).ToList();

        var prompt = $@"You are writing dialogue for {character.Name}.

CHARACTER PROFILE:
- Name: {character.Name}
- Personality: {characterPersonality ?? "Authentic and consistent"}
- Current emotional state: {DetermineEmotionalState(character, state)}
- Known facts: {knownFacts.Count} pieces of information

WHAT {character.Name.ToUpper()} KNOWS:
{string.Join("\n", knownFacts.Select((f, i) => $"{i + 1}. {f}"))}

CURRENT SITUATION:
- Location: {GetCharacterLocation(character, state)}
- Intent: {intent.Description}

DIALOGUE GUIDELINES:
- Stay in character consistently
- Reflect current emotional state
- Use only information the character knows
- Make dialogue natural and purposeful
- Show personality through word choice and cadence
- Avoid exposition dumps - be natural

Generate authentic dialogue that reveals character while advancing the scene.";

        return prompt;
    }

    /// <summary>
    /// Builds an optimized prompt for summary generation.
    /// </summary>
    public string BuildOptimizedSummaryPrompt(
        StoryState state,
        int lastNEvents = 10)
    {
        var recentEvents = state.EventHistory.TakeLast(lastNEvents).ToList();

        var prompt = $@"Summarize the recent events in ""{state.WorldState.WorldName}"".

EVENTS TO SUMMARIZE ({recentEvents.Count} events):
{string.Join("\n", recentEvents.Select((e, i) => $"{i + 1}. {e.GetType().Name} at {e.Timestamp}"))}

SUMMARY REQUIREMENTS:
- Concise but informative (2-3 sentences max)
- Focus on key plot developments
- Mention important character actions
- Highlight story progression
- Maintain chronological flow

Generate a clear, engaging summary that captures the essence of recent story developments.";

        return prompt;
    }

    /// <summary>
    /// Builds an optimized prompt for consistency checking.
    /// </summary>
    public string BuildOptimizedConsistencyPrompt(
        StoryState state,
        string narrativeToCheck,
        IEnumerable<string> establishedFacts)
    {
        var facts = establishedFacts.Take(10).ToList();

        var prompt = $@"Check narrative consistency for ""{state.WorldState.WorldName}"".

ESTABLISHED FACTS:
{string.Join("\n", facts.Select((f, i) => $"{i + 1}. {f}"))}

NARRATIVE TO CHECK:
{narrativeToCheck}

CONSISTENCY CHECK:
- Does the narrative contradict any established facts?
- Are character behaviors consistent with their history?
- Do locations and objects match previous descriptions?
- Is the timeline coherent?

Report any inconsistencies found or confirm consistency.";

        return prompt;
    }

    private string BuildCharacterContext(StoryState state, NarrativeIntent intent)
    {
        var relevantCharacters = intent.TargetCharacterIds.Any()
            ? state.Characters.Where(c => intent.TargetCharacterIds.Contains(c.Key))
            : state.Characters.Take(3);

        if (!relevantCharacters.Any())
            return "- No specific characters in focus";

        return string.Join("\n", relevantCharacters.Select(c =>
            $"- {c.Value.Name}: {c.Value.KnownFacts.Count} known facts, currently at {GetCharacterLocation(c.Value, state)}"));
    }

    private string BuildRecentEventsContext(StoryState state, int count = 5)
    {
        var recentEvents = state.EventHistory.TakeLast(count).ToList();

        if (!recentEvents.Any())
            return "- Story is just beginning";

        return string.Join("\n", recentEvents.Select(e =>
            $"- {e.GetType().Name.Replace("Event", "")}: {e.Timestamp:HH:mm}"));
    }

    private string BuildPreviousNarrativeContext(string? previousNarrative)
    {
        if (string.IsNullOrWhiteSpace(previousNarrative))
            return "";

        var preview = previousNarrative.Length > 200
            ? previousNarrative.Substring(previousNarrative.Length - 200) + "..."
            : previousNarrative;

        return $@"
PREVIOUS NARRATIVE (for continuity):
{preview}

Maintain narrative flow and style consistency.";
    }

    private string DetermineEmotionalState(CharacterState character, StoryState state)
    {
        // Simple heuristic based on recent events
        var recentEvents = state.EventHistory.TakeLast(5);
        var hasConflict = recentEvents.Any(e => e.GetType().Name.Contains("Combat") || e.GetType().Name.Contains("Conflict"));
        var hasDiscovery = recentEvents.Any(e => e.GetType().Name.Contains("Discovery"));

        if (hasConflict) return "Tense and alert";
        if (hasDiscovery) return "Curious and focused";
        return "Calm and observant";
    }

    private string GetCharacterLocation(CharacterState character, StoryState state)
    {
        // Try to determine location from recent events or state
        // Default to "current scene" if not determinable
        return "current scene";
    }
}
