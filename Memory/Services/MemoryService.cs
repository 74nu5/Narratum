using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Store;

namespace Narratum.Memory.Services;

/// <summary>
/// Implémentation de IMemoryService qui orchestre tous les composants de mémoire.
/// 
/// Dépendances:
/// - IMemoryRepository: Persistance des memorias
/// - IFactExtractor: Extraction des faits depuis les événements
/// - ISummaryGenerator: Génération des résumés hiérarchiques
/// - ICoherenceValidator: Validation de la cohérence logique
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repository;
    private readonly IFactExtractor _factExtractor;
    private readonly ISummaryGenerator _summaryGenerator;
    private readonly ICoherenceValidator _coherenceValidator;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IMemoryRepository repository,
        IFactExtractor factExtractor,
        ISummaryGenerator summaryGenerator,
        ICoherenceValidator coherenceValidator,
        ILogger<MemoryService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _factExtractor = factExtractor ?? throw new ArgumentNullException(nameof(factExtractor));
        _summaryGenerator = summaryGenerator ?? throw new ArgumentNullException(nameof(summaryGenerator));
        _coherenceValidator = coherenceValidator ?? throw new ArgumentNullException(nameof(coherenceValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Mémoriser un événement unique.
    /// </summary>
    public async Task<Result<Memorandum>> RememberEventAsync(
        Id worldId,
        object domainEvent,
        IReadOnlyDictionary<string, object>? context = null)
    {
        try
        {
            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Remembering event {EventType} for world {WorldId}",
                domainEvent.GetType().Name,
                worldGuid);

            // Créer le contexte d'extraction
            var extractContext = new EventExtractorContext(
                WorldId: worldGuid,
                EventTimestamp: DateTime.UtcNow,
                EntityNameMap: new Dictionary<string, string>(),
                AdditionalContext: context);

            // Extraire les faits
            var facts = _factExtractor.ExtractFromEvent(domainEvent, extractContext);
            _logger.LogDebug("Extracted {FactCount} facts from event", facts.Count);

            // Créer le memorandum
            var memorandum = Memorandum.CreateEmpty(worldGuid, $"Event: {domainEvent.GetType().Name}");
            
            // Ajouter les faits au niveau Event
            var canonicalState = memorandum.CanonicalStates[MemoryLevel.Event];
            foreach (var fact in facts)
            {
                canonicalState = canonicalState.AddFact(fact);
            }

            var updatedCanonicalStates = new Dictionary<MemoryLevel, CanonicalState>(memorandum.CanonicalStates)
            {
                { MemoryLevel.Event, canonicalState }
            };

            var updatedMemoranda = memorandum with
            {
                CanonicalStates = updatedCanonicalStates,
                LastUpdated = DateTime.UtcNow
            };

            // Persister
            await _repository.SaveAsync(updatedMemoranda);
            _logger.LogInformation("Memorandum {MemoriumId} created successfully", updatedMemoranda.Id);

            return Result<Memorandum>.Ok(updatedMemoranda);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remember event for world {WorldId}", worldId.Value);
            return Result<Memorandum>.Fail($"Failed to remember event: {ex.Message}");
        }
    }

    /// <summary>
    /// Mémoriser un groupe d'événements comme un chapitre.
    /// </summary>
    public async Task<Result<Memorandum>> RememberChapterAsync(
        Id worldId,
        IReadOnlyList<object> events,
        IReadOnlyDictionary<string, object>? context = null)
    {
        try
        {
            if (events == null || events.Count == 0)
            {
                return Result<Memorandum>.Fail("Cannot remember empty chapter");
            }

            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Remembering chapter with {EventCount} events for world {WorldId}",
                events.Count,
                worldGuid);

            var startTime = DateTime.UtcNow;

            // Créer le contexte d'extraction
            var extractContext = EventExtractorContext.CreateMinimal(worldGuid);

            // Extraire tous les faits
            var allFacts = _factExtractor.ExtractFromEvents(events, extractContext);
            _logger.LogDebug("Extracted {FactCount} facts from {EventCount} events", allFacts.Count, events.Count);

            // Générer résumé de chapitre
            var summary = _summaryGenerator.SummarizeChapter(allFacts);

            // Créer memorandum
            var memorandum = Memorandum.CreateEmpty(worldGuid, "Chapter Summary", summary);
            
            // Ajouter les faits au niveau Chapter
            var canonicalState = memorandum.CanonicalStates[MemoryLevel.Chapter];
            foreach (var fact in allFacts)
            {
                canonicalState = canonicalState.AddFact(fact);
            }

            var updatedCanonicalStates = new Dictionary<MemoryLevel, CanonicalState>(memorandum.CanonicalStates)
            {
                { MemoryLevel.Chapter, canonicalState }
            };

            var updatedMemoranda = memorandum with
            {
                CanonicalStates = updatedCanonicalStates,
                LastUpdated = DateTime.UtcNow
            };

            // Persister
            await _repository.SaveAsync(updatedMemoranda);
            _logger.LogInformation(
                "Chapter memorandum {MemoriumId} created with {EventCount} events",
                updatedMemoranda.Id,
                events.Count);

            return Result<Memorandum>.Ok(updatedMemoranda);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remember chapter for world {WorldId}", worldId.Value);
            return Result<Memorandum>.Fail($"Failed to remember chapter: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrouver un memorandum par son ID.
    /// </summary>
    public async Task<Result<Memorandum?>> RetrieveMemoriumAsync(Id memorandumId)
    {
        try
        {
            _logger.LogInformation("Retrieving memorandum {MemoriumId}", memorandumId.Value);

            var memorandum = await _repository.GetByIdAsync(memorandumId.Value);

            if (memorandum == null)
            {
                _logger.LogWarning("Memorandum {MemoriumId} not found", memorandumId.Value);
                return Result<Memorandum?>.Ok(null);
            }

            _logger.LogInformation("Memorandum {MemoriumId} retrieved successfully", memorandumId.Value);
            return Result<Memorandum?>.Ok(memorandum);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memorandum {MemoriumId}", memorandumId.Value);
            return Result<Memorandum?>.Fail($"Failed to retrieve memorandum: {ex.Message}");
        }
    }

    /// <summary>
    /// Trouver tous les memorias mentionnant une entité spécifique.
    /// </summary>
    public async Task<Result<IReadOnlyList<Memorandum>>> FindMemoriaByEntityAsync(
        Id worldId,
        string entityName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return Result<IReadOnlyList<Memorandum>>.Fail("Entity name cannot be empty");
            }

            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Finding memoria for entity {EntityName} in world {WorldGuid}",
                entityName,
                worldGuid);

            var memoria = await _repository.GetByWorldAsync(worldGuid);
            var filtered = new List<Memorandum>();

            foreach (var mem in memoria)
            {
                // Vérifier si une entité est mentionnée dans les faits canoniques
                var hasEntity = false;
                foreach (var canonicalState in mem.CanonicalStates.Values)
                {
                    if (canonicalState.Facts.Any(f => 
                        f.EntityReferences.Contains(entityName, StringComparer.OrdinalIgnoreCase)))
                    {
                        hasEntity = true;
                        break;
                    }
                }

                if (hasEntity)
                {
                    filtered.Add(mem);
                }
            }

            _logger.LogInformation(
                "Found {MemoriaCount} memoria mentioning entity {EntityName}",
                filtered.Count,
                entityName);

            return Result<IReadOnlyList<Memorandum>>.Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find memoria for entity {EntityName}", entityName);
            return Result<IReadOnlyList<Memorandum>>.Fail($"Failed to find memoria: {ex.Message}");
        }
    }

    /// <summary>
    /// Résumer un historique narratif de façon déterministe.
    /// </summary>
    public async Task<Result<string>> SummarizeHistoryAsync(
        Id worldId,
        IReadOnlyList<object> events,
        int targetLength = 500)
    {
        try
        {
            if (events == null || events.Count == 0)
            {
                return Result<string>.Fail("Cannot summarize empty history");
            }

            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Summarizing history of {EventCount} events for world {WorldGuid}",
                events.Count,
                worldGuid);

            var startTime = DateTime.UtcNow;

            // Créer le contexte d'extraction
            var extractContext = EventExtractorContext.CreateMinimal(worldGuid);

            // Extraire tous les faits
            var allFacts = _factExtractor.ExtractFromEvents(events, extractContext);
            _logger.LogDebug("Extracted {FactCount} facts", allFacts.Count);

            // Résumer
            var summary = _summaryGenerator.SummarizeChapter(allFacts);
            if (summary.Length > targetLength)
            {
                summary = summary[..targetLength] + "…";
            }

            _logger.LogInformation(
                "History summarized in {Duration}ms to {Length} characters",
                (DateTime.UtcNow - startTime).TotalMilliseconds,
                summary.Length);

            return Result<string>.Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize history for world {WorldGuid}", worldId.Value);
            return Result<string>.Fail($"Failed to summarize history: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtenir l'état canonique du monde à une date donnée.
    /// </summary>
    public async Task<Result<CanonicalState>> GetCanonicalStateAsync(
        Id worldId,
        DateTime asOf)
    {
        try
        {
            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Computing canonical state for world {WorldGuid} as of {Date}",
                worldGuid,
                asOf);

            var memoria = await _repository.GetByWorldAsync(worldGuid);
            var relevantMemoria = memoria
                .Where(m => m.CreatedAt <= asOf)
                .OrderBy(m => m.CreatedAt)
                .ToList();

            // Fusionner tous les états canoniques
            var mergedState = CanonicalState.CreateEmpty(worldGuid, MemoryLevel.World);
            
            foreach (var mem in relevantMemoria)
            {
                var worldState = mem.CanonicalStates[MemoryLevel.World];
                foreach (var fact in worldState.Facts)
                {
                    mergedState = mergedState.AddFact(fact);
                }
            }

            _logger.LogInformation(
                "Canonical state computed with {FactCount} facts",
                mergedState.Facts.Count);

            return Result<CanonicalState>.Ok(mergedState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get canonical state for world {WorldGuid}", worldId.Value);
            return Result<CanonicalState>.Fail($"Failed to get canonical state: {ex.Message}");
        }
    }

    /// <summary>
    /// Valider la cohérence logique d'un ensemble de memorias.
    /// </summary>
    public async Task<Result<IReadOnlyList<CoherenceViolation>>> ValidateCoherenceAsync(
        Id worldId,
        IReadOnlyList<Memorandum> memoria)
    {
        try
        {
            if (memoria == null || memoria.Count == 0)
            {
                return Result<IReadOnlyList<CoherenceViolation>>.Ok(
                    Array.Empty<CoherenceViolation>());
            }

            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Validating coherence of {MemoriaCount} memoria for world {WorldGuid}",
                memoria.Count,
                worldGuid);

            // Fusionner tous les états canoniques pour vérification
            var allFacts = new HashSet<Fact>();
            foreach (var mem in memoria)
            {
                foreach (var canonicalState in mem.CanonicalStates.Values)
                {
                    allFacts.UnionWith(canonicalState.Facts);
                }
            }

            // Valider avec le coherence validator
            var violations = _coherenceValidator.ValidateFacts(allFacts.ToList());

            _logger.LogInformation(
                "Coherence validation complete. Found {ViolationCount} violations",
                violations.Count);

            return Result<IReadOnlyList<CoherenceViolation>>.Ok(violations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate coherence for world {WorldGuid}", worldId.Value);
            return Result<IReadOnlyList<CoherenceViolation>>.Fail($"Failed to validate coherence: {ex.Message}");
        }
    }

    /// <summary>
    /// Asserter un fait spécifique au système.
    /// </summary>
    public async Task<Result<Unit>> AssertFactAsync(
        Id worldId,
        Fact fact)
    {
        try
        {
            if (fact == null)
            {
                return Result<Unit>.Fail("Fact cannot be null");
            }

            var worldGuid = worldId.Value;
            _logger.LogInformation(
                "Asserting fact for world {WorldGuid}: {FactContent}",
                worldGuid,
                fact.Content);

            // TODO: Implémenter vérification de contradiction avec faits existants

            _logger.LogInformation("Fact asserted successfully");
            return Result<Unit>.Ok(default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assert fact for world {WorldGuid}", worldId.Value);
            return Result<Unit>.Fail($"Failed to assert fact: {ex.Message}");
        }
    }
}
