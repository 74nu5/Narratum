using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Implémentation de ISnapshotService pour conversion déterministe StoryState ↔ StateSnapshot.
/// Utilise JSON pour sérialisation avec ordre garantisseur déterminisme.
/// </summary>
public class SnapshotService : ISnapshotService
{
    /// <summary>
    /// Options JSON pour sérialisation déterministe.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Version actuelle du format de snapshot.
    /// À incrémenter quand le format change pour gestion des migrations.
    /// </summary>
    private const int CurrentSnapshotVersion = 1;

    /// <inheritdoc />
    public StateSnapshot CreateSnapshot(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Sérialiser les états des personnages de manière déterministe
        var characterStatesData = SerializeCharacterStates(state);

        // Sérialiser les événements de manière déterministe
        var eventsData = SerializeEvents(state);

        // Sérialiser l'état du monde
        var worldStateData = SerializeWorldState(state);

        var snapshot = new StateSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorldId = state.WorldState.WorldId.Value,
            CurrentArcId = state.CurrentChapter?.ArcId.Value,
            CurrentChapterId = state.CurrentChapter?.Id.Value,
            NarrativeTime = state.WorldState.NarrativeTime.Ticks,
            TotalEventCount = state.EventHistory.Count,
            CharacterStatesData = characterStatesData,
            EventsData = eventsData,
            WorldStateData = worldStateData,
            SnapshotVersion = CurrentSnapshotVersion
        };

        // Calculer le hash d'intégrité
        snapshot = snapshot with
        {
            IntegrityHash = ComputeIntegrityHash(snapshot)
        };

        return snapshot;
    }

    /// <inheritdoc />
    public Result<StoryState> RestoreFromSnapshot(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Valider le snapshot
        var validationResult = ValidateSnapshot(snapshot);
        if (validationResult is Result<Unit>.Failure failureValidation)
        {
            return Result<StoryState>.Fail($"Snapshot validation failed: {failureValidation.Message}");
        }

        try
        {
            // Désérialiser les données
            var characterStates = DeserializeCharacterStates(snapshot.CharacterStatesData);
            var events = DeserializeEvents(snapshot.EventsData);
            var worldState = DeserializeWorldState(snapshot.WorldStateData);

            // Créer le StoryState restauré
            // Pour Phase 1.5, création simple - Phase 2+ gérera les chapitres
            var restoredState = new StoryState(
                worldState: worldState
            );

            return Result<StoryState>.Ok(restoredState);
        }
        catch (Exception ex)
        {
            return Result<StoryState>.Fail($"Failed to restore from snapshot: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Result<Unit> ValidateSnapshot(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Vérifier les champs obligatoires
        if (snapshot.SnapshotId == Guid.Empty)
            return Result<Unit>.Fail("SnapshotId cannot be empty");

        if (string.IsNullOrWhiteSpace(snapshot.CharacterStatesData))
            return Result<Unit>.Fail("CharacterStatesData cannot be empty");

        if (string.IsNullOrWhiteSpace(snapshot.EventsData))
            return Result<Unit>.Fail("EventsData cannot be empty");

        if (string.IsNullOrWhiteSpace(snapshot.WorldStateData))
            return Result<Unit>.Fail("WorldStateData cannot be empty");

        if (snapshot.SnapshotVersion != CurrentSnapshotVersion)
            return Result<Unit>.Fail(
                $"Snapshot version {snapshot.SnapshotVersion} not supported. Current version: {CurrentSnapshotVersion}");

        // Vérifier l'intégrité si le hash est présent
        if (!string.IsNullOrWhiteSpace(snapshot.IntegrityHash))
        {
            var expectedHash = ComputeIntegrityHash(snapshot);
            if (snapshot.IntegrityHash != expectedHash)
                return Result<Unit>.Fail("Snapshot integrity check failed");
        }

        return Result<Unit>.Ok(Unit.Default());
    }

    /// <summary>
    /// Sérialise les états des personnages de manière déterministe.
    /// </summary>
    private static string SerializeCharacterStates(StoryState state)
    {
        // Trier les personnages par ID pour déterminisme
        var sortedStates = state.Characters
            .OrderBy(kvp => kvp.Key.Value.ToString())
            .Select(kvp =>
            {
                var charState = kvp.Value;
                return new
                {
                    characterId = charState.CharacterId.Value,
                    name = charState.Name,
                    vitalStatus = charState.VitalStatus.ToString(),
                    currentLocationId = charState.CurrentLocationId?.Value,
                    knownFacts = charState.KnownFacts
                        .OrderBy(x => x)
                        .ToList()
                };
            })
            .ToList();

        return JsonSerializer.Serialize(sortedStates, JsonOptions);
    }

    /// <summary>
    /// Sérialise les événements de manière déterministe.
    /// </summary>
    private static string SerializeEvents(StoryState state)
    {
        // Les événements sont déjà en ordre chronologique
        var serializedEvents = state.EventHistory
            .Select(evt => new
            {
                id = evt.Id.ToString(),
                type = evt.GetType().Name,
                timestamp = evt.Timestamp
            })
            .ToList();

        return JsonSerializer.Serialize(serializedEvents, JsonOptions);
    }

    /// <summary>
    /// Sérialise l'état du monde.
    /// </summary>
    private static string SerializeWorldState(StoryState state)
    {
        var worldData = new
        {
            worldId = state.WorldState.WorldId.Value,
            narrativeTime = state.WorldState.NarrativeTime.Ticks,
            totalEventCount = state.EventHistory.Count,
            currentChapterId = state.CurrentChapter?.Id.Value
        };

        return JsonSerializer.Serialize(worldData, JsonOptions);
    }

    /// <summary>
    /// Désérialise les états des personnages.
    /// </summary>
    private static Dictionary<Id, CharacterState> DeserializeCharacterStates(string data)
    {
        // Pour la Phase 1.5, retourner un dictionnaire vide
        // En Phase 2+, implémenter la désérialisation complète
        return new Dictionary<Id, CharacterState>();
    }

    /// <summary>
    /// Désérialise les événements.
    /// </summary>
    private static List<Event> DeserializeEvents(string data)
    {
        // Pour la Phase 1.5, retourner une liste vide
        // En Phase 2+, implémenter la désérialisation complète
        return new List<Event>();
    }

    /// <summary>
    /// Désérialise l'état du monde.
    /// </summary>
    private static WorldState DeserializeWorldState(string data)
    {
        // Pour la Phase 1.5, créer un WorldState minimal
        // En Phase 2+, implémenter la désérialisation complète
        var world = new StoryWorld(name: "Restored World");
        return new WorldState(worldId: world.Id, worldName: world.Name);
    }

    /// <summary>
    /// Calcule le hash SHA256 d'intégrité du snapshot.
    /// </summary>
    private static string ComputeIntegrityHash(StateSnapshot snapshot)
    {
        // Créer une copie sans le hash
        var snapshotWithoutHash = snapshot with { IntegrityHash = null };

        var dataToHash = string.Concat(
            snapshotWithoutHash.SnapshotId,
            snapshotWithoutHash.WorldId,
            snapshotWithoutHash.CurrentChapterId,
            snapshotWithoutHash.NarrativeTime,
            snapshotWithoutHash.CharacterStatesData,
            snapshotWithoutHash.EventsData,
            snapshotWithoutHash.WorldStateData
        );

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dataToHash));
        return Convert.ToBase64String(hashBytes);
    }
}
