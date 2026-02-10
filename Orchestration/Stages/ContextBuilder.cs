using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.State;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Implémentation du ContextBuilder.
///
/// Construit un contexte narratif enrichi en collectant :
/// - L'état des personnages actifs
/// - Les informations sur le lieu actuel
/// - La mémoire récente depuis IMemoryService
/// - L'état canonique du monde
/// </summary>
public class ContextBuilder : IContextBuilder
{
    private readonly IMemoryService? _memoryService;
    private readonly ILogger<ContextBuilder>? _logger;

    public ContextBuilder(
        IMemoryService? memoryService = null,
        ILogger<ContextBuilder>? logger = null)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<Result<NarrativeContext>> BuildAsync(
        StoryState currentState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(intent);

        try
        {
            _logger?.LogDebug("Building narrative context for intent {IntentType}", intent.Type);

            // 1. Identifier les personnages actifs
            var activeCharacters = BuildActiveCharacters(currentState, intent);

            // 2. Déterminer le lieu actuel
            var currentLocation = BuildCurrentLocation(currentState, intent, activeCharacters);

            // 3. Récupérer la mémoire récente (si disponible)
            var recentMemoria = new List<Memorandum>();
            CanonicalState? canonicalState = null;
            string? recentSummary = null;

            if (_memoryService != null)
            {
                var worldId = currentState.WorldState.WorldId;

                // Récupérer l'état canonique
                var canonicalResult = await _memoryService.GetCanonicalStateAsync(worldId, DateTime.UtcNow);
                if (canonicalResult is Result<CanonicalState>.Success canonicalSuccess)
                {
                    canonicalState = canonicalSuccess.Value;
                }

                // Générer un résumé des événements récents
                if (currentState.EventHistory.Count > 0)
                {
                    var summaryResult = await _memoryService.SummarizeHistoryAsync(
                        worldId,
                        currentState.EventHistory.Cast<object>().ToList(),
                        targetLength: 300);

                    if (summaryResult is Result<string>.Success summarySuccess)
                    {
                        recentSummary = summarySuccess.Value;
                    }
                }
            }

            // 4. Extraire les événements récents
            var recentEvents = currentState.EventHistory
                .TakeLast(10)
                .Cast<object>()
                .ToList();

            // 5. Construire les métadonnées
            var metadata = new Dictionary<string, object>
            {
                ["intentType"] = intent.Type.ToString(),
                ["characterCount"] = activeCharacters.Count,
                ["hasLocation"] = currentLocation != null,
                ["hasMemory"] = _memoryService != null,
                ["eventCount"] = recentEvents.Count
            };

            var context = new NarrativeContext(
                state: currentState,
                recentMemoria: recentMemoria,
                canonicalState: canonicalState,
                activeCharacters: activeCharacters,
                currentLocation: currentLocation,
                recentEvents: recentEvents,
                recentSummary: recentSummary,
                metadata: metadata);

            _logger?.LogDebug(
                "Context built with {CharacterCount} characters, {EventCount} events",
                activeCharacters.Count, recentEvents.Count);

            return Result<NarrativeContext>.Ok(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to build narrative context");
            return Result<NarrativeContext>.Fail($"Context build failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Construit la liste des personnages actifs.
    /// </summary>
    private List<CharacterContext> BuildActiveCharacters(StoryState state, NarrativeIntent intent)
    {
        var characters = new List<CharacterContext>();

        // Si l'intention cible des personnages spécifiques, les utiliser
        if (intent.TargetCharacterIds.Count > 0)
        {
            foreach (var charId in intent.TargetCharacterIds)
            {
                if (state.Characters.TryGetValue(charId, out var charState))
                {
                    characters.Add(CharacterContext.FromCharacterState(charState));
                }
            }
        }
        else
        {
            // Sinon, prendre tous les personnages vivants
            foreach (var charState in state.Characters.Values)
            {
                if (charState.VitalStatus == VitalStatus.Alive)
                {
                    characters.Add(CharacterContext.FromCharacterState(charState));
                }
            }
        }

        return characters;
    }

    /// <summary>
    /// Construit le contexte du lieu actuel.
    /// </summary>
    private LocationContext? BuildCurrentLocation(
        StoryState state,
        NarrativeIntent intent,
        IReadOnlyList<CharacterContext> activeCharacters)
    {
        // Si l'intention cible un lieu spécifique
        if (intent.TargetLocationId != null)
        {
            var locationId = intent.TargetLocationId;

            // Chercher les personnages présents à ce lieu
            var presentCharacters = activeCharacters
                .Where(c => c.CurrentLocationId != null && c.CurrentLocationId.Value == locationId.Value)
                .Select(c => c.CharacterId)
                .ToHashSet();

            return new LocationContext(
                locationId,
                $"Location-{locationId.Value.ToString()[..8]}",
                "A location in the story world.",
                presentCharacters);
        }

        // Sinon, déterminer le lieu le plus commun parmi les personnages actifs
        var locationGroups = activeCharacters
            .Where(c => c.CurrentLocationId != null)
            .GroupBy(c => c.CurrentLocationId!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (locationGroups != null)
        {
            var locationId = locationGroups.Key;
            var presentCharacters = locationGroups
                .Select(c => c.CharacterId)
                .ToHashSet();

            return new LocationContext(
                locationId,
                $"Location-{locationId.Value.ToString()[..8]}",
                "The current scene location.",
                presentCharacters);
        }

        return null;
    }
}
