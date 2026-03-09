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
                Tu es un moteur narratif pour le monde "{worldName}".
                Génère du contenu narratif cohérent et captivant qui fait avancer l'histoire.
                Maintiens la cohérence avec les faits établis et les comportements des personnages.
                Écris à la troisième personne, au passé.
                Privilégie le montrer plutôt que dire.
                """,

            AgentType.Character => $"""
                Tu es un générateur de dialogues pour "{worldName}".
                Génère des dialogues et réactions authentiques, dans le personnage.
                Chaque personnage a des traits de personnalité et un style d'élocution distincts.
                Les dialogues doivent révéler le personnage et faire avancer l'intrigue.
                Utilise un ton émotionnel adapté au contexte.
                """,

            AgentType.Summary => $"""
                Tu es un résumeur narratif pour "{worldName}".
                Génère des résumés concis et factuels des événements de l'histoire.
                Concentre-toi sur les points clés de l'intrigue, les actions des personnages et les changements d'état.
                Maintiens une précision chronologique.
                Évite toute interprétation ou embellissement.
                """,

            AgentType.Consistency => $"""
                Tu es un vérificateur de cohérence pour "{worldName}".
                Vérifie que le contenu narratif est aligné avec les faits établis.
                Identifie les contradictions avec les états des personnages, les lieux ou les événements.
                Signale toute incohérence logique.
                Rapporte les problèmes clairement et précisément.
                """,

            _ => $"Tu es un assistant IA pour le monde narratif \"{worldName}\"."
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
        sb.AppendLine($"## Intention : {GetIntentDescription(intent)}");

        if (!string.IsNullOrEmpty(intent.Description))
        {
            sb.AppendLine($"Détails : {intent.Description}");
        }

        sb.AppendLine();

        // Ajouter le contexte des personnages
        if (context.ActiveCharacters.Count > 0)
        {
            sb.AppendLine("## Personnages actifs :");
            foreach (var character in context.ActiveCharacters)
            {
                sb.AppendLine($"- {character.Name} ({character.Status})");
                if (character.KnownFacts.Count > 0)
                {
                    var facts = string.Join(", ", character.KnownFacts.Take(5));
                    sb.AppendLine($"  Faits connus : {facts}");
                }
            }
            sb.AppendLine();
        }

        // Ajouter le contexte du lieu
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"## Lieu : {context.CurrentLocation.Name}");
            sb.AppendLine($"{context.CurrentLocation.Description}");
            sb.AppendLine();
        }

        // Ajouter le résumé récent
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            sb.AppendLine("## Résumé des événements récents :");
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
            IntentType.ContinueNarrative => "Continuer la narration naturellement",
            IntentType.GenerateDialogue => "Générer un dialogue entre les personnages",
            IntentType.DescribeScene => "Décrire la scène actuelle en détail",
            IntentType.CreateTension => "Créer une tension dramatique",
            IntentType.ResolveConflict => "Résoudre le conflit actuel",
            IntentType.Summarize => "Résumer les événements récents",
            IntentType.IntroduceEvent => "Introduire un nouvel événement",
            _ => "Générer du contenu narratif"
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
                IntentType.ContinueNarrative => "Continuez l'histoire là où elle s'est arrêtée. Maintenez le rythme et le ton.",
                IntentType.DescribeScene => "Décrivez la scène avec des détails sensoriels. Installez l'atmosphère.",
                IntentType.CreateTension => "Construisez le suspense et la tension dramatique. Utilisez des techniques de rythme.",
                IntentType.ResolveConflict => "Amenez le conflit vers une résolution satisfaisante.",
                _ => "Générez du contenu narratif approprié."
            },

            AgentType.Character => intent.Type switch
            {
                IntentType.GenerateDialogue => "Générez une conversation naturelle. Chaque personnage doit avoir une voix distincte.",
                IntentType.ResolveConflict => "Générez les réactions et dialogues des personnages pour la résolution.",
                _ => "Générez des dialogues et réactions dans le personnage."
            },

            AgentType.Summary => "Fournissez un résumé concis des événements clés et des changements d'état.",

            AgentType.Consistency => "Vérifiez la cohérence du contenu généré avec les faits établis.",

            _ => ""
        };
    }
}
