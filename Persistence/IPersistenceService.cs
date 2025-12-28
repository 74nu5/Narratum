using Narratum.Core;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Service abstrait pour la persistance des états narratifs.
/// Fournit les opérations de sauvegarde et chargement avec gestion des slots.
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// Sauvegarde un état narratif complet avec un nom/identifiant.
    /// </summary>
    /// <param name="slotName">Nom du slot de sauvegarde (ex: "game-1", "autosave")</param>
    /// <param name="state">État complète à sauvegarder</param>
    /// <returns>Résultat de succès ou d'erreur</returns>
    Task<Result<Unit>> SaveStateAsync(string slotName, StoryState state);

    /// <summary>
    /// Charge un état narratif sauvegardé.
    /// </summary>
    /// <param name="slotName">Nom du slot à charger</param>
    /// <returns>État sauvegardé ou erreur si non trouvé</returns>
    Task<Result<StoryState>> LoadStateAsync(string slotName);

    /// <summary>
    /// Supprime une sauvegarde.
    /// </summary>
    /// <param name="slotName">Nom du slot à supprimer</param>
    /// <returns>Résultat de succès ou d'erreur</returns>
    Task<Result<Unit>> DeleteStateAsync(string slotName);

    /// <summary>
    /// Liste tous les slots de sauvegarde existants.
    /// </summary>
    /// <returns>Collection des noms de slots</returns>
    Task<Result<IReadOnlyList<string>>> ListSavedStatesAsync();

    /// <summary>
    /// Vérifie si un slot existe.
    /// </summary>
    /// <param name="slotName">Nom du slot à vérifier</param>
    /// <returns>True si le slot existe, false sinon</returns>
    Task<Result<bool>> StateExistsAsync(string slotName);

    /// <summary>
    /// Obtient les métadonnées d'une sauvegarde (timestamp, taille, etc).
    /// </summary>
    /// <param name="slotName">Nom du slot</param>
    /// <returns>Métadonnées ou erreur</returns>
    Task<Result<SaveStateMetadata>> GetStateMetadataAsync(string slotName);
}

/// <summary>
/// Métadonnées d'une sauvegarde.
/// </summary>
/// <param name="SlotName">Nom unique du slot de sauvegarde</param>
/// <param name="SavedAt">Timestamp de sauvegarde</param>
/// <param name="TotalEvents">Nombre total d'événements dans l'état</param>
/// <param name="CurrentChapterId">Chapitre actuel au moment de la sauvegarde (nullable)</param>
public record SaveStateMetadata(
    string SlotName,
    DateTime SavedAt,
    int TotalEvents,
    Guid? CurrentChapterId
);
