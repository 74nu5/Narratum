# Démarrage rapide - Étape 1.2

Guide pratique pour utiliser les entités créées dans l'étape 1.2 (Core & Domain).

## Installation

```bash
cd d:\Perso\Narratum
dotnet build
dotnet test
```

Tous les tests devraient passer (17/17).

## Exemples d'utilisation

### 1. Créer un univers narratif

```csharp
using Narratum.Domain;
using Narratum.Core;

// Créer un univers
var world = new StoryWorld(
    name: "Aethermoor",
    description: "Un monde magique et mystérieux"
);

Console.WriteLine($"Monde créé: {world.Name} (ID: {world.Id.Value})");
```

### 2. Créer des personnages

```csharp
// Définir des traits (immuables)
var traits = new Dictionary<string, string>
{
    { "class", "warrior" },
    { "alignment", "neutral good" },
    { "race", "human" }
};

// Créer un personnage
var hero = new Character("Aric le Brave", traits);
var villain = new Character("Malachar l'Ancien");

Console.WriteLine($"{hero.Name} - Status: {hero.VitalStatus}");
```

### 3. Créer des lieux

```csharp
// Lieux simple
var forest = new Location(
    name: "Forêt de Silvermist",
    description: "Une ancienne forêt enveloppée de magie"
);

// Lieu hiérarchique
var cave = new Location(
    name: "Caverne d'Obsidienne",
    description: "Une grotte sombre dans les montagnes",
    parentLocationId: forest.Id
);

// Définir l'accessibilité
forest.AddAccessibleLocation(cave.Id);
```

### 4. Créer un arc narratif

```csharp
// Créer un arc
var arc = new StoryArc(
    worldId: world.Id,
    title: "La Quête du Cristal",
    objective: "Trouver le cristal légendaire caché dans les montagnes"
);

// Démarrer l'arc
arc.Start(); // Passe en InProgress

// Créer des chapitres
for (int i = 0; i < 5; i++)
{
    var chapter = new StoryChapter(arc.Id, index: i);
    arc.AddChapter(chapter);
}
```

### 5. Gérer les relations

```csharp
// Établir une relation
var relationship = new Relationship(
    type: "ally",
    trust: 75,
    affection: 50,
    notes: "Allié de longue date"
);

hero.SetRelationship(villain.Id, relationship);

// Consulter une relation
var rel = hero.GetRelationship(villain.Id);
if (rel != null)
{
    Console.WriteLine($"Trust: {rel.Trust}, Affection: {rel.Affection}");
    
    // Mettre à jour les sentiments
    var updated = rel.UpdateTrust(10);
    hero.SetRelationship(villain.Id, updated);
}
```

### 6. Événements immuables

```csharp
// Créer des événements (immuables)
var encounter = new CharacterEncounterEvent(hero.Id, villain.Id, forest.Id);
var death = new CharacterDeathEvent(villain.Id, forest.Id, cause: "Defeated in combat");
var revelation = new RevelationEvent(hero.Id, "Malachar était son frère perdu");

Console.WriteLine($"Event 1: {encounter.Type}");
Console.WriteLine($"Event 2: {death.Type} - Cause: {death.GetCause()}");
```

### 7. Gestion d'état - Immutabilité contrôlée

```csharp
using Narratum.State;

// Créer un état initial
var storyState = StoryState.Create(world.Id, "Aethermoor");

// Ajouter des personnages (retourne nouvel état)
var heroState = new CharacterState(hero.Id, "Aric", VitalStatus.Alive, forest.Id);
var villainState = new CharacterState(villain.Id, "Malachar", VitalStatus.Alive, forest.Id);

var state1 = storyState
    .WithCharacter(heroState)
    .WithCharacter(villainState);

// Événements - Ajouter un événement (retourne nouvel état)
var state2 = state1.WithEvent(encounter);
var state3 = state2.WithEvent(death);

// État original inchangé
Console.WriteLine($"État 1: {state1.EventHistory.Count} événements");
Console.WriteLine($"État 2: {state2.EventHistory.Count} événements");
Console.WriteLine($"État 3: {state3.EventHistory.Count} événements");

// Mettre à jour la localisation d'un personnage
var newHeroState = heroState.MoveTo(cave.Id);
var state4 = state1.WithCharacter(newHeroState);
```

### 8. Snapshots pour persistance

```csharp
// Créer un snapshot du state actuel
var snapshot = state3.CreateSnapshot();

Console.WriteLine($"Snapshot: {snapshot.Description}");
Console.WriteLine($"Créé à: {snapshot.CreatedAt}");

// Le state peut être restauré à partir du snapshot
var restoredState = snapshot.State;
```

### 9. Transitions d'état avec validation

```csharp
// Les transitions sont contrôlées et validées
try
{
    // ✅ Valide: personnage vivant se déplace
    var alive = new CharacterState(hero.Id, "Aric", VitalStatus.Alive, forest.Id);
    var moved = alive.MoveTo(cave.Id);
    
    // ❌ Invalide: personnage mort ne peut pas se déplacer
    var dead = new CharacterState(villain.Id, "Malachar", VitalStatus.Dead, forest.Id);
    var cannotMove = dead.MoveTo(cave.Id); // Lance une exception
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Erreur: {ex.Message}");
}
```

## Invariants garantis

Le moteur garantit les invariants suivants :

✅ **Personnages morts ne peuvent pas agir**
- `.MoveTo()` lance une exception si `VitalStatus == Dead`

✅ **Traits immuables**
- `Character.Traits` est readonly
- Impossible de modifier après création

✅ **Événements immuables**
- Événements dans `EventHistory` ne disparaissent jamais
- Historique est `IReadOnlyList<Event>`

✅ **Temps monotone**
- `WorldState.AdvanceTime()` lance exception si delta < 0

✅ **Pas de self-relationships**
- `.SetRelationship()` lance exception si `otherCharacterId == this.Id`

✅ **Immuabilité d'état**
- `StoryState` est un record - créer un nouvel état à chaque mutation
- Aucune modification in-place

## Structure du projet

```
Narratum/
├── Core/
│   ├── Id.cs              - Identifiants uniques
│   ├── Result.cs          - Gestion d'erreurs fonctionnelle
│   ├── IStoryRule.cs      - Interface des règles
│   ├── IRepository.cs     - Interface persistance
│   ├── DomainEvent.cs     - Base événements
│   └── Enums.cs           - Énumérations
├── Domain/
│   ├── StoryWorld.cs      - Univers narratif
│   ├── StoryArc.cs        - Arc narratif
│   ├── StoryChapter.cs    - Chapitre
│   ├── Character.cs       - Personnage
│   ├── Location.cs        - Lieu
│   ├── Relationship.cs    - Relations (Value Object)
│   └── Event.cs           - Événements (abstrait + 4 implémentations)
├── State/
│   ├── CharacterState.cs  - État personnage
│   ├── WorldState.cs      - État monde
│   └── StoryState.cs      - État complet + Snapshot
└── Tests/
    └── Phase1Step2IntegrationTests.cs - 17 tests
```

## Exécution des tests

```bash
# Tous les tests
dotnet test

# Tests spécifiques
dotnet test --filter CreateStoryWorld_ShouldSucceed

# Avec coverage
dotnet test /p:CollectCoverage=true
```

## Prochaines étapes

**Étape 1.3** : State Management
- Services de transition d'état
- Historique des changements
- Replay d'événements

**Étape 1.4** : Rules Engine
- Moteur d'évaluation des règles
- Règles narratives de base
- Validation déterministe

---

Pour plus de détails, consulter [Phase1-Design.md](Phase1-Design.md).
