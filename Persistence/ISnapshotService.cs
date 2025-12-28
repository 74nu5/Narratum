using Narratum.Core;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Service abstrait pour la conversion entre StoryState et StateSnapshot.
/// Gère la sérialisation/désérialisation déterministe de l'état.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Crée un snapshot à partir d'un état narratif complet.
    /// </summary>
    /// <param name="state">État à convertir</param>
    /// <returns>Snapshot sérialisable de l'état</returns>
    StateSnapshot CreateSnapshot(StoryState state);

    /// <summary>
    /// Restaure un état narratif complet à partir d'un snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot à restaurer</param>
    /// <returns>État restauré ou erreur si snapshot invalide</returns>
    Result<StoryState> RestoreFromSnapshot(StateSnapshot snapshot);

    /// <summary>
    /// Valide qu'un snapshot est bien formé et peut être restauré.
    /// </summary>
    /// <param name="snapshot">Snapshot à valider</param>
    /// <returns>Résultat de validation</returns>
    Result<Unit> ValidateSnapshot(StateSnapshot snapshot);
}

/// <summary>
/// Snapshot d'un état narratif - format sérialisable pour persistance.
/// Format: JSON ou binaire, déterministe pour assurer même résultat au restauration.
/// </summary>
public record StateSnapshot
{
    /// <summary>
    /// Identifiant unique du snapshot.
    /// </summary>
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Timestamp de création du snapshot.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Identifiant du monde narratif.
    /// </summary>
    public required Guid WorldId { get; init; }

    /// <summary>
    /// Identifiant de l'arc narratif actif.
    /// </summary>
    public Guid? CurrentArcId { get; init; }

    /// <summary>
    /// Identifiant du chapitre actif.
    /// </summary>
    public Guid? CurrentChapterId { get; init; }

    /// <summary>
    /// Temps narratif actuel (en jours ou unités narratives).
    /// </summary>
    public required long NarrativeTime { get; init; }

    /// <summary>
    /// Nombre total d'événements enregistrés.
    /// </summary>
    public required int TotalEventCount { get; init; }

    /// <summary>
    /// Données sérialisées de tous les états des personnages.
    /// Format: JSON sérialisé ou représentation binaire déterministe.
    /// </summary>
    public required string CharacterStatesData { get; init; }

    /// <summary>
    /// Données sérialisées des événements.
    /// Format: JSON sérialisé ou représentation binaire déterministe.
    /// </summary>
    public required string EventsData { get; init; }

    /// <summary>
    /// Données sérialisées de l'état du monde.
    /// Format: JSON sérialisé ou représentation binaire déterministe.
    /// </summary>
    public required string WorldStateData { get; init; }

    /// <summary>
    /// Version du format de snapshot pour gestion des migrations.
    /// </summary>
    public required int SnapshotVersion { get; init; }

    /// <summary>
    /// Hash de vérification d'intégrité (optionnel).
    /// </summary>
    public string? IntegrityHash { get; init; }
}
