using Narratum.Core;
using Narratum.Domain;

namespace Narratum.Memory.Services;

/// <summary>
/// Implémentation du service d'extraction de faits.
/// Orchestre les extracteurs spécialisés par type d'événement.
/// Garantit l'extraction complète et déterministe de tous les faits importants.
/// </summary>
public class FactExtractorService : IFactExtractor
{
    private readonly IReadOnlyDictionary<Type, IEventFactExtractor> _extractors;
    private readonly IReadOnlySet<Type> _supportedEventTypes;

    public IReadOnlySet<Type> SupportedEventTypes => _supportedEventTypes;

    public FactExtractorService(params IEventFactExtractor[] extractors)
    {
        if (extractors == null || extractors.Length == 0)
            throw new ArgumentException("Au moins un extracteur est requis", nameof(extractors));

        var extractorDict = new Dictionary<Type, IEventFactExtractor>();
        var supportedTypes = new HashSet<Type>();

        foreach (var extractor in extractors)
        {
            foreach (var eventType in extractor.SupportedEventTypes)
            {
                extractorDict[eventType] = extractor;
                supportedTypes.Add(eventType);
            }
        }

        _extractors = extractorDict;
        _supportedEventTypes = supportedTypes;
    }

    public bool CanExtract(Type eventType)
    {
        return _extractors.ContainsKey(eventType);
    }

    public IReadOnlyList<Fact> ExtractFromEvent(
        object domainEvent,
        EventExtractorContext context)
    {
        if (domainEvent == null)
            throw new ArgumentNullException(nameof(domainEvent));

        var eventType = domainEvent.GetType();

        if (!_extractors.TryGetValue(eventType, out var extractor))
            throw new NotSupportedException($"Type d'événement non supporté: {eventType.Name}");

        var facts = extractor.Extract(domainEvent, context);
        
        // Assurer le déterminisme: trier les faits par contenu
        return facts
            .OrderBy(f => f.Content)
            .ThenBy(f => f.Id.ToString())
            .ToList();
    }

    public IReadOnlyList<Fact> ExtractFromEvents(
        IReadOnlyList<object> domainEvents,
        EventExtractorContext context)
    {
        if (domainEvents == null || domainEvents.Count == 0)
            return new List<Fact>();

        var allFacts = new List<Fact>();

        foreach (var evt in domainEvents)
        {
            var facts = ExtractFromEvent(evt, context);
            allFacts.AddRange(facts);
        }

        // Déduplquer les faits identiques
        var uniqueFacts = allFacts
            .GroupBy(f => f.Content)
            .Select(g => g.First())
            .OrderBy(f => f.Content)
            .ToList();

        return uniqueFacts;
    }
}

/// <summary>
/// Interface pour les extracteurs spécialisés par type d'événement.
/// </summary>
public interface IEventFactExtractor
{
    /// <summary>
    /// Types d'événements que cet extracteur peut traiter.
    /// </summary>
    IReadOnlySet<Type> SupportedEventTypes { get; }

    /// <summary>
    /// Extrait les faits d'un événement.
    /// </summary>
    IReadOnlyList<Fact> Extract(object domainEvent, EventExtractorContext context);
}

/// <summary>
/// Extracteur pour les événements CharacterDeathEvent.
/// Produit un fait indiquant qu'un personnage est décédé.
/// </summary>
public class CharacterDeathEventExtractor : IEventFactExtractor
{
    public IReadOnlySet<Type> SupportedEventTypes { get; } = new HashSet<Type>
    {
        typeof(CharacterDeathEvent)
    };

    public IReadOnlyList<Fact> Extract(object domainEvent, EventExtractorContext context)
    {
        if (domainEvent is not CharacterDeathEvent deathEvent)
            throw new ArgumentException("Événement incorrect", nameof(domainEvent));

        var facts = new List<Fact>();

        // Extraire le nom du personnage
        var characterId = deathEvent.ActorIds[0].Value.ToString();
        var characterName = context.GetEntityName(characterId) 
            ?? $"Character_{characterId}";

        // Construire le contenu du fait
        var content = deathEvent.GetCause() != null
            ? $"{characterName} died ({deathEvent.GetCause()})"
            : $"{characterName} died";

        facts.Add(Fact.Create(
            content: content,
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { characterName },
            timeContext: $"At {deathEvent.Timestamp:g}",
            confidence: 1.0,  // Les morts sont certaines
            source: deathEvent.Id.ToString()
        ));

        return facts;
    }
}

/// <summary>
/// Extracteur pour les événements CharacterMovedEvent.
/// Produit un fait indiquant un déplacement de personnage.
/// </summary>
public class CharacterMovedEventExtractor : IEventFactExtractor
{
    public IReadOnlySet<Type> SupportedEventTypes { get; } = new HashSet<Type>
    {
        typeof(CharacterMovedEvent)
    };

    public IReadOnlyList<Fact> Extract(object domainEvent, EventExtractorContext context)
    {
        if (domainEvent is not CharacterMovedEvent movedEvent)
            throw new ArgumentException("Événement incorrect", nameof(domainEvent));

        var facts = new List<Fact>();

        var characterId = movedEvent.ActorIds[0].Value.ToString();
        var characterName = context.GetEntityName(characterId)
            ?? $"Character_{characterId}";

        var fromLocationId = movedEvent.GetFromLocation().Value.ToString();
        var fromLocationName = context.GetEntityName(fromLocationId)
            ?? $"Location_{fromLocationId}";

        var toLocationId = movedEvent.LocationId?.Value.ToString() ?? "";
        var toLocationName = context.GetEntityName(toLocationId)
            ?? $"Location_{toLocationId}";

        var content = $"{characterName} moved from {fromLocationName} to {toLocationName}";

        facts.Add(Fact.Create(
            content: content,
            factType: FactType.Event,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { characterName, fromLocationName, toLocationName },
            timeContext: $"At {movedEvent.Timestamp:g}",
            confidence: 1.0,
            source: movedEvent.Id.ToString()
        ));

        // Ajouter un fait de changement d'état du personnage
        facts.Add(Fact.Create(
            content: $"{characterName} is at {toLocationName}",
            factType: FactType.LocationState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { characterName, toLocationName },
            timeContext: $"As of {movedEvent.Timestamp:g}",
            confidence: 1.0,
            source: movedEvent.Id.ToString()
        ));

        return facts;
    }
}

/// <summary>
/// Extracteur pour les événements CharacterEncounterEvent.
/// Produit des faits indiquant une rencontre entre personnages.
/// </summary>
public class CharacterEncounterEventExtractor : IEventFactExtractor
{
    public IReadOnlySet<Type> SupportedEventTypes { get; } = new HashSet<Type>
    {
        typeof(CharacterEncounterEvent)
    };

    public IReadOnlyList<Fact> Extract(object domainEvent, EventExtractorContext context)
    {
        if (domainEvent is not CharacterEncounterEvent encounterEvent)
            throw new ArgumentException("Événement incorrect", nameof(domainEvent));

        var facts = new List<Fact>();

        var character1Id = encounterEvent.ActorIds[0].Value.ToString();
        var character1Name = context.GetEntityName(character1Id)
            ?? $"Character_{character1Id}";

        var character2Id = encounterEvent.ActorIds[1].Value.ToString();
        var character2Name = context.GetEntityName(character2Id)
            ?? $"Character_{character2Id}";

        var locationId = encounterEvent.LocationId?.Value.ToString();
        var locationName = encounterEvent.LocationId != null
            ? (context.GetEntityName(locationId ?? "") ?? $"Location_{locationId}")
            : "unknown location";

        var content = $"{character1Name} and {character2Name} met at {locationName}";

        facts.Add(Fact.Create(
            content: content,
            factType: FactType.Event,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { character1Name, character2Name, locationName },
            timeContext: $"At {encounterEvent.Timestamp:g}",
            confidence: 1.0,
            source: encounterEvent.Id.ToString()
        ));

        // Ajouter des faits de relation (rencontre implique interaction)
        facts.Add(Fact.Create(
            content: $"{character1Name} knows {character2Name}",
            factType: FactType.Relationship,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { character1Name, character2Name },
            timeContext: $"Since {encounterEvent.Timestamp:g}",
            confidence: 0.8,  // Moins certain que la mort
            source: encounterEvent.Id.ToString()
        ));

        return facts;
    }
}
