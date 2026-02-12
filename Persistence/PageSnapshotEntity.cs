namespace Narratum.Persistence;

/// <summary>
/// Entité représentant un snapshot de page dans la base de données.
/// Chaque page générée crée un snapshot complet de l'état à ce point.
/// </summary>
public record PageSnapshotEntity
{
    /// <summary>
    /// Identifiant unique du snapshot.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Nom du slot de sauvegarde (histoire parente).
    /// </summary>
    public required string SlotName { get; init; }

    /// <summary>
    /// Index de la page (0 = état initial, 1 = première page générée, etc.).
    /// </summary>
    public required int PageIndex { get; init; }

    /// <summary>
    /// Timestamp de génération de cette page.
    /// </summary>
    public required DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Texte narratif généré pour cette page (null pour page 0 = état initial).
    /// </summary>
    public string? NarrativeText { get; init; }

    /// <summary>
    /// StoryState complet sérialisé en JSON à ce point de l'histoire.
    /// </summary>
    public required string SerializedState { get; init; }

    /// <summary>
    /// Description de l'intention de l'utilisateur (ce qui a été demandé).
    /// </summary>
    public string? IntentDescription { get; init; }

    /// <summary>
    /// Modèle LLM utilisé pour générer cette page.
    /// </summary>
    public string? ModelUsed { get; init; }

    /// <summary>
    /// Genre/Style narratif de l'histoire.
    /// </summary>
    public string? GenreStyle { get; init; }

    /// <summary>
    /// FullPipelineResult sérialisé (mode expert).
    /// </summary>
    public string? SerializedPipelineResult { get; init; }

    /// <summary>
    /// Prompts envoyés aux agents (mode expert).
    /// </summary>
    public string? PromptsSent { get; init; }

    /// <summary>
    /// Output brut LLM avant validation (mode expert).
    /// </summary>
    public string? RawLlmOutput { get; init; }
}
