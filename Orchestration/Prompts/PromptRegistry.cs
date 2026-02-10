using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Prompts;

/// <summary>
/// Registre central des templates de prompts.
///
/// Permet d'enregistrer et de récupérer des templates
/// par type d'agent et type d'intention.
/// </summary>
public sealed class PromptRegistry
{
    private readonly Dictionary<(AgentType, IntentType), IPromptTemplate> _templates = new();
    private readonly Dictionary<AgentType, IPromptTemplate> _defaultTemplates = new();
    private readonly object _lock = new();

    /// <summary>
    /// Enregistre un template pour un agent et une intention spécifiques.
    /// </summary>
    public void Register(IPromptTemplate template, IntentType intentType)
    {
        ArgumentNullException.ThrowIfNull(template);

        lock (_lock)
        {
            _templates[(template.TargetAgent, intentType)] = template;
        }
    }

    /// <summary>
    /// Enregistre un template comme template par défaut pour un agent.
    /// </summary>
    public void RegisterDefault(IPromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        lock (_lock)
        {
            _defaultTemplates[template.TargetAgent] = template;

            // Enregistrer aussi pour toutes les intentions supportées
            foreach (var intent in template.SupportedIntents)
            {
                _templates[(template.TargetAgent, intent)] = template;
            }
        }
    }

    /// <summary>
    /// Récupère un template pour un agent et une intention donnés.
    /// </summary>
    public IPromptTemplate? GetTemplate(AgentType agent, IntentType intent)
    {
        lock (_lock)
        {
            // Chercher d'abord un template spécifique
            if (_templates.TryGetValue((agent, intent), out var template))
            {
                return template;
            }

            // Sinon retourner le template par défaut
            return _defaultTemplates.GetValueOrDefault(agent);
        }
    }

    /// <summary>
    /// Récupère le template par défaut pour un agent.
    /// </summary>
    public IPromptTemplate? GetDefaultTemplate(AgentType agent)
    {
        lock (_lock)
        {
            return _defaultTemplates.GetValueOrDefault(agent);
        }
    }

    /// <summary>
    /// Récupère tous les templates enregistrés.
    /// </summary>
    public IReadOnlyList<IPromptTemplate> GetAllTemplates()
    {
        lock (_lock)
        {
            return _templates.Values
                .Concat(_defaultTemplates.Values)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>
    /// Récupère tous les templates pour un agent donné.
    /// </summary>
    public IReadOnlyList<IPromptTemplate> GetTemplatesForAgent(AgentType agent)
    {
        lock (_lock)
        {
            var templates = _templates
                .Where(kvp => kvp.Key.Item1 == agent)
                .Select(kvp => kvp.Value)
                .Distinct()
                .ToList();

            if (_defaultTemplates.TryGetValue(agent, out var defaultTemplate))
            {
                if (!templates.Contains(defaultTemplate))
                {
                    templates.Add(defaultTemplate);
                }
            }

            return templates;
        }
    }

    /// <summary>
    /// Vérifie si un template existe pour une combinaison agent/intention.
    /// </summary>
    public bool HasTemplate(AgentType agent, IntentType intent)
    {
        lock (_lock)
        {
            return _templates.ContainsKey((agent, intent)) ||
                   _defaultTemplates.ContainsKey(agent);
        }
    }

    /// <summary>
    /// Supprime tous les templates.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _templates.Clear();
            _defaultTemplates.Clear();
        }
    }

    /// <summary>
    /// Nombre total de templates enregistrés.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _templates.Values
                    .Concat(_defaultTemplates.Values)
                    .Distinct()
                    .Count();
            }
        }
    }

    /// <summary>
    /// Crée un registre avec les templates par défaut pré-enregistrés.
    /// </summary>
    public static PromptRegistry CreateWithDefaults()
    {
        var registry = new PromptRegistry();

        // Enregistrer les templates par défaut pour chaque agent
        registry.RegisterDefault(new Templates.SummaryPromptTemplate());
        registry.RegisterDefault(new Templates.NarratorPromptTemplate());
        registry.RegisterDefault(new Templates.CharacterPromptTemplate());
        registry.RegisterDefault(new Templates.ConsistencyPromptTemplate());

        return registry;
    }
}
