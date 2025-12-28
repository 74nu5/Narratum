namespace Narratum.Memory;

/// <summary>
/// Record immutable représentant un Fait extrait du texte narratif.
/// Exemples: "Aric is dead", "Tower is destroyed", "Aric trusts Lyra"
/// </summary>
/// <param name="Id">Identifiant unique du fait</param>
/// <param name="Content">Contenu textuel du fait</param>
/// <param name="FactType">Type du fait (CharacterState, LocationState, etc.)</param>
/// <param name="MemoryLevel">Niveau hiérarchique (Event, Chapter, Arc, World)</param>
/// <param name="EntityReferences">Ensemble des entités mentionnées (personnages, lieux, objets)</param>
/// <param name="TimeContext">Contexte temporel optionnel</param>
/// <param name="Confidence">Score de confiance (0-1) de l'extraction</param>
/// <param name="Source">Source du fait (numéro d'événement, chapitre, etc.)</param>
/// <param name="CreatedAt">Timestamp de création</param>
public sealed record Fact(
    Guid Id,
    string Content,
    FactType FactType,
    MemoryLevel MemoryLevel,
    IReadOnlySet<string> EntityReferences,
    string? TimeContext = null,
    double Confidence = 1.0,
    string? Source = null,
    DateTime? CreatedAt = null
)
{
    /// <summary>
    /// Crée un nouveau Fact avec les paramètres par défaut.
    /// </summary>
    public static Fact Create(
        string content,
        FactType factType,
        MemoryLevel memoryLevel,
        IEnumerable<string> entityReferences,
        string? timeContext = null,
        double confidence = 1.0,
        string? source = null)
    {
        return new Fact(
            Id: Guid.NewGuid(),
            Content: content,
            FactType: factType,
            MemoryLevel: memoryLevel,
            EntityReferences: entityReferences.ToHashSet(),
            TimeContext: timeContext,
            Confidence: confidence,
            Source: source,
            CreatedAt: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Valide que le Fact respecte les contraintes de base.
    /// </summary>
    public bool Validate()
    {
        // Content ne doit pas être vide
        if (string.IsNullOrWhiteSpace(Content))
            return false;

        // Confidence doit être entre 0 et 1
        if (Confidence < 0 || Confidence > 1)
            return false;

        // EntityReferences ne doit pas être vide pour les états d'entité
        if (FactType is FactType.CharacterState or FactType.LocationState or FactType.Relationship or FactType.Knowledge)
        {
            if (EntityReferences.Count == 0)
                return false;
        }

        return true;
    }
}
