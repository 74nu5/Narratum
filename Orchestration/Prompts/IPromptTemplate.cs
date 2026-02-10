using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts;

/// <summary>
/// Interface pour les templates de prompts.
///
/// Chaque template définit comment construire les prompts
/// pour un type d'agent spécifique.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// Nom unique du template.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type d'agent ciblé par ce template.
    /// </summary>
    AgentType TargetAgent { get; }

    /// <summary>
    /// Types d'intentions que ce template peut gérer.
    /// </summary>
    IReadOnlySet<IntentType> SupportedIntents { get; }

    /// <summary>
    /// Construit le prompt système pour l'agent.
    ///
    /// Le prompt système définit le rôle, les règles et le format attendu.
    /// </summary>
    string BuildSystemPrompt(NarrativeContext context);

    /// <summary>
    /// Construit le prompt utilisateur avec le contexte et l'intention.
    ///
    /// Le prompt utilisateur contient les données spécifiques à la requête.
    /// </summary>
    string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent);

    /// <summary>
    /// Extrait les variables du contexte pour injection dans les prompts.
    /// </summary>
    IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context);

    /// <summary>
    /// Vérifie si ce template peut gérer une intention donnée.
    /// </summary>
    bool CanHandle(NarrativeIntent intent);
}

/// <summary>
/// Classe de base abstraite pour les templates de prompts.
/// Fournit une implémentation commune.
/// </summary>
public abstract class PromptTemplateBase : IPromptTemplate
{
    public abstract string Name { get; }
    public abstract AgentType TargetAgent { get; }
    public abstract IReadOnlySet<IntentType> SupportedIntents { get; }

    public abstract string BuildSystemPrompt(NarrativeContext context);
    public abstract string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent);

    public virtual IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        var variables = new Dictionary<string, string>
        {
            { "location_name", context.CurrentLocation?.Name ?? "Unknown" },
            { "character_count", context.ActiveCharacters.Count.ToString() },
            { "event_count", context.RecentEvents.Count.ToString() },
            { "context_time", context.ContextBuiltAt.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        // Ajouter les noms des personnages actifs
        var characterNames = context.ActiveCharacters
            .Select(c => c.Name)
            .ToList();
        variables["active_characters"] = string.Join(", ", characterNames);

        // Ajouter le résumé récent si disponible
        if (!string.IsNullOrEmpty(context.RecentSummary))
        {
            variables["recent_summary"] = context.RecentSummary;
        }

        return variables;
    }

    public virtual bool CanHandle(NarrativeIntent intent)
    {
        return SupportedIntents.Contains(intent.Type);
    }

    /// <summary>
    /// Formate une liste de personnages pour inclusion dans un prompt.
    /// </summary>
    protected string FormatCharacterList(IReadOnlyList<CharacterContext> characters)
    {
        if (characters.Count == 0)
            return "No characters present.";

        var lines = characters.Select(c =>
            $"- {c.Name} ({c.Status})" +
            (c.CharacterTraits.Count > 0 ? $" - Traits: {string.Join(", ", c.CharacterTraits)}" : ""));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formate une liste d'événements pour inclusion dans un prompt.
    /// </summary>
    protected string FormatEventList(IReadOnlyList<object> events)
    {
        if (events.Count == 0)
            return "No recent events.";

        var lines = events.Select((e, i) => $"{i + 1}. {FormatEvent(e)}");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formate un événement individuel.
    /// </summary>
    protected virtual string FormatEvent(object evt)
    {
        // Utiliser le type dynamique pour accéder aux propriétés
        var type = evt.GetType();
        var typeProp = type.GetProperty("Type");
        var eventType = typeProp?.GetValue(evt)?.ToString() ?? "Unknown";

        return $"Event: {eventType}";
    }

    /// <summary>
    /// Formate les faits connus pour inclusion dans un prompt.
    /// </summary>
    protected string FormatKnownFacts(IReadOnlySet<string> facts)
    {
        if (facts.Count == 0)
            return "No established facts.";

        var lines = facts.Select(f => $"- {f}");
        return string.Join(Environment.NewLine, lines);
    }
}
