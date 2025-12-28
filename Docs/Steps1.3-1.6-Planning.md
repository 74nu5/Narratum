# Étapes 1.3-1.6 : Planification et prochaines actions

## Vue d'ensemble

L'**étape 1.2** (Core & Domain) est **✅ COMPLÉTÉE**.

Les étapes suivantes vont construire sur cette fondation solide :

```
Phase 1: Fondations (SANS IA)
├── ✅ 1.1 Structure initiale     (COMPLÉTÉE)
├── ✅ 1.2 Core & Domain          (COMPLÉTÉE)
├── ⏳ 1.3 State Management        (À FAIRE)
├── ⏳ 1.4 Rules Engine            (À FAIRE)
├── ⏳ 1.5 Persistence            (À FAIRE)
└── ⏳ 1.6 Tests unitaires        (À FAIRE)
```

---

## Étape 1.3 : State Management

**Objectif** : Orchestrer les transitions d'état et gérer l'historique.

### Responsabilités
- Coordonner les changements d'état
- Appliquer les actions narratives
- Générer les événements
- Maintenir l'intégrité de l'état

### Fichiers à créer

#### Simulation/IStateTransitionService.cs
```csharp
namespace Narratum.Simulation;

public interface IStateTransitionService
{
    /// <summary>
    /// Applies an action to the current state and returns the new state.
    /// </summary>
    Result<StoryState> ApplyAction(StoryState state, StoryAction action);
    
    /// <summary>
    /// Validates if an action can be applied to the current state.
    /// </summary>
    Result<Unit> ValidateAction(StoryState state, StoryAction action);
}
```

#### Simulation/IProgressionService.cs
```csharp
namespace Narratum.Simulation;

public interface IProgressionService
{
    /// <summary>
    /// Progresses the story to the next event.
    /// </summary>
    Result<StoryState> Progress(StoryState state, StoryAction action);
    
    /// <summary>
    /// Gets the current chapter.
    /// </summary>
    StoryChapter? GetCurrentChapter(StoryState state);
    
    /// <summary>
    /// Advances to the next chapter.
    /// </summary>
    Result<StoryState> AdvanceChapter(StoryState state);
}
```

#### Simulation/StoryAction.cs
```csharp
namespace Narratum.Simulation;

/// <summary>
/// Represents an action in the narrative (player or system action).
/// </summary>
public abstract record StoryAction
{
    public Id Id { get; } = Id.New();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record MoveCharacterAction(Id CharacterId, Id ToLocationId) : StoryAction;
public record EndChapterAction(Id ChapterId) : StoryAction;
public record CreateEventAction(string EventType, Id[] ActorIds, Id? LocationId = null) : StoryAction;
```

#### Simulation/StateTransitionService.cs
```csharp
namespace Narratum.Simulation;

public class StateTransitionService : IStateTransitionService
{
    private readonly IEnumerable<IStoryRule> _rules;
    
    public StateTransitionService(IEnumerable<IStoryRule> rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }
    
    public Result<StoryState> ApplyAction(StoryState state, StoryAction action)
    {
        // 1. Validate using rules
        foreach (var rule in _rules)
        {
            var validationResult = rule.Validate(state, action);
            if (validationResult is Result<Unit>.Failure failure)
                return Result<StoryState>.Fail(failure.Message);
        }
        
        // 2. Apply the action and return new state
        // Implementation depends on action type
        return ProcessAction(state, action);
    }
    
    public Result<Unit> ValidateAction(StoryState state, StoryAction action)
    {
        // Validate without applying
        foreach (var rule in _rules)
        {
            var validationResult = rule.Validate(state, action);
            if (validationResult is Result<Unit>.Failure failure)
                return validationResult;
        }
        return Result<Unit>.Ok(default);
    }
    
    private Result<StoryState> ProcessAction(StoryState state, StoryAction action) { }
}
```

#### Simulation/ProgressionService.cs
Implémente `IProgressionService` en orchestrant transitions et chapitres.

### Tests requis
- `StateTransitionServiceTests.cs` (8+ tests)
- `ProgressionServiceTests.cs` (8+ tests)
- Validation des règles avant transition
- Génération correcte d'événements
- Progression des chapitres

### Dépendances
- `Narratum.Core`
- `Narratum.Domain`
- `Narratum.State`

---

## Étape 1.4 : Rules Engine

**Objectif** : Implémenter le moteur de règles narratives.

### Responsabilités
- Évaluer les conditions
- Appliquer les effets
- Garantir les invariants
- Valider les transitions

### Fichiers à créer

#### Rules/IStoryRuleImplementation.cs
```csharp
namespace Narratum.Rules;

public abstract class StoryRuleBase : IStoryRule
{
    public string Name { get; protected init; }
    
    public virtual Result<Unit> Validate(object state, object action)
    {
        return ValidateInternal((StoryState)state, (StoryAction)action);
    }
    
    protected abstract Result<Unit> ValidateInternal(StoryState state, StoryAction action);
}
```

#### Rules/CharacterInvariants/

```csharp
// DeadCharacterCannotMoveRule.cs
public class DeadCharacterCannotMoveRule : StoryRuleBase
{
    public DeadCharacterCannotMoveRule() 
        => Name = "DeadCharacterCannotMove";
    
    protected override Result<Unit> ValidateInternal(StoryState state, StoryAction action)
    {
        if (action is not MoveCharacterAction move)
            return Result<Unit>.Ok(default);
        
        var character = state.GetCharacter(move.CharacterId);
        if (character?.VitalStatus == VitalStatus.Dead)
            return Result<Unit>.Fail("Dead characters cannot move");
        
        return Result<Unit>.Ok(default);
    }
}

// CharacterMustExistRule.cs
public class CharacterMustExistRule : StoryRuleBase { }

// LocationMustBeAccessibleRule.cs
public class LocationMustBeAccessibleRule : StoryRuleBase { }
```

#### Rules/TimeInvariants/

```csharp
// TimeCannotGoBackwardsRule.cs
public class TimeCannotGoBackwardsRule : StoryRuleBase { }
```

#### Rules/EventRules/

```csharp
// EventActorsMustExistRule.cs
public class EventActorsMustExistRule : StoryRuleBase { }

// EventLocationMustExistRule.cs
public class EventLocationMustExistRule : StoryRuleBase { }
```

### Tests requis
- `CharacterInvariantTests.cs` (6+ tests)
- `TimeInvariantTests.cs` (4+ tests)
- `EventRuleTests.cs` (6+ tests)
- Validation des règles composées
- Ordre d'exécution des règles

### Dépendances
- `Narratum.Core`
- `Narratum.Domain`
- `Narratum.State`
- `Narratum.Simulation`

---

## Étape 1.5 : Persistence

**Objectif** : Implémenter la sauvegarde et le chargement des états.

### Responsabilités
- Sérialisation/désérialisation
- Stockage SQLite
- Gestion des migrations
- Snapshots et restauration

### Fichiers à créer

#### Persistence/PersistenceDbContext.cs
```csharp
namespace Narratum.Persistence;

public class PersistenceDbContext : DbContext
{
    public DbSet<StoryWorldDto> Worlds { get; set; }
    public DbSet<StoryArcDto> Arcs { get; set; }
    public DbSet<StoryChapterDto> Chapters { get; set; }
    public DbSet<CharacterDto> Characters { get; set; }
    public DbSet<LocationDto> Locations { get; set; }
    public DbSet<EventDto> Events { get; set; }
    public DbSet<StateSnapshotDto> StateSnapshots { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=narratum.db");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuration des entités
    }
}
```

#### Persistence/Repositories/

```csharp
// IWorldRepository.cs
public interface IWorldRepository : IRepository<StoryWorld, Id> { }

// ICharacterRepository.cs
public interface ICharacterRepository : IRepository<Character, Id> { }

// ILocationRepository.cs
public interface ILocationRepository : IRepository<Location, Id> { }

// IEventRepository.cs
public interface IEventRepository
{
    Task<Result<Event>> AddAsync(Event storyEvent);
    Task<Result<IReadOnlyList<Event>>> GetByChapterAsync(Id chapterId);
    Task<Result<IReadOnlyList<Event>>> GetAllAsync();
}

// IStateRepository.cs
public interface IStateRepository
{
    Task<Result<StateSnapshot>> SaveSnapshotAsync(StateSnapshot snapshot);
    Task<Result<StateSnapshot>> LoadSnapshotAsync(Id snapshotId);
    Task<Result<IReadOnlyList<StateSnapshot>>> GetAllSnapshotsAsync();
}
```

#### Persistence/Implementations/

Implémenter chaque repository avec EF Core.

### Tests requis
- `RepositoryTests.cs` (12+ tests)
- `SerializationTests.cs` (8+ tests)
- `SnapshotTests.cs` (6+ tests)
- Intégrité des données
- Restauration correcte

### Dépendances
- `Narratum.Core`
- `Narratum.Domain`
- `Narratum.State`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Sqlite`

---

## Étape 1.6 : Tests unitaires complets

**Objectif** : Couverture complète et tests de régression.

### Objectifs de qualité
- ✅ Couverture > 80%
- ✅ Tous les cas d'erreur testés
- ✅ Scénarios de régression (5+)
- ✅ Performance < 10s

### Tests à ajouter

#### Tests par module
```
Core.Tests/
├── IdTests.cs                    (4 tests)
├── ResultTests.cs                (6 tests)
└── EnumsTests.cs                 (3 tests)

Domain.Tests/
├── StoryWorldTests.cs            (6 tests)
├── StoryArcTests.cs              (8 tests)
├── StoryChapterTests.cs          (8 tests)
├── CharacterTests.cs             (10 tests)
├── LocationTests.cs              (8 tests)
├── RelationshipTests.cs          (6 tests)
└── EventTests.cs                 (10 tests)

State.Tests/
├── CharacterStateTests.cs        (8 tests)
├── WorldStateTests.cs            (6 tests)
└── StoryStateTests.cs            (10 tests)

Rules.Tests/
├── CharacterInvariantTests.cs    (8 tests)
├── TimeInvariantTests.cs         (6 tests)
└── EventRuleTests.cs             (8 tests)

Simulation.Tests/
├── StateTransitionServiceTests.cs (10 tests)
├── ProgressionServiceTests.cs    (10 tests)
└── StoryActionTests.cs           (6 tests)

Persistence.Tests/
├── RepositoryTests.cs            (12 tests)
├── SerializationTests.cs         (8 tests)
└── SnapshotTests.cs              (6 tests)
```

#### Scénarios de régression (5 scenarios)

1. **Hero's Journey** (12 étapes)
2. **Betrayal Arc** (15 étapes)
3. **Time-sensitive Events** (10 étapes)
4. **Multi-character Interaction** (8 étapes)
5. **State Restoration** (réplay exact)

### Configuration pour coverage

```csharp
// .coverletrc.json
{
  "include": [ "Narratum.*" ],
  "exclude": [ "Narratum.Tests" ],
  "include-test-assembly": true,
  "single-hit-breakpoint": false,
  "use-source-link": true,
  "threshold": 80
}
```

### Exécution
```bash
dotnet test /p:CollectCoverage=true /p:CoverageDirectory=coverage
```

---

## Dépendances inter-étapes

```
Étape 1.3 (State Management)
  ├── Dépend: Core, Domain, State
  └── Fournit: IStateTransitionService, IProgressionService

Étape 1.4 (Rules Engine)
  ├── Dépend: Core, Domain, State, Simulation(1.3)
  └── Fournit: Implémentations de IStoryRule

Étape 1.5 (Persistence)
  ├── Dépend: Core, Domain, State
  └── Fournit: Repositories, DbContext

Étape 1.6 (Tests)
  ├── Dépend: Tous les modules
  └── Fournit: Couverture et régression
```

---

## Checklist d'implémentation

### Avant de commencer chaque étape
- [ ] Lire la spécification complète
- [ ] Créer les interfaces (ports)
- [ ] Écrire les tests (TDD)
- [ ] Implémenter les classes
- [ ] Vérifier la compilation
- [ ] Exécuter les tests
- [ ] Documenter les changements

### Pour chaque nouveau fichier
- [ ] Namespaces corrects
- [ ] Documentation XML complète
- [ ] Pas de warnings
- [ ] Tests unitaires

### Avant de merger
- [ ] Tous les tests passent
- [ ] Pas de régression
- [ ] Couverture > 80%
- [ ] Compilation Release OK
- [ ] Documentation mise à jour

---

## Ressources

### Documentation existante
- `Docs/Phase1-Design.md` - Architecture détaillée
- `Docs/HiddenWorldSimulation.md` - Simulation hors-scène
- `Tests/Phase1Step2IntegrationTests.cs` - Exemples tests

### Patterns à appliquer
- Repository Pattern (Persistence)
- Service Pattern (Simulation, Rules)
- Strategy Pattern (Rules)
- Command Pattern (Actions)

### Best practices
- Immutabilité stricte
- Déterminisme garanti
- Séparation concerns
- Zero side-effects
- Exhaustive validation

---

## Timeline estimée

| Étape | Estimation | Priorité |
|-------|-----------|----------|
| 1.3 | 4-6h | 🔴 HAUTE |
| 1.4 | 4-6h | 🔴 HAUTE |
| 1.5 | 4-6h | 🔴 HAUTE |
| 1.6 | 3-4h | 🟡 MOYENNE |
| **Total** | **15-22h** | |

---

## Validation finale de Phase 1

À la fin de l'étape 1.6, vous devrez pouvoir:

1. ✅ Créer un univers (StoryWorld)
2. ✅ Définir des personnages (Character)
3. ✅ Créer un arc narratif (StoryArc)
4. ✅ Avancer l'histoire chapitre par chapitre (1.3)
5. ✅ Évaluer des règles narratives (1.4)
6. ✅ Sauvegarder l'état complet (1.5)
7. ✅ Charger un état sauvegardé (1.5)
8. ✅ Reproduire exactement la même séquence (1.3-1.6)

**Tout sans texte généré.**

---

*Prochaines étapes bien définies. Prêt pour l'étape 1.3.*
