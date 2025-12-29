# Phase 2 — Mémoire & Cohérence (Design et Architecture)

**Status**: 📋 DESIGN DOCUMENT  
**Phase**: Phase 2 — Memory & Coherence System  
**Dependencies**: Phase 1 (✅ COMPLETE)  

---

## 📋 Table des Matières

1. [Objectif et Contexte](#objectif-et-contexte)
2. [Principes Directeurs](#principes-directeurs)
3. [Architecture Globale](#architecture-globale)
4. [Modules et Composants](#modules-et-composants)
5. [Algorithmes et Stratégies](#algorithmes-et-stratégies)
6. [APIs Publiques](#apis-publiques)
7. [Plan de Développement](#plan-de-développement)
8. [Tests et Validation](#tests-et-validation)
9. [Interdictions Volontaires](#interdictions-volontaires)

---

## Objectif et Contexte

### Vision Phase 2

Phase 2 construit un système de **mémoire et cohérence** qui permet au moteur narratif de:

- 📚 **Mémoriser** les événements significatifs
- 🔍 **Retrouver** les informations pertinentes rapidement
- 🧠 **Résumer** de longs historiques narratifs
- ✓ **Valider** la cohérence logique des états
- ⚠️ **Détecter** les contradictions avant qu'elles ne se propagent

### Pourquoi avant l'IA?

> **La continuité logique doit fonctionner avant toute créativité.**

Si le système ne peut pas :
- Résumer une histoire
- Retrouver un personnage
- Détecter une incohérence

Alors aucun LLM ne pourra le faire.

### Transition depuis Phase 1

Phase 1 fournit:
- ✅ Entités de domaine immuables (Character, Location, Event, etc.)
- ✅ États narratifs (WorldState, CharacterState, StoryState)
- ✅ Snapshots complets et validables
- ✅ Validation de règles (RuleEngine)
- ✅ Persistance déterministe

Phase 2 **ajoute**:
- 📚 Abstraction de mémoire
- 🔗 Indices et relations
- 📊 Résumés hiérarchiques
- 🧪 Validation de cohérence

---

## Principes Directeurs

### 1️⃣ Déterminisme Absolu

```csharp
// Résumé du même historique = même résumé
var summary1 = memoryService.SummarizeHistory(history);
var summary2 = memoryService.SummarizeHistory(history);
Assert.Equal(summary1, summary2); // TOUJOURS TRUE
```

**Jamais** de randomisation, de température ou de non-déterminisme.

### 2️⃣ Sans LLM (Phase 2)

Tout fonctionne avec **logique pure**:
- Extraction de faits
- Agrégation
- Validation logique
- Détection de contradictions

Le LLM n'intervient que **optionnellement** en Phase 3+.

### 3️⃣ Hiérarchie Temporelle

Les résumés s'organisent par niveau:
- **Niveau 0**: Events (granularité maximale)
- **Niveau 1**: Chapters (scènes, actes)
- **Niveau 2**: Arcs (chapitres groupés)
- **Niveau 3**: World (histoire complète)

### 4️⃣ Immutabilité Structurelle

Les résumés et mémoriques sont **immuables** (records).

```csharp
public record Memorandum(
    Id Id,
    DateTime CreatedAt,
    string FactualSummary,
    IReadOnlySet<string> ImportantEntities,
    IReadOnlySet<string> StateChanges
);
```

### 5️⃣ Transparence Totale

Chaque mémoire contient:
- Les **sources** (quels events?)
- Les **timestamps**
- Les **hashes** d'intégrité
- Les **métadonnées** de calcul

```csharp
public record MemoryMetadata(
    DateTime ComputedAt,
    IReadOnlyList<Id> SourceEventIds,
    string ComputationHash,
    TimeSpan ComputationTime
);
```

---

## Architecture Globale

### Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────┐
│  Narratum.Memory (nouveau module)                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  IMemoryService (interface publique)                │  │
│  │  - Mémoriser des événements                         │  │
│  │  - Résumer un historique                           │  │
│  │  - Retrouver des faits                             │  │
│  │  - Valider la cohérence                            │  │
│  └──────────────────────────────────────────────────────┘  │
│                      ↓                                      │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Memory Layers (4 couches de traitement)           │  │
│  │  - FactExtraction          (niveau 0)              │  │
│  │  - ChapterSummarization    (niveau 1)              │  │
│  │  - ArcCompression          (niveau 2)              │  │
│  │  - WorldState Canonization (niveau 3)              │  │
│  └──────────────────────────────────────────────────────┘  │
│                      ↓                                      │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Coherence Engine (validation logique)              │  │
│  │  - ContradictionDetector                           │  │
│  │  - FactValidator                                   │  │
│  │  - StateConsistencyChecker                         │  │
│  └──────────────────────────────────────────────────────┘  │
│                      ↓                                      │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Memory Store (persistance)                        │  │
│  │  - IMemoryRepository<T>                            │  │
│  │  - SQLite storage                                  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                         ↓
            Narratum.Persistence (Phase 1)
            Narratum.State (Phase 1)
            Narratum.Domain (Phase 1)
            Narratum.Core (Phase 1)
```

### Intégration dans la solution

```
Narratum/
├── Core/                          (Phase 1 ✅)
├── Domain/                        (Phase 1 ✅)
├── State/                         (Phase 1 ✅)
├── Persistence/                   (Phase 1 ✅)
├── Rules/                         (Phase 1 ✅)
├── Simulation/                    (Phase 1 ✅)
├── Memory/                        (Phase 2 🆕)
│   ├── Services/
│   ├── Layers/
│   ├── Coherence/
│   ├── Store/
│   └── Models/
├── Tests/                         (Phase 1 ✅ + Phase 2 🆕)
└── Playground/                    (Phase 1 ✅)
```

---

## Modules et Composants

### 1. Narratum.Memory.Models

**Responsabilité**: Définir les types immuables pour la mémoire.

#### Memorandum (Record)

```csharp
public record Memorandum(
    Id Id,
    Id StoryWorldId,
    DateTime CreatedAt,
    
    // Contenu mémorisé
    string FactualSummary,
    
    // Entités et changements
    IReadOnlySet<string> ImportantEntities,
    IReadOnlySet<string> ImportantLocations,
    IReadOnlySet<string> StateChanges,
    
    // Traçabilité
    MemoryLevel Level,
    IReadOnlyList<Id> SourceEventIds,
    
    // Intégrité
    string ContentHash,
    MemoryMetadata Metadata
);

public enum MemoryLevel
{
    Event = 0,      // Un seul événement
    Chapter = 1,    // Groupe d'événements
    Arc = 2,        // Groupe de chapitres
    World = 3       // Histoire complète
}

public record MemoryMetadata(
    DateTime ComputedAt,
    TimeSpan ComputationTime,
    string ComputationHash,
    int SourceEventCount,
    int SummarizedToLength
);
```

#### Fact (Record)

```csharp
public record Fact(
    Id Id,
    string Content,
    DateTime FirstMentionedAt,
    DateTime LastMentionedAt,
    int OccurrenceCount,
    IReadOnlySet<string> RelatedEntities,
    FactType Type,
    bool IsCanonical
);

public enum FactType
{
    CharacterState,      // "Aric is dead"
    LocationState,       // "Tower is destroyed"
    Relationship,        // "Aric trusts Lyra"
    Knowledge,          // "Crystal has power"
    Event,              // "Combat occurred"
    Contradiction       // "Aric is both alive and dead"
}
```

#### CoherenceViolation (Record)

```csharp
public record CoherenceViolation(
    Id Id,
    string Description,
    CoherenceViolationType Type,
    Fact ConflictingFact1,
    Fact ConflictingFact2,
    DateTime DetectedAt,
    CoherenceSeverity Severity
);

public enum CoherenceViolationType
{
    StatementContradiction,    // "X is true" vs "X is false"
    SequenceViolation,         // Timeline impossible
    EntityInconsistency,       // Character state mismatch
    LocationInconsistency      // Location state mismatch
}

public enum CoherenceSeverity
{
    Info,      // Non problématique
    Warning,   // Potentiellement problématique
    Error      // Brise la cohérence logique
}
```

#### CanonicalState (Record)

```csharp
public record CanonicalState(
    Id WorldId,
    DateTime AsOf,
    IReadOnlyDictionary<Id, CharacterCanonical> Characters,
    IReadOnlyDictionary<Id, LocationCanonical> Locations,
    IReadOnlySet<string> CommonKnowledge,
    IReadOnlySet<string> MajorEvents,
    string StateHash
);

public record CharacterCanonical(
    Id Id,
    string Name,
    VitalStatus Status,
    Id? LastKnownLocation,
    IReadOnlySet<string> KnownFacts,
    DateTime LastStatusChange
);

public record LocationCanonical(
    Id Id,
    string Name,
    string State,  // "Safe" / "Dangerous" / "Destroyed"
    DateTime LastStateChange
);
```

---

### 2. Narratum.Memory.Services

**Responsabilité**: Orchestrer les opérations mémoire.

#### IMemoryService (Interface)

```csharp
public interface IMemoryService
{
    // Mémorisation
    Task<Result<Memorandum>> RememberEventAsync(
        Id worldId,
        Event domainEvent,
        StoryState context);

    Task<Result<Memorandum>> RememberChapterAsync(
        Id worldId,
        IReadOnlyList<Event> events,
        StoryState finalState);

    // Récupération
    Task<Result<Memorandum>> RetrieveMemoriumAsync(
        Id memorandumId);

    Task<Result<IReadOnlyList<Memorandum>>> FindMemoriaByEntityAsync(
        Id worldId,
        string entityName);

    // Résumés
    Task<Result<string>> SummarizeHistoryAsync(
        Id worldId,
        IReadOnlyList<Event> events,
        int targetLength = 500);

    Task<Result<CanonicalState>> GetCanonicalStateAsync(
        Id worldId,
        DateTime asOf);

    // Cohérence
    Task<Result<IReadOnlyList<CoherenceViolation>>> 
        ValidateCoherenceAsync(
            Id worldId,
            IReadOnlyList<Memorandum> memoria);

    Task<Result<Unit>> AssertFactAsync(
        Id worldId,
        Fact fact);
}
```

#### MemoryService (Implémentation)

```csharp
public class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repository;
    private readonly IFactExtractor _factExtractor;
    private readonly ISummaryGenerator _summaryGenerator;
    private readonly ICoherenceValidator _coherenceValidator;
    private readonly IMemoryStore _memoryStore;

    public MemoryService(
        IMemoryRepository repository,
        IFactExtractor factExtractor,
        ISummaryGenerator summaryGenerator,
        ICoherenceValidator coherenceValidator,
        IMemoryStore memoryStore)
    {
        _repository = repository;
        _factExtractor = factExtractor;
        _summaryGenerator = summaryGenerator;
        _coherenceValidator = coherenceValidator;
        _memoryStore = memoryStore;
    }

    public async Task<Result<Memorandum>> RememberEventAsync(
        Id worldId,
        Event domainEvent,
        StoryState context)
    {
        try
        {
            // Extraire les faits de l'événement
            var facts = _factExtractor.ExtractFrom(domainEvent, context);

            // Créer le memorandum
            var memorandum = new Memorandum(
                Id: Id.New(),
                StoryWorldId: worldId,
                CreatedAt: DateTime.UtcNow,
                FactualSummary: FormatFacts(facts),
                ImportantEntities: facts.SelectEntity().ToHashSet(),
                ImportantLocations: facts.SelectLocation().ToHashSet(),
                StateChanges: facts.SelectStateChanges().ToHashSet(),
                Level: MemoryLevel.Event,
                SourceEventIds: new[] { domainEvent.Id },
                ContentHash: ComputeHash(facts),
                Metadata: ComputeMetadata(new[] { domainEvent })
            );

            // Persister
            await _memoryStore.SaveAsync(memorandum);
            return Result<Memorandum>.Ok(memorandum);
        }
        catch (Exception ex)
        {
            return Result<Memorandum>.Fail($"Failed to remember event: {ex.Message}");
        }
    }

    // Autres méthodes...
}
```

---

### 3. Narratum.Memory.Layers

**Responsabilité**: Traiter la mémoire par niveaux hiérarchiques.

#### FactExtractorLayer

```csharp
public interface IFactExtractor
{
    IReadOnlyList<Fact> ExtractFrom(Event domainEvent, StoryState context);
    IReadOnlyList<Fact> ExtractFrom(IReadOnlyList<Event> events, StoryState context);
}

public class FactExtractorLayer : IFactExtractor
{
    public IReadOnlyList<Fact> ExtractFrom(Event domainEvent, StoryState context)
    {
        var facts = new List<Fact>();

        // Extraire selon le type d'événement
        switch (domainEvent)
        {
            case CharacterDeathEvent death:
                facts.Add(new Fact(
                    Id: Id.New(),
                    Content: $"{GetCharacterName(death.CharacterId)} died",
                    FirstMentionedAt: DateTime.UtcNow,
                    LastMentionedAt: DateTime.UtcNow,
                    OccurrenceCount: 1,
                    RelatedEntities: new HashSet<string> { GetCharacterName(death.CharacterId) },
                    Type: FactType.CharacterState,
                    IsCanonical: true
                ));
                break;

            case CharacterMovedEvent moved:
                facts.Add(new Fact(
                    Id: Id.New(),
                    Content: $"{GetCharacterName(moved.CharacterId)} moved from " +
                             $"{GetLocationName(moved.FromLocationId)} to " +
                             $"{GetLocationName(moved.ToLocationId)}",
                    FirstMentionedAt: DateTime.UtcNow,
                    LastMentionedAt: DateTime.UtcNow,
                    OccurrenceCount: 1,
                    RelatedEntities: new HashSet<string> 
                    { 
                        GetCharacterName(moved.CharacterId),
                        GetLocationName(moved.FromLocationId),
                        GetLocationName(moved.ToLocationId)
                    },
                    Type: FactType.Event,
                    IsCanonical: true
                ));
                break;

            // Autres types d'événements...
        }

        return facts;
    }

    // Autres méthodes...
}
```

#### SummaryGeneratorLayer

```csharp
public interface ISummaryGenerator
{
    string SummarizeChapter(IReadOnlyList<Fact> chapterFacts);
    string SummarizeArc(IReadOnlyList<string> chapterSummaries);
    string SummarizeWorld(IReadOnlyList<string> arcSummaries);
}

public class SummaryGeneratorLayer : ISummaryGenerator
{
    public string SummarizeChapter(IReadOnlyList<Fact> chapterFacts)
    {
        // Algorithme déterministe de résumé
        var importantFacts = chapterFacts
            .Where(f => f.IsCanonical)
            .OrderBy(f => f.FirstMentionedAt)
            .ToList();

        var summary = string.Join(
            " | ",
            importantFacts.Select(f => f.Content)
        );

        return summary.Length > 500 
            ? Truncate(summary, 500) 
            : summary;
    }

    public string SummarizeArc(IReadOnlyList<string> chapterSummaries)
    {
        // Agréger les résumés de chapitres
        var keyPoints = chapterSummaries
            .SelectMany(s => ExtractKeyPoints(s))
            .Distinct()
            .ToList();

        return string.Join(" → ", keyPoints);
    }

    public string SummarizeWorld(IReadOnlyList<string> arcSummaries)
    {
        // Créer une histoire globale
        var story = new StringBuilder();
        foreach (var arc in arcSummaries)
        {
            story.AppendLine($"• {arc}");
        }
        return story.ToString();
    }

    private IReadOnlyList<string> ExtractKeyPoints(string summary)
    {
        // Extraction simple : phrases après " | "
        return summary
            .Split("|")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private string Truncate(string text, int maxLength)
    {
        return text.Length > maxLength 
            ? text[..maxLength] + "…" 
            : text;
    }
}
```

---

### 4. Narratum.Memory.Coherence

**Responsabilité**: Valider la cohérence logique.

#### ICoherenceValidator (Interface)

```csharp
public interface ICoherenceValidator
{
    IReadOnlyList<CoherenceViolation> ValidateState(CanonicalState state);
    IReadOnlyList<CoherenceViolation> ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState);
    bool ContainsContradiction(Fact fact1, Fact fact2);
}
```

#### CoherenceValidator (Implémentation)

```csharp
public class CoherenceValidator : ICoherenceValidator
{
    public IReadOnlyList<CoherenceViolation> ValidateState(CanonicalState state)
    {
        var violations = new List<CoherenceViolation>();

        // Vérifier les contradictions internes
        var facts = state.Characters
            .SelectMany(c => ExtractCharacterFacts(c.Value))
            .Concat(state.Locations
                .SelectMany(l => ExtractLocationFacts(l.Value)))
            .ToList();

        for (int i = 0; i < facts.Count; i++)
        {
            for (int j = i + 1; j < facts.Count; j++)
            {
                if (ContainsContradiction(facts[i], facts[j]))
                {
                    violations.Add(new CoherenceViolation(
                        Id: Id.New(),
                        Description: $"Contradiction: {facts[i].Content} vs {facts[j].Content}",
                        Type: CoherenceViolationType.StatementContradiction,
                        ConflictingFact1: facts[i],
                        ConflictingFact2: facts[j],
                        DetectedAt: DateTime.UtcNow,
                        Severity: CoherenceSeverity.Error
                    ));
                }
            }
        }

        return violations;
    }

    public IReadOnlyList<CoherenceViolation> ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState)
    {
        var violations = new List<CoherenceViolation>();

        // Vérifier que les changements sont logiques
        foreach (var character in newState.Characters.Values)
        {
            if (previousState.Characters.TryGetValue(character.Id, out var prevChar))
            {
                // Un personnage mort ne peut pas devenir vivant
                if (prevChar.Status == VitalStatus.Dead && 
                    character.Status == VitalStatus.Alive)
                {
                    violations.Add(new CoherenceViolation(
                        Id: Id.New(),
                        Description: $"{character.Name} is dead but became alive",
                        Type: CoherenceViolationType.EntityInconsistency,
                        ConflictingFact1: new Fact(/* ... */),
                        ConflictingFact2: new Fact(/* ... */),
                        DetectedAt: DateTime.UtcNow,
                        Severity: CoherenceSeverity.Error
                    ));
                }
            }
        }

        return violations;
    }

    public bool ContainsContradiction(Fact fact1, Fact fact2)
    {
        // Logique simple : chercher "is" vs "is not" dans le contenu
        return fact1.Content.Contains("is ") && 
               fact2.Content.Contains("is not ") &&
               ExtractSubject(fact1) == ExtractSubject(fact2);
    }

    private string ExtractSubject(Fact fact)
    {
        // Extraire le sujet simple (première entité)
        return fact.RelatedEntities.FirstOrDefault() ?? "";
    }

    // Autres méthodes...
}
```

---

### 5. Narratum.Memory.Store

**Responsabilité**: Persister les memorias.

#### IMemoryRepository & IMemoryStore

```csharp
public interface IMemoryRepository
{
    Task<Memorandum?> GetByIdAsync(Id id);
    Task<IReadOnlyList<Memorandum>> GetByWorldAsync(Id worldId);
    Task<IReadOnlyList<Memorandum>> GetByEntityAsync(Id worldId, string entityName);
    Task SaveAsync(Memorandum memorandum);
    Task<Result<Unit>> DeleteAsync(Id id);
}

public interface IMemoryStore
{
    Task SaveAsync(Memorandum memorandum);
    Task SaveAsync(IReadOnlyList<Memorandum> memoria);
    Task<Memorandum?> RetrieveAsync(Id id);
    Task<IReadOnlyList<Memorandum>> QueryAsync(MemoryQuery query);
}

public record MemoryQuery(
    Id? WorldId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    MemoryLevel? Level = null,
    string? EntityFilter = null
);
```

#### SQLiteMemoryRepository (Implémentation)

```csharp
public class SQLiteMemoryRepository : IMemoryRepository
{
    private readonly MemoryDbContext _dbContext;

    public SQLiteMemoryRepository(MemoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Memorandum?> GetByIdAsync(Id id)
    {
        var entity = await _dbContext.Memoria
            .FirstOrDefaultAsync(m => m.Id == id.ToString());

        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Memorandum>> GetByWorldAsync(Id worldId)
    {
        var entities = await _dbContext.Memoria
            .Where(m => m.WorldId == worldId.ToString())
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task SaveAsync(Memorandum memorandum)
    {
        var entity = new MemorandumEntity
        {
            Id = memorandum.Id.ToString(),
            WorldId = memorandum.StoryWorldId.ToString(),
            CreatedAt = memorandum.CreatedAt,
            FactualSummary = memorandum.FactualSummary,
            Level = (int)memorandum.Level,
            ContentHash = memorandum.ContentHash,
            SerializedData = JsonSerializer.Serialize(memorandum)
        };

        await _dbContext.Memoria.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
    }

    // Autres méthodes...
}
```

---

## Algorithmes et Stratégies

### Stratégie de Résumé Hiérarchique

**Principe**: Les résumés se construisent de bas en haut.

```
Events (100+)
    ↓ [FactExtraction]
Facts (50)
    ↓ [Filtering]
Important Facts (15)
    ↓ [ChapterSummarization]
Chapter Summary (1 sentence)
    ↓ [Group multiple chapters]
Arc Summary (5 sentences)
    ↓ [Group multiple arcs]
World Summary (2-3 paragraphs)
```

### Algo 1: Détection de Contradictions (Déterministe)

```
Pour chaque paire de faits (F1, F2):
  1. Extraire le sujet de F1 et F2
  2. Si les sujets sont identiques:
     a. Chercher patterns opposés ("is" vs "is not")
     b. Chercher timeline impossible (après mort)
     c. Chercher états mutuellement exclusifs
  3. Si contradiction trouvée:
     → Créer une CoherenceViolation
```

### Algo 2: Extraction de Faits

```
Pour chaque Event E:
  1. Mapper le type d'événement → FactType
  2. Construire une phrase naturelle
  3. Extraire les entités mentionnées
  4. Déterminer si c'est un changement d'état
  5. Stocker les métadonnées (timestamp, source)
```

### Algo 3: Compression de l'Historique

```
Pour réduire N events en M facts:
  1. Grouper par entité (personnages, lieux)
  2. Pour chaque entité:
     a. Garder le premier et dernier état
     b. Détecter les changements importants
  3. Éliminer les transitions intermédiaires
  4. Conserver les événements critiques uniquement
```

---

## APIs Publiques

### Entrée de Phase 2 : IMemoryService

```csharp
// Setup
var memoryService = new MemoryService(
    repository,
    factExtractor,
    summaryGenerator,
    coherenceValidator,
    memoryStore
);

// Utilisation
var memorandum = await memoryService.RememberEventAsync(
    worldId: world.Id,
    domainEvent: characterDeathEvent,
    context: storyState
);

var summary = await memoryService.SummarizeHistoryAsync(
    worldId: world.Id,
    events: chapterEvents,
    targetLength: 500
);

var state = await memoryService.GetCanonicalStateAsync(
    worldId: world.Id,
    asOf: DateTime.UtcNow
);

var violations = await memoryService.ValidateCoherenceAsync(
    worldId: world.Id,
    memoria: allMemoria
);
```

---

## Plan de Développement

### Étape 2.1: Fondations des Types
- [ ] Créer Narratum.Memory.Models
- [ ] Implémenter Memorandum, Fact, CanonicalState
- [ ] Implémenter CoherenceViolation
- [ ] Tests unitaires pour les records (immutabilité, sérialisation)

### Étape 2.2: Couche d'Extraction
- [ ] Implémenter IFactExtractor
- [ ] Supports pour tous les types d'Event de Phase 1
- [ ] Tests: 1 fact = 1 event → vérification déterministe
- [ ] Tests: ensemble d'events → tous les faits extraits

### Étape 2.3: Couche de Résumé
- [ ] Implémenter ISummaryGenerator
- [ ] Résumé de chapitre (grouper les faits)
- [ ] Résumé d'arc (grouper les chapitres)
- [ ] Résumé du monde (histoire complète)
- [ ] Tests: résumés déterministes et stables

### Étape 2.4: Validation de Cohérence
- [ ] Implémenter ICoherenceValidator
- [ ] Détecter les contradictions simples
- [ ] Détecter les violations de séquence
- [ ] Détecter les incohérences d'entités
- [ ] Tests: cas de violation variés

### Étape 2.5: Persistance
- [ ] Créer MemoryDbContext (EF Core)
- [ ] Implémenter SQLiteMemoryRepository
- [ ] Tests: persistence et retrieval
- [ ] Tests: queryage avec filtres

### Étape 2.6: Service Principal
- [ ] Implémenter MemoryService
- [ ] Orchestrer toutes les couches
- [ ] Gestion d'erreurs robuste
- [ ] Logging détaillé

### Étape 2.7: Tests Complets
- [ ] Suite d'intégration complète
- [ ] Cas d'usage réalistes
- [ ] Historiques longs (50+ chapitres)
- [ ] Performance benchmarks

### Étape 2.8: Documentation
- [ ] Javadoc/XML comments
- [ ] Exemples d'utilisation
- [ ] Guides de cohérence
- [ ] Troubleshooting

---

## Tests et Validation

### Catégories de Tests

#### Tests Unitaires

```csharp
[Fact]
public void FactExtractor_CharacterDeathEvent_ExtractsCorrectFact()
{
    // Arrange
    var characterId = Id.New();
    var evt = new CharacterDeathEvent(characterId);
    var extractor = new FactExtractorLayer();

    // Act
    var facts = extractor.ExtractFrom(evt, storyState);

    // Assert
    Assert.Single(facts);
    Assert.Contains("dead", facts[0].Content);
    Assert.Equal(FactType.CharacterState, facts[0].Type);
}

[Fact]
public void SummaryGenerator_DeterministicOutput()
{
    // Même entrée = même sortie
    var facts = GetTestFacts();
    var generator = new SummaryGeneratorLayer();

    var summary1 = generator.SummarizeChapter(facts);
    var summary2 = generator.SummarizeChapter(facts);

    Assert.Equal(summary1, summary2);
}

[Fact]
public void CoherenceValidator_DetectsContradiction()
{
    // Arrange
    var aricAlive = new Fact(/* Aric is alive */);
    var aricDead = new Fact(/* Aric is dead */);
    var validator = new CoherenceValidator();

    // Act
    var result = validator.ContainsContradiction(aricAlive, aricDead);

    // Assert
    Assert.True(result);
}
```

#### Tests d'Intégration

```csharp
[Fact]
public async Task MemoryService_RememberAndRetrieve()
{
    // Arrange
    var service = new MemoryService(/* dependencies */);
    var evt = new CharacterDeathEvent(characterId);

    // Act
    var remember = await service.RememberEventAsync(worldId, evt, state);
    var retrieve = await service.RetrieveMemoriumAsync(remember.Value.Id);

    // Assert
    Assert.Equal(remember.Value.Id, retrieve.Value.Id);
}

[Fact]
public async Task MemoryService_Summarization_LongHistory()
{
    // Test avec 50+ événements
    var events = GenerateTestEvents(50);
    var summary = await memoryService.SummarizeHistoryAsync(
        worldId, events, targetLength: 500
    );

    Assert.NotEmpty(summary.Value);
    Assert.True(summary.Value.Length <= 600); // 500 + marge
}
```

#### Tests de Cohérence

```csharp
[Fact]
public void CoherenceValidator_CharacterCantBeAliveThenDead()
{
    // État 1: Aric alive
    var prevState = CreateState(aric: Alive);

    // État 2: Aric dead
    var newState = CreateState(aric: Dead);

    var violations = validator.ValidateTransition(prevState, newState);
    
    // Cette transition est possible (mort est finale)
    Assert.Empty(violations);
}

[Fact]
public void CoherenceValidator_CharacterCantBeDeadThenAlive()
{
    // État 1: Aric dead
    var prevState = CreateState(aric: Dead);

    // État 2: Aric alive
    var newState = CreateState(aric: Alive);

    var violations = validator.ValidateTransition(prevState, newState);
    
    // Cette transition est IMPOSSIBLE
    Assert.NotEmpty(violations);
    Assert.Single(violations);
}
```

### Critères de Validation

✅ **Déterminisme**
- Même historique → même résumé, toujours

✅ **Complétude**
- Tous les faits importants sont extraits
- Aucun événement majeur n'est omis

✅ **Cohérence**
- Les violations sont détectées
- Les transitions logiquement impossibles sont rejetées

✅ **Performance**
- Traiter 100 événements en < 500ms
- Résumer 50 chapitres en < 1s

✅ **Immuabilité**
- Les memorias ne changent jamais après création

---

## Interdictions Volontaires

### ❌ Pas de LLM
- Aucun appel OpenAI / Ollama / local LLM en Phase 2
- Les résumés sont **purement logiques**
- Si on veut du texte "beau", on attend Phase 5

### ❌ Pas de Stochastique
- Pas de randomisation
- Pas de température/top_p
- Pas de non-déterminisme quelconque

### ❌ Pas de Génération de Texte Libre
- Les résumés sont construits par concaténation
- Pas de "paraphrase naturelle"
- Format structuré seulement

### ❌ Pas de Modification du Core
- Le Core (Narratum.Core, Narratum.Domain, etc.) ne change pas
- Memory est **additive seulement**
- Phase 2 ne doit jamais toucher Phase 1

### ❌ Pas de Cache Non-Invalidable
- Tous les caches sont invalidables
- Les memorias doivent pouvoir être régénérées

---

## Prochaines Phases (Vue Globale)

### Phase 3 — Orchestration
- Pipeline complet avec agents simulés
- Intégration avec Phase 2

### Phase 4 — LLM Minimale
- Premier appel LLM (SummaryAgent uniquement)
- Reste du système inchangé

### Phase 5 — Narration Contrôlée
- NarratorAgent, CharacterAgent
- Génération cohérente de prose

### Phase 6 — UI
- Interface utilisateur
- Export narratif

---

## Conclusion

Phase 2 pose les bases du **contrôle narratif** sans IA.

Si le système peut résumer, retrouver et valider la cohérence d'une histoire, alors n'importe quel LLM peut générer du texte **sans casser la structure**.

**Le moteur doit être robuste avant la créativité.**

---

**Document Date**: 28 Décembre 2025  
**Status**: 📋 DESIGN DOCUMENT  
**Next Step**: Étape 2.1 — Fondations des Types
