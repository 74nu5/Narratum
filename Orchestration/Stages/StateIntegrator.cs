using System.Text;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Implémentation du StateIntegrator.
///
/// Convertit les sorties brutes des agents en une sortie narrative
/// structurée, incluant le texte narratif, les événements générés,
/// et les changements d'état.
/// </summary>
public class StateIntegrator : IStateIntegrator
{
    private readonly IMemoryService? _memoryService;
    private readonly ILogger<StateIntegrator>? _logger;

    public StateIntegrator(
        IMemoryService? memoryService = null,
        ILogger<StateIntegrator>? logger = null)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<NarrativeOutput> IntegrateAsync(
        RawOutput rawOutput,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawOutput);
        ArgumentNullException.ThrowIfNull(context);

        _logger?.LogDebug("Integrating raw output into narrative output");

        // 1. Combiner le texte narratif
        var narrativeText = CombineNarrativeText(rawOutput, context);

        // 2. Extraire les événements générés (basique pour Phase 3)
        var generatedEvents = ExtractEvents(rawOutput, context);

        // 3. Identifier les changements d'état
        var stateChanges = IdentifyStateChanges(rawOutput, context);

        // 4. Créer le memorandum (si le service mémoire est disponible)
        Memorandum? memorandum = null;
        if (_memoryService != null && generatedEvents.Count > 0)
        {
            try
            {
                var worldId = context.State.WorldState.WorldId;

                // Pour Phase 3, on crée un memorandum simple
                // Les événements générés sont des objets simples
                var result = await _memoryService.RememberEventAsync(
                    worldId,
                    new { NarrativeText = narrativeText, Timestamp = DateTime.UtcNow });

                if (result is Result<Memorandum>.Success success)
                {
                    memorandum = success.Value;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create memorandum, continuing without it");
            }
        }

        // 5. Construire les métadonnées
        var metadata = new Dictionary<string, object>
        {
            ["sourceAgents"] = rawOutput.Responses.Keys.Select(k => k.ToString()).ToList(),
            ["totalDuration"] = rawOutput.TotalDuration,
            ["eventCount"] = generatedEvents.Count,
            ["stateChangeCount"] = stateChanges.Count,
            ["hasMemorandum"] = memorandum != null
        };

        var output = new NarrativeOutput(
            narrativeText,
            memorandum,
            generatedEvents.Cast<object>(),
            metadata);

        _logger?.LogDebug(
            "Integration completed: {TextLength} chars, {EventCount} events, {ChangeCount} changes",
            narrativeText.Length, generatedEvents.Count, stateChanges.Count);

        return output;
    }

    /// <summary>
    /// Combine le texte narratif de tous les agents.
    /// </summary>
    private string CombineNarrativeText(RawOutput rawOutput, NarrativeContext context)
    {
        var sb = new StringBuilder();

        // Priorité: Narrator > Character > Summary > autres
        var priorityOrder = new[]
        {
            AgentType.Narrator,
            AgentType.Character,
            AgentType.Summary,
            AgentType.Consistency
        };

        foreach (var agentType in priorityOrder)
        {
            var response = rawOutput.GetResponse(agentType);
            if (response != null && response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                // Pour le personnage, on peut ajouter un marqueur
                if (agentType == AgentType.Character)
                {
                    sb.Append(response.Content);
                }
                else
                {
                    sb.Append(response.Content);
                }
            }
        }

        // Si aucun texte narratif, utiliser un placeholder
        if (sb.Length == 0)
        {
            sb.Append("[No narrative content generated]");
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extrait les événements générés.
    /// </summary>
    private List<GeneratedEvent> ExtractEvents(RawOutput rawOutput, NarrativeContext context)
    {
        var events = new List<GeneratedEvent>();

        // Pour Phase 3, on crée des événements simples basés sur le contenu
        // L'extraction avancée sera implémentée dans les phases suivantes

        var narratorResponse = rawOutput.GetResponse(AgentType.Narrator);
        if (narratorResponse?.Success == true && !string.IsNullOrEmpty(narratorResponse.Content))
        {
            events.Add(new GeneratedEvent(
                Id.New(),
                "NarrativeGenerated",
                $"Narrative content generated: {narratorResponse.Content.Length} chars",
                DateTime.UtcNow));
        }

        var characterResponse = rawOutput.GetResponse(AgentType.Character);
        if (characterResponse?.Success == true && !string.IsNullOrEmpty(characterResponse.Content))
        {
            events.Add(new GeneratedEvent(
                Id.New(),
                "DialogueGenerated",
                "Character dialogue generated",
                DateTime.UtcNow));
        }

        return events;
    }

    /// <summary>
    /// Identifie les changements d'état.
    /// </summary>
    private List<StateChange> IdentifyStateChanges(RawOutput rawOutput, NarrativeContext context)
    {
        var changes = new List<StateChange>();

        // Pour Phase 3, on identifie les changements basiques
        // L'identification avancée sera implémentée plus tard

        // Avancer le temps narratif
        changes.Add(StateChange.TimeAdvanced(rawOutput.TotalDuration));

        return changes;
    }
}

/// <summary>
/// Événement généré par le pipeline.
/// </summary>
public sealed record GeneratedEvent(
    Id EventId,
    string EventType,
    string Description,
    DateTime Timestamp)
{
    public static GeneratedEvent Create(string type, string description)
        => new(Id.New(), type, description, DateTime.UtcNow);
}
