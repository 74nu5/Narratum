namespace Narratum.Memory.Services;

/// <summary>
/// Contexte pour l'extraction de faits à partir d'événements.
/// Fournit accès à l'état du monde et aux données de support.
/// </summary>
public sealed record EventExtractorContext(
    Guid WorldId,
    DateTime EventTimestamp,
    IReadOnlyDictionary<string, string> EntityNameMap,
    IReadOnlyDictionary<string, object>? AdditionalContext = null
)
{
    /// <summary>
    /// Obtient le nom lisible d'une entité par son ID.
    /// </summary>
    public string? GetEntityName(string entityId)
    {
        return EntityNameMap.TryGetValue(entityId, out var name) ? name : null;
    }

    /// <summary>
    /// Crée un contexte minimal pour les tests ou cas simples.
    /// </summary>
    public static EventExtractorContext CreateMinimal(Guid worldId)
    {
        return new EventExtractorContext(
            WorldId: worldId,
            EventTimestamp: DateTime.UtcNow,
            EntityNameMap: new Dictionary<string, string>(),
            AdditionalContext: null
        );
    }
}

/// <summary>
/// Interface pour extraire des faits à partir d'événements de domaine.
/// Chaque type d'événement a sa propre logique d'extraction.
/// </summary>
public interface IFactExtractor
{
    /// <summary>
    /// Extrait les faits d'un événement unique.
    /// Garantit le déterminisme: même événement = mêmes faits, toujours.
    /// </summary>
    IReadOnlyList<Fact> ExtractFromEvent(
        object domainEvent,
        EventExtractorContext context);

    /// <summary>
    /// Extrait les faits d'une collection d'événements.
    /// </summary>
    IReadOnlyList<Fact> ExtractFromEvents(
        IReadOnlyList<object> domainEvents,
        EventExtractorContext context);

    /// <summary>
    /// Obtient les types d'événements supportés.
    /// </summary>
    IReadOnlySet<Type> SupportedEventTypes { get; }

    /// <summary>
    /// Vérifie si ce extracteur peut traiter un type d'événement donné.
    /// </summary>
    bool CanExtract(Type eventType);
}
