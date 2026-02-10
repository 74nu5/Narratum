using System.Text;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Implémentation du PromptBuilder.
///
/// Construit des prompts structurés pour chaque type d'agent
/// en fonction du contexte narratif et de l'intention.
/// </summary>
public class PromptBuilder : IPromptBuilder
{
    private readonly ILogger<PromptBuilder>? _logger;

    public PromptBuilder(ILogger<PromptBuilder>? logger = null)
    {
        _logger = logger;
    }

    public Task<Result<PromptSet>> BuildAsync(
        NarrativeContext context,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);

        try
        {
            _logger?.LogDebug("Building prompts for intent {IntentType}", intent.Type);

            var prompts = new List<AgentPrompt>();

            // Sélectionner les agents selon le type d'intention
            var agentSelection = SelectAgentsForIntent(intent);

            foreach (var agentType in agentSelection)
            {
                var prompt = BuildPromptForAgent(agentType, context, intent);
                prompts.Add(prompt);
            }

            // Déterminer l'ordre d'exécution
            var executionOrder = DetermineExecutionOrder(intent, prompts);

            var promptSet = new PromptSet(prompts, executionOrder);

            _logger?.LogDebug("Built {Count} prompts with {Order} execution", prompts.Count, executionOrder);

            return Task.FromResult(Result<PromptSet>.Ok(promptSet));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to build prompts");
            return Task.FromResult(Result<PromptSet>.Fail($"Prompt build failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Sélectionne les agents appropriés pour l'intention.
    /// </summary>
    private IReadOnlyList<AgentType> SelectAgentsForIntent(NarrativeIntent intent)
    {
        return intent.Type switch
        {
            IntentType.ContinueNarrative => new[] { AgentType.Narrator },
            IntentType.GenerateDialogue => new[] { AgentType.Character, AgentType.Narrator },
            IntentType.DescribeScene => new[] { AgentType.Narrator },
            IntentType.CreateTension => new[] { AgentType.Narrator },
            IntentType.ResolveConflict => new[] { AgentType.Narrator, AgentType.Character },
            IntentType.Summarize => new[] { AgentType.Summary },
            IntentType.IntroduceEvent => new[] { AgentType.Narrator },
            _ => new[] { AgentType.Narrator }
        };
    }

    /// <summary>
    /// Construit le prompt pour un agent spécifique.
    /// </summary>
    private AgentPrompt BuildPromptForAgent(
        AgentType agentType,
        NarrativeContext context,
        NarrativeIntent intent)
    {
        var systemPrompt = BuildSystemPrompt(agentType, context);
        var userPrompt = BuildUserPrompt(agentType, context, intent);
        var variables = BuildVariables(agentType, context, intent);

        return new AgentPrompt(
            agentType,
            systemPrompt,
            userPrompt,
            variables,
            DeterminePriority(agentType, intent));
    }

    /// <summary>
    /// Construit le prompt système pour un agent.
    /// </summary>
    private string BuildSystemPrompt(AgentType agentType, NarrativeContext context)
    {
        var worldName = context.State.WorldState.WorldName;

        return agentType switch
        {
            AgentType.Narrator => $"""
                You are a narrative engine for the world "{worldName}".
                Generate coherent, engaging narrative content that advances the story.
                Maintain consistency with established facts and character behaviors.
                Write in third person, past tense.
                Focus on showing rather than telling.
                """,

            AgentType.Character => $"""
                You are a character dialogue generator for "{worldName}".
                Generate authentic, in-character dialogue and reactions.
                Each character has distinct personality traits and speech patterns.
                Dialogue should reveal character and advance plot.
                Use appropriate emotional tone based on context.
                """,

            AgentType.Summary => $"""
                You are a narrative summarizer for "{worldName}".
                Generate concise, factual summaries of story events.
                Focus on key plot points, character actions, and state changes.
                Maintain chronological accuracy.
                Avoid interpretation or embellishment.
                """,

            AgentType.Consistency => $"""
                You are a consistency checker for "{worldName}".
                Verify that narrative content aligns with established facts.
                Identify contradictions with character states, locations, or events.
                Flag any logical inconsistencies.
                Report issues clearly and specifically.
                """,

            _ => $"You are an AI assistant for the narrative world \"{worldName}\"."
        };
    }

    /// <summary>
    /// Construit le prompt utilisateur pour un agent.
    /// </summary>
    private string BuildUserPrompt(
        AgentType agentType,
        NarrativeContext context,
        NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        // Ajouter l'intention
        sb.AppendLine($"## Intent: {GetIntentDescription(intent)}");

        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"Details: {intent.Description}");
        }

        sb.AppendLine();

        // Ajouter le contexte des personnages
        if (context.ActiveCharacters.Count > 0)
        {
            sb.AppendLine("## Active Characters:");
            foreach (var character in context.ActiveCharacters)
            {
                sb.AppendLine($"- {character.Name} ({character.Status})");
                if (character.KnownFacts.Count > 0)
                {
                    var facts = string.Join(", ", character.KnownFacts.Take(5));
                    sb.AppendLine($"  Known facts: {facts}");
                }
            }
            sb.AppendLine();
        }

        // Ajouter le contexte du lieu
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"## Location: {context.CurrentLocation.Name}");
            sb.AppendLine($"{context.CurrentLocation.Description}");
            sb.AppendLine();
        }

        // Ajouter le résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("## Recent Events Summary:");
            sb.AppendLine(context.RecentSummary);
            sb.AppendLine();
        }

        // Instructions spécifiques selon le type d'agent
        sb.AppendLine(GetAgentSpecificInstructions(agentType, intent));

        return sb.ToString();
    }

    /// <summary>
    /// Construit les variables pour le prompt.
    /// </summary>
    private Dictionary<string, string> BuildVariables(
        AgentType agentType,
        NarrativeContext context,
        NarrativeIntent intent)
    {
        var variables = new Dictionary<string, string>
        {
            ["world_name"] = context.State.WorldState.WorldName,
            ["intent_type"] = intent.Type.ToString(),
            ["character_count"] = context.ActiveCharacters.Count.ToString(),
            ["agent_type"] = agentType.ToString()
        };

        if (context.CurrentLocation != null)
        {
            variables["location_name"] = context.CurrentLocation.Name;
        }

        if (context.ActiveCharacters.Count > 0)
        {
            variables["character_names"] = string.Join(", ",
                context.ActiveCharacters.Select(c => c.Name));
        }

        return variables;
    }

    /// <summary>
    /// Détermine l'ordre d'exécution des prompts.
    /// </summary>
    private ExecutionOrder DetermineExecutionOrder(
        NarrativeIntent intent,
        IReadOnlyList<AgentPrompt> prompts)
    {
        // Un seul prompt -> séquentiel par défaut
        if (prompts.Count <= 1)
            return ExecutionOrder.Sequential;

        // Dialogue -> séquentiel (character puis narrator)
        if (intent.Type == IntentType.GenerateDialogue)
            return ExecutionOrder.Sequential;

        // Sinon -> parallèle possible
        return ExecutionOrder.Parallel;
    }

    /// <summary>
    /// Détermine la priorité du prompt.
    /// </summary>
    private PromptPriority DeterminePriority(AgentType agentType, NarrativeIntent intent)
    {
        // Le narrateur est toujours requis
        if (agentType == AgentType.Narrator)
            return PromptPriority.Required;

        // Le personnage est requis pour les dialogues
        if (agentType == AgentType.Character && intent.Type == IntentType.GenerateDialogue)
            return PromptPriority.Required;

        // Summary est requis pour les résumés
        if (agentType == AgentType.Summary && intent.Type == IntentType.Summarize)
            return PromptPriority.Required;

        // Consistency est optionnel
        if (agentType == AgentType.Consistency)
            return PromptPriority.Optional;

        return PromptPriority.Optional;
    }

    /// <summary>
    /// Obtient la description de l'intention.
    /// </summary>
    private string GetIntentDescription(NarrativeIntent intent)
    {
        return intent.Type switch
        {
            IntentType.ContinueNarrative => "Continue the narrative naturally",
            IntentType.GenerateDialogue => "Generate dialogue between characters",
            IntentType.DescribeScene => "Describe the current scene in detail",
            IntentType.CreateTension => "Create dramatic tension",
            IntentType.ResolveConflict => "Resolve the current conflict",
            IntentType.Summarize => "Summarize recent events",
            IntentType.IntroduceEvent => "Introduce a new event",
            _ => "Generate narrative content"
        };
    }

    /// <summary>
    /// Obtient les instructions spécifiques à l'agent.
    /// </summary>
    private string GetAgentSpecificInstructions(AgentType agentType, NarrativeIntent intent)
    {
        return agentType switch
        {
            AgentType.Narrator => intent.Type switch
            {
                IntentType.ContinueNarrative => "Continue the story from where it left off. Maintain pacing and tone.",
                IntentType.DescribeScene => "Describe the scene with sensory details. Set the atmosphere.",
                IntentType.CreateTension => "Build suspense and dramatic tension. Use pacing techniques.",
                IntentType.ResolveConflict => "Bring the conflict to a satisfying resolution.",
                _ => "Generate appropriate narrative content."
            },

            AgentType.Character => intent.Type switch
            {
                IntentType.GenerateDialogue => "Generate a natural conversation. Each character should have a distinct voice.",
                IntentType.ResolveConflict => "Generate character reactions and dialogue for the resolution.",
                _ => "Generate in-character dialogue and reactions."
            },

            AgentType.Summary => "Provide a concise summary of the key events and state changes.",

            AgentType.Consistency => "Check the generated content for consistency with established facts.",

            _ => ""
        };
    }
}
