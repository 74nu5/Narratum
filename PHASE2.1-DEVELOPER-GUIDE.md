# Guide Développeur - Phase 2.1 Memory Models

## Vue d'ensemble

Ce guide explique comment utiliser les models immutables de mémoire narratif créés dans la Phase 2.1.

## Structure Hiérarchique

La mémoire narrative est organisée en 4 niveaux hiérarchiques:

```
World (Monde)
  └── Arc (Arc narratif)
       └── Chapter (Chapitre)
            └── Event (Événement)
```

Chaque niveau a son propre état canonique indépendant.

## Concepts Clés

### 1. Fact (Fait)
Un fait est une déclaration atomique extraite du texte narratif.

```csharp
// Créer un fait
var fact = Fact.Create(
    content: "Aric is dead",
    factType: FactType.CharacterState,
    memoryLevel: MemoryLevel.Event,
    entityReferences: new[] { "Aric" },
    timeContext: "After the battle",
    confidence: 0.95,
    source: "Chapter 3"
);

// Valider le fait
if (fact.Validate())
{
    // Le fait est valide
}
```

**Types de faits:**
- `CharacterState` - État d'un personnage ("Aric is dead")
- `LocationState` - État d'un lieu ("Tower is destroyed")
- `Relationship` - Relations entre entités ("Aric trusts Lyra")
- `Knowledge` - Information abstraite ("Crystal has power")
- `Event` - Événement narratif ("Combat occurred")
- `Contradiction` - Contradiction détectée

### 2. CanonicalState (État Canonique)
Représente l'ensemble des faits "accepted as true" à un niveau donné.

```csharp
// Créer un état vide
var state = CanonicalState.CreateEmpty(worldId, MemoryLevel.Event);

// Ajouter un fait (retourne un nouvel état)
var newState = state.AddFact(fact);

// Ajouter plusieurs faits
var updatedState = state.AddFacts(new[] { fact1, fact2, fact3 });

// Récupérer les faits d'une entité
var aricFacts = state.GetFactsForEntity("Aric");

// Récupérer les faits d'un type
var stateChanges = state.GetFactsByType(FactType.CharacterState);

// Accéder au nombre de faits
int count = state.FactCount;
```

### 3. CoherenceViolation (Violation de Cohérence)
Détecte et enregistre les incohérences logiques.

```csharp
// Créer une violation
var violation = CoherenceViolation.Create(
    violationType: CoherenceViolationType.StatementContradiction,
    severity: CoherenceSeverity.Error,
    description: "Aric is both alive and dead",
    involvedFactIds: new[] { factId1, factId2 },
    resolution: "Choose the canonical fact and deprecate the other"
);

// Marquer comme résolue
var resolvedViolation = violation.MarkResolved();

// Vérifier le statut
if (violation.IsResolved)
{
    // La violation a été résolue
}

// Obtenir une description complète
string fullDesc = violation.GetFullDescription();
```

**Types de violations:**
- `StatementContradiction` - "X is true" vs "X is false"
- `SequenceViolation` - Timeline impossible
- `EntityInconsistency` - État incohérent
- `LocationInconsistency` - État incohérent du lieu

### 4. Memorandum (Mémorandum)
Le container principal de toute la mémoire narrative du monde.

```csharp
// Créer un mémorandum vide
var memo = Memorandum.CreateEmpty(
    worldId: Guid.NewGuid(),
    title: "Aethelmere World State",
    description: "Complete state of the world"
);

// Ajouter un fait à un niveau
var updatedMemo = memo.AddFact(MemoryLevel.Event, fact);

// Ajouter plusieurs faits
var updatedMemo2 = memo.AddFacts(MemoryLevel.Chapter, facts);

// Enregistrer une violation
var memo3 = updatedMemo.AddViolation(violation);

// Résoudre une violation
var memo4 = memo3.ResolveViolation(violationId);

// Récupérer l'état à un niveau
var eventState = memo.GetCanonicalState(MemoryLevel.Event);

// Récupérer les faits d'un niveau
var allFacts = memo.GetFacts(MemoryLevel.Event);

// Requête croisée
var aricFacts = memo.GetFactsForEntity(MemoryLevel.Event, "Aric");

// Filtrer les violations
var unresolved = memo.GetUnresolvedViolations();
var errors = memo.GetViolationsBySeverity(CoherenceSeverity.Error);

// Obtenir un résumé
string summary = memo.GetSummary();
```

## Patterns d'Utilisation

### Pattern 1: Chaîner les Opérations

```csharp
var memo = Memorandum.CreateEmpty(worldId, "World")
    .AddFact(MemoryLevel.Event, fact1)
    .AddFact(MemoryLevel.Event, fact2)
    .AddViolation(violation1)
    .ResolveViolation(violation1.Id);
```

### Pattern 2: Immutabilité pour Audit

```csharp
var memo1 = Memorandum.CreateEmpty(worldId, "World");
var memo2 = memo1.AddFact(MemoryLevel.Event, fact);

// memo1 est inchangé
Assert.Empty(memo1.GetFacts(MemoryLevel.Event));

// memo2 contient le nouveau fait
Assert.Single(memo2.GetFacts(MemoryLevel.Event));
```

### Pattern 3: Requêtes Complexes

```csharp
// Récupérer tous les personnages mentionnés
var characterStates = memo.GetFacts(MemoryLevel.Event)
    .Where(f => f.FactType == FactType.CharacterState)
    .SelectMany(f => f.EntityReferences);

// Vérifier la cohérence d'une entité
var aricFacts = memo.GetFactsForEntity(MemoryLevel.Event, "Aric");
if (aricFacts.Count() > 1)
{
    // Vérifier les contradictions
}
```

### Pattern 4: Validation

```csharp
if (!fact.Validate())
{
    throw new ArgumentException("Fact is invalid");
}

if (!memo.Validate())
{
    throw new InvalidOperationException("Memorandum contains invalid data");
}
```

## Sérialisation (Future)

Les records immutables sont conçues pour être sérialisées facilement:

```csharp
// Préparé pour JSON serialization (implémentation future)
var json = JsonSerializer.Serialize(memo);
var restored = JsonSerializer.Deserialize<Memorandum>(json);
```

## Performance et Considérations

### Immutabilité
- **Avantage:** Sécurité des threads, audit trail naturel
- **Coût:** Allocation mémoire pour chaque modification
- **Mitigation:** Utiliser dans des contextes non-temps-réel

### Requêtes
- **Filtre par entité:** O(n) - parcourt tous les faits
- **Filtre par type:** O(n) - parcourt tous les faits
- **Ajout de faits:** O(1) amorti avec HashSet

### Validation
- Appelez `Validate()` seulement quand nécessaire
- Utilisée automatiquement lors de l'ajout à CanonicalState

## Erreurs Courantes

### 1. Oublier que l'Immutabilité Retourne une Nouvelle Instance

```csharp
// ❌ MAUVAIS
memo.AddFact(level, fact);
// memo n'a pas changé !

// ✅ BON
memo = memo.AddFact(level, fact);
// ou
var updatedMemo = memo.AddFact(level, fact);
```

### 2. Ajouter des Faits Invalides

```csharp
// ❌ MAUVAIS
var invalidFact = new Fact(
    Id: Guid.NewGuid(),
    Content: "",  // Content vide !
    FactType: FactType.CharacterState,
    MemoryLevel: MemoryLevel.Event,
    EntityReferences: new HashSet<string>()  // Pas d'entité !
);

// ✅ BON
var fact = Fact.Create(
    content: "Aric is dead",
    factType: FactType.CharacterState,
    memoryLevel: MemoryLevel.Event,
    entityReferences: new[] { "Aric" }
);
```

### 3. Confondre les Niveaux

```csharp
// ❌ MAUVAIS
var facts = memo.GetFacts(MemoryLevel.Event);
// Les faits du niveau Chapter ne sont pas inclus !

// ✅ BON - Récupérer les faits de tous les niveaux
var allFacts = new[] { 
    MemoryLevel.Event,
    MemoryLevel.Chapter,
    MemoryLevel.Arc,
    MemoryLevel.World
}.SelectMany(level => memo.GetFacts(level));
```

## Tests

Voir `Memory.Tests/` pour les exemples complets:

```csharp
[Fact]
public void TestAddingFacts()
{
    var memo = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
    var fact = Fact.Create("Test fact", FactType.Event, MemoryLevel.Event, new[] { "Entity" });
    
    var updated = memo.AddFact(MemoryLevel.Event, fact);
    
    Assert.Single(updated.GetFacts(MemoryLevel.Event));
}
```

## Roadmap

Les phases suivantes amélioreront ce modèle:

1. **Phase 2.2:** Sérialisation JSON
2. **Phase 2.3:** Repository Pattern pour persistance
3. **Phase 2.4:** Moteur de détection d'incohérence
4. **Phase 2.5:** Cache et optimisations
5. **Phase 3:** Intégration avec le moteur de simulation

---

**Dernière mise à jour:** 2025-01-22  
**Version:** 1.0 (Phase 2.1)
