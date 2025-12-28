# Phase 1 - Architecture et Conception

## Document d'architecture et de conception
### Fondations du moteur narratif (sans IA)

---

## 1. Objectif de la phase 1

Construire un **moteur narratif robuste, testable et persistant**, totalement **indépendant de toute IA**.

À la fin de cette phase :
- le moteur fonctionne **sans LLM**
- toute la logique narrative est **déterministe**
- les états peuvent être **sauvegardés / restaurés**
- le code est **stable, testable et extensible**

👉 Cette phase pose **90 % de la valeur structurelle** du projet.

---

## 2. Périmètre volontairement limité

### Inclus
- Modélisation du domaine narratif
- Gestion des états de l'histoire
- Gestion des univers, personnages, lieux
- Règles métier et invariants
- Persistance locale
- Tests unitaires

### Explicitement EXCLU
- IA / LLM
- Prompting
- UI graphique finale
- Génération de texte libre
- Optimisation des performances

---

## 3. Principes de conception

### 3.1 Séparation stricte des responsabilités
- Le moteur narratif **ne génère pas de texte**
- Il manipule des **faits, états et transitions**
- La narration est un **produit secondaire**, pas une source de vérité

### 3.2 Determinism first
À état initial identique + mêmes actions utilisateur  
→ résultat strictement identique.

### 3.3 Testabilité absolue
Tout comportement narratif doit être :
- reproductible
- vérifiable par tests
- indépendant d'entrées aléatoires

---

## 4. Architecture globale (Phase 1)

```
Narratum.Core
│
├─ Domain
│  ├─ StoryWorld
│  ├─ StoryArc
│  ├─ StoryChapter
│  ├─ Character
│  ├─ Location
│  └─ Event
│
├─ State
│  ├─ StoryState
│  ├─ CharacterState
│  └─ WorldState
│
├─ Rules
│  ├─ IStoryRule
│  ├─ Invariants
│  └─ Validators
│
└─ Services
   ├─ StoryProgressionService
   └─ StateTransitionService
```

---

## 5. Modèle de domaine

### 5.1 StoryWorld
Représente un univers narratif cohérent.

**Responsabilités :**
- règles globales
- chronologie
- métadonnées

**Attributs principaux :**
- `Id` : Identifiant unique
- `Name` : Nom de l'univers
- `Rules` : Collection de règles globales
- `Timeline` : Chronologie narrative
- `CreatedAt` : Date de création

---

### 5.2 StoryArc
Arc narratif structurant.

**Contient :**
- objectif narratif
- état de progression
- chapitres

**Attributs principaux :**
- `Id` : Identifiant unique
- `WorldId` : Référence au monde
- `Title` : Titre de l'arc
- `Objective` : Objectif narratif
- `Status` : État (ouvert, en cours, terminé)
- `Chapters` : Collection de chapitres

---

### 5.3 StoryChapter
Unité de progression atomique.

**Attributs :**
- `Id` : Identifiant unique
- `ArcId` : Référence à l'arc
- `Index` : Position dans l'arc
- `Status` : État (ouvert, terminé)
- `Events` : Événements canoniques produits
- `StartedAt` : Timestamp de début
- `CompletedAt` : Timestamp de fin (nullable)

---

### 5.4 Character
Entité persistante.

**Attributs :**
- `Id` : Identifiant unique
- `Name` : Nom du personnage
- `Traits` : Traits fixes (immuables)
- `Relationships` : Relations avec autres personnages
- `VitalStatus` : État vital (vivant, mort, inconnu)
- `CurrentLocationId` : Localisation actuelle

**Règles :**
- Un personnage mort ne peut pas être ressuscité
- Les traits fixes ne changent jamais
- Les relations sont bidirectionnelles

---

### 5.5 Location
Lieu dans l'univers narratif.

**Attributs :**
- `Id` : Identifiant unique
- `Name` : Nom du lieu
- `Description` : Description factuelle
- `ParentLocationId` : Lieu parent (nullable)
- `AccessibleFrom` : Lieux accessibles depuis celui-ci

---

### 5.6 Event
Fait narratif immuable.

**Exemples :**
- rencontre
- mort
- révélation
- déplacement

**Attributs :**
- `Id` : Identifiant unique
- `Type` : Type d'événement
- `Timestamp` : Moment narratif
- `ActorIds` : Personnages impliqués
- `LocationId` : Lieu de l'événement
- `Data` : Données spécifiques (JSON)

👉 Un événement **ne disparaît jamais**, il peut être masqué mais pas supprimé.

---

## 6. Gestion des états

### StoryState
Source unique de vérité.

**Contient :**
- état du monde
- état des personnages
- historique d'événements
- position narrative actuelle

**Attributs :**
- `WorldState` : État du monde
- `CharacterStates` : Collection des états des personnages
- `EventHistory` : Historique complet des événements
- `CurrentChapterId` : Chapitre actuel
- `NarrativeTime` : Temps narratif actuel

**Règle :**
> Aucune logique métier hors du StoryState et des Rules.

### CharacterState
État d'un personnage à un moment donné.

**Attributs :**
- `CharacterId` : Référence au personnage
- `VitalStatus` : État vital
- `LocationId` : Localisation
- `KnownFacts` : Faits connus par le personnage
- `LastSeenAt` : Dernier événement impliquant le personnage

### WorldState
État global de l'univers.

**Attributs :**
- `WorldId` : Référence au monde
- `NarrativeTime` : Temps narratif
- `ActiveArcId` : Arc actif
- `Metadata` : Métadonnées additionnelles

---

## 7. Règles métier et invariants

### 7.1 Invariants critiques
- Un personnage mort ne peut pas agir
- Un lieu inexistant ne peut pas être ciblé
- Le temps narratif est monotone (ne recule jamais)
- Un événement ne peut pas être annulé
- Les relations sont symétriques (A connaît B ⟺ B connaît A)

### 7.2 Mécanisme de règles

```csharp
public interface IStoryRule
{
    RuleResult Validate(StoryState state, StoryAction action);
}
```

**Les règles sont :**
- composables
- ordonnées
- testables indépendamment

**Types de règles :**
- **Règles de validation** : Vérifient qu'une action est autorisée
- **Règles d'invariants** : Garantissent la cohérence de l'état
- **Règles de progression** : Déterminent les événements résultants

---

## 8. Progression narrative

### StoryAction
Action utilisateur ou système.

**Exemples :**
- avancer le temps
- déplacer un personnage
- déclencher un événement
- terminer un chapitre

**Attributs :**
- `Type` : Type d'action
- `ActorId` : Personnage qui agit (nullable)
- `Parameters` : Paramètres de l'action
- `Timestamp` : Moment de l'action

### StoryProgressionService
Service orchestrant la progression.

**Responsabilités :**
1. Recevoir une `StoryAction`
2. Valider via les `IStoryRule`
3. Appliquer les transformations d'état
4. Générer les `Event` résultants
5. Mettre à jour le `StoryState`
6. Retourner le résultat

**Méthode principale :**
```csharp
public ProgressionResult Progress(StoryState state, StoryAction action)
{
    // 1. Valider l'action
    var validationResult = _ruleEngine.Validate(state, action);
    if (!validationResult.IsValid)
        return ProgressionResult.Invalid(validationResult.Errors);
    
    // 2. Appliquer l'action
    var events = _stateTransitionService.Apply(state, action);
    
    // 3. Valider les invariants
    var invariantResult = _invariantValidator.Validate(state);
    if (!invariantResult.IsValid)
        throw new InvalidStateException(invariantResult.Errors);
    
    return ProgressionResult.Success(events);
}
```

---

## 9. Persistance

### Choix technique
- **SQLite**
- Accès via **repository pattern**
- **Entity Framework Core** ou **Dapper** (à décider)

### Persisté
- `StoryWorld`
- `StoryState`
- `Events`
- `Characters`
- `Locations`
- Sauvegardes multiples (slots)

### Structure de persistance

**Tables principales :**
- `Worlds` : Univers narratifs
- `Arcs` : Arcs narratifs
- `Chapters` : Chapitres
- `Characters` : Personnages
- `Locations` : Lieux
- `Events` : Événements
- `States` : Snapshots d'états
- `SaveSlots` : Sauvegardes utilisateur

**Règle :**
> La persistance ne contient aucune logique métier.

---

## 10. Tests unitaires (obligatoires)

### Types de tests

1. **Tests de domaine**
   - Création d'univers
   - Création de personnages
   - Relations entre entités

2. **Tests de règles**
   - Validation des invariants
   - Scénarios d'échec
   - Règles composites

3. **Tests de progression**
   - Progression d'arc
   - Génération d'événements
   - Transitions d'état

4. **Tests de persistance**
   - Sauvegarde / restauration
   - Intégrité des données
   - Migration de schéma

5. **Tests de scénarios**
   - Scénarios narratifs complets (sans texte)
   - Cohérence sur 50+ actions
   - Reproductibilité déterministe

### Objectif
- **100 % des règles couvertes**
- Scénarios narratifs simulés **sans texte**
- Tests de non-régression automatisés

---

## 11. Livrables de fin de phase 1

### Code
- ✅ Solution .NET 10 structurée
- ✅ `Narratum.Core` complet
- ✅ `Narratum.Domain` avec toutes les entités
- ✅ `Narratum.State` avec gestion d'état
- ✅ `Narratum.Rules` avec moteur de règles
- ✅ `Narratum.Persistence` fonctionnelle

### Tests
- ✅ Suite de tests verte (100 % pass)
- ✅ Couverture > 80 %
- ✅ Tests de scénarios complets

### Documentation
- ✅ README Phase 1 (ce document)
- ✅ Documentation des entités
- ✅ Exemples d'utilisation
- ✅ Guide de contribution au code

---

## 12. Critères de validation (GO / NO GO)

### Tu peux :
- ✅ Créer un univers
- ✅ Ajouter des personnages et lieux
- ✅ Jouer 50 actions sans IA
- ✅ Restaurer une sauvegarde
- ✅ Détecter une incohérence (invariant violé)
- ✅ Modifier une règle sans casser le reste
- ✅ Reproduire exactement la même séquence

### Validation finale
**Si OUI à tout** → Phase 2  
**Si NON à un seul** → on ne continue pas.

---

## 13. Philosophie finale

> **« Si l'IA disparaît demain, le moteur doit survivre. »**

Cette phase transforme le projet en **vrai logiciel**, pas en démo.

---

## 14. Ordre de développement recommandé

### Étape 1 : Core & Domain (Semaine 1-2)
1. Créer les projets .NET
2. Implémenter les entités de base
3. Tests unitaires des entités

### Étape 2 : State Management (Semaine 2-3)
1. Implémenter `StoryState`
2. Implémenter les transitions
3. Tests de cohérence d'état

### Étape 3 : Rules Engine (Semaine 3-4)
1. Interface `IStoryRule`
2. Règles de base
3. Moteur de validation
4. Tests de règles

### Étape 4 : Progression Service (Semaine 4-5)
1. `StoryProgressionService`
2. `StateTransitionService`
3. Tests de progression

### Étape 5 : Persistence (Semaine 5-6)
1. Schéma de base de données
2. Repositories
3. Tests de persistance

### Étape 6 : Integration & Validation (Semaine 6-7)
1. Tests d'intégration
2. Scénarios complets
3. Documentation finale
4. Revue de code

---

## 15. Stack technique Phase 1

### Langage & Framework
- .NET 10
- C# 13

### Persistance
- SQLite
- Entity Framework Core 9.0 (ou Dapper)

### Tests
- xUnit
- FluentAssertions
- NSubstitute (pour mocks si nécessaire)

### Outils
- Analyzers .NET (déjà configurés dans Directory.Build.props)
- Code coverage (coverlet)

---

## Annexe : Exemple de flux complet

```csharp
// 1. Créer un univers
var world = new StoryWorld("Royaume d'Eldoria");

// 2. Ajouter des personnages
var hero = new Character("Aric", VitalStatus.Alive);
var mentor = new Character("Gandalf", VitalStatus.Alive);

// 3. Créer un arc narratif
var arc = new StoryArc(world.Id, "La Quête du Cristal");

// 4. Démarrer un chapitre
var chapter = arc.StartChapter();

// 5. Action utilisateur
var action = new StoryAction(
    type: ActionType.MoveCharacter,
    actorId: hero.Id,
    parameters: new { destinationId = "forest_entrance" }
);

// 6. Progression
var result = progressionService.Progress(state, action);

// 7. Vérifier le résultat
Assert.True(result.IsSuccess);
Assert.Contains(result.Events, e => e.Type == EventType.CharacterMoved);

// 8. Sauvegarder
await repository.SaveState(state);
```

Ce flux doit fonctionner **sans aucune IA**.
