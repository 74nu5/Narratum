using Narratum.Core;
using Narratum.Memory;

namespace Narratum.Memory.Services;

/// <summary>
/// Service public pour orchestrer toutes les opérations de mémoire du système narratif.
/// 
/// Responsibilities:
/// - Mémoriser des événements individuels
/// - Mémoriser des groupes d'événements (chapitres)
/// - Récupérer les memorias stockées
/// - Résumer l'historique narratif
/// - Obtenir l'état canonique du monde
/// - Valider la cohérence logique
/// - Asserter des faits au système
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Mémoriser un événement unique et créer un memorandum correspondant.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="domainEvent">L'événement à mémoriser</param>
    /// <param name="context">Contexte optionnel d'extraction</param>
    /// <returns>Le memorandum créé, ou erreur si échec</returns>
    Task<Result<Memorandum>> RememberEventAsync(
        Id worldId,
        object domainEvent,
        IReadOnlyDictionary<string, object>? context = null);

    /// <summary>
    /// Mémoriser un groupe d'événements (chapitre) comme une unité.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="events">Les événements du chapitre</param>
    /// <param name="context">Contexte optionnel d'extraction</param>
    /// <returns>Le memorandum de chapitre créé, ou erreur si échec</returns>
    Task<Result<Memorandum>> RememberChapterAsync(
        Id worldId,
        IReadOnlyList<object> events,
        IReadOnlyDictionary<string, object>? context = null);

    /// <summary>
    /// Retrouver un memorandum spécifique par son ID.
    /// </summary>
    /// <param name="memorandumId">L'ID du memorandum à retrouver</param>
    /// <returns>Le memorandum trouvé, ou null si non trouvé</returns>
    Task<Result<Memorandum?>> RetrieveMemoriumAsync(Id memorandumId);

    /// <summary>
    /// Trouver tous les memorias d'un monde qui mentionnent une entité spécifique.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="entityName">Le nom ou ID de l'entité à rechercher</param>
    /// <returns>Liste de tous les memorias mentionnant l'entité</returns>
    Task<Result<IReadOnlyList<Memorandum>>> FindMemoriaByEntityAsync(
        Id worldId,
        string entityName);

    /// <summary>
    /// Résumer un historique narratif en un texte concis et déterministe.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="events">Les événements à résumer</param>
    /// <param name="targetLength">Longueur cible du résumé en caractères</param>
    /// <returns>Un résumé texte de l'historique, ou erreur si échec</returns>
    Task<Result<string>> SummarizeHistoryAsync(
        Id worldId,
        IReadOnlyList<object> events,
        int targetLength = 500);

    /// <summary>
    /// Obtenir l'état canonique du monde à une date donnée.
    /// Agrège tous les changements d'état jusqu'à cette date.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="asOf">La date pour laquelle obtenir l'état canonique</param>
    /// <returns>L'état canonique du monde à la date spécifiée</returns>
    Task<Result<CanonicalState>> GetCanonicalStateAsync(
        Id worldId,
        DateTime asOf);

    /// <summary>
    /// Valider la cohérence logique d'un ensemble de memorias.
    /// Détecte les contradictions et violations de logique.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="memoria">Les memorias à valider</param>
    /// <returns>Liste des violations de cohérence détectées (vide si cohérent)</returns>
    Task<Result<IReadOnlyList<CoherenceViolation>>> ValidateCoherenceAsync(
        Id worldId,
        IReadOnlyList<Memorandum> memoria);

    /// <summary>
    /// Asserter un fait spécifique au système de mémoire.
    /// Persiste le fait et valide qu'il n'entre pas en contradiction.
    /// </summary>
    /// <param name="worldId">L'ID du monde narratif</param>
    /// <param name="fact">Le fait à asserter</param>
    /// <returns>Unit en cas de succès, ou erreur si contradiction détectée</returns>
    Task<Result<Unit>> AssertFactAsync(
        Id worldId,
        Fact fact);
}
