using Narratum.Core;
using Narratum.State;
using Narratum.Memory;

namespace Narratum.Orchestration.Models;

/// <summary>
/// Contexte enrichi pour l'exécution du pipeline.
/// Contient toutes les informations nécessaires pour générer du contenu narratif.
/// </summary>
public sealed record PipelineContext
{
    /// <summary>
    /// Identifiant unique de ce contexte.
    /// </summary>
    public Id ContextId { get; }

    /// <summary>
    /// État actuel de l'histoire.
    /// </summary>
    public StoryState StoryState { get; }

    /// <summary>
    /// Intention narrative à réaliser.
    /// </summary>
    public NarrativeIntent Intent { get; }

    /// <summary>
    /// Memorandum le plus récent (contexte mémoire).
    /// </summary>
    public Memorandum? CurrentMemorandum { get; }

    /// <summary>
    /// État canonique actuel (source of truth).
    /// </summary>
    public CanonicalState? CanonicalState { get; }

    /// <summary>
    /// Résumés hiérarchiques disponibles.
    /// </summary>
    public IReadOnlyDictionary<MemoryLevel, string> Summaries { get; }

    /// <summary>
    /// Personnages actifs dans cette scène.
    /// </summary>
    public IReadOnlyList<Id> ActiveCharacterIds { get; init; }

    /// <summary>
    /// Lieu actuel de l'action.
    /// </summary>
    public Id? CurrentLocationId { get; init; }

    /// <summary>
    /// Métadonnées additionnelles.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Timestamp de création du contexte.
    /// </summary>
    public DateTime CreatedAt { get; }

    public PipelineContext(
        StoryState storyState,
        NarrativeIntent intent,
        Memorandum? currentMemorandum = null,
        CanonicalState? canonicalState = null,
        IReadOnlyDictionary<MemoryLevel, string>? summaries = null,
        IEnumerable<Id>? activeCharacterIds = null,
        Id? currentLocationId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ContextId = Id.New();
        StoryState = storyState ?? throw new ArgumentNullException(nameof(storyState));
        Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        CurrentMemorandum = currentMemorandum;
        CanonicalState = canonicalState;
        Summaries = summaries ?? new Dictionary<MemoryLevel, string>();
        ActiveCharacterIds = (activeCharacterIds?.ToList() ?? new List<Id>()).AsReadOnly();
        CurrentLocationId = currentLocationId;
        Metadata = metadata ?? new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Crée un contexte minimal pour les tests.
    /// </summary>
    public static PipelineContext CreateMinimal(StoryState storyState, NarrativeIntent intent)
        => new(storyState, intent);

    /// <summary>
    /// Ajoute des métadonnées au contexte.
    /// </summary>
    public PipelineContext WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Ajoute un personnage actif au contexte.
    /// </summary>
    public PipelineContext WithActiveCharacter(Id characterId)
    {
        var newCharacters = ActiveCharacterIds.ToList();
        if (!newCharacters.Contains(characterId))
            newCharacters.Add(characterId);
        return this with { ActiveCharacterIds = newCharacters.AsReadOnly() };
    }
}
