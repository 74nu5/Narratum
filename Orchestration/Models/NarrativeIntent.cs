using Narratum.Core;

namespace Narratum.Orchestration.Models;

/// <summary>
/// Type d'intention narrative demandée par l'utilisateur ou le système.
/// </summary>
public enum IntentType
{
    /// <summary>
    /// Continuer le récit naturellement.
    /// </summary>
    ContinueNarrative,

    /// <summary>
    /// Introduire un nouvel événement spécifique.
    /// </summary>
    IntroduceEvent,

    /// <summary>
    /// Générer un dialogue entre personnages.
    /// </summary>
    GenerateDialogue,

    /// <summary>
    /// Décrire une scène ou un lieu.
    /// </summary>
    DescribeScene,

    /// <summary>
    /// Résumer une période narrative.
    /// </summary>
    Summarize,

    /// <summary>
    /// Créer un point de tension dramatique.
    /// </summary>
    CreateTension,

    /// <summary>
    /// Résoudre un conflit ou une intrigue.
    /// </summary>
    ResolveConflict
}

/// <summary>
/// Représente une intention narrative - ce que l'utilisateur ou le système veut accomplir.
/// Immutable et validée à la construction.
/// </summary>
public sealed record NarrativeIntent
{
    /// <summary>
    /// Identifiant unique de l'intention.
    /// </summary>
    public Id Id { get; }

    /// <summary>
    /// Type d'intention narrative.
    /// </summary>
    public IntentType Type { get; }

    /// <summary>
    /// Description textuelle optionnelle de l'intention.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Identifiants des personnages ciblés par cette intention.
    /// </summary>
    public IReadOnlyList<Id> TargetCharacterIds { get; }

    /// <summary>
    /// Identifiant du lieu ciblé par cette intention.
    /// </summary>
    public Id? TargetLocationId { get; }

    /// <summary>
    /// Paramètres additionnels pour l'intention.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }

    /// <summary>
    /// Date de création de l'intention.
    /// </summary>
    public DateTime CreatedAt { get; }

    public NarrativeIntent(
        IntentType type,
        string? description = null,
        IEnumerable<Id>? targetCharacterIds = null,
        Id? targetLocationId = null,
        IReadOnlyDictionary<string, object>? parameters = null)
    {
        Id = Id.New();
        Type = type;
        Description = description;
        TargetCharacterIds = (targetCharacterIds?.ToList() ?? new List<Id>()).AsReadOnly();
        TargetLocationId = targetLocationId;
        Parameters = parameters ?? new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Crée une intention simple de continuation narrative.
    /// </summary>
    public static NarrativeIntent Continue(string? description = null)
        => new(IntentType.ContinueNarrative, description);

    /// <summary>
    /// Crée une intention de dialogue entre personnages.
    /// </summary>
    public static NarrativeIntent Dialogue(IEnumerable<Id> characterIds, Id locationId)
        => new(IntentType.GenerateDialogue, targetCharacterIds: characterIds, targetLocationId: locationId);

    /// <summary>
    /// Crée une intention de description de scène.
    /// </summary>
    public static NarrativeIntent DescribeLocation(Id locationId, string? description = null)
        => new(IntentType.DescribeScene, description, targetLocationId: locationId);
}
