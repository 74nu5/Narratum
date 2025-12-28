# Tests

Ce dossier contient tous les tests du projet.

## Responsabilités

- Tests unitaires de tous les modules
- Tests d'intégration du moteur narratif
- Tests de déterminisme et de reproductibilité

## Organisation (Phase 1)

### Tests unitaires par module

- `Narratum.Core.Tests` - Tests des abstractions
- `Narratum.Domain.Tests` - Tests des entités
- `Narratum.State.Tests` - Tests de gestion d'état
- `Narratum.Rules.Tests` - Tests du moteur de règles
- `Narratum.Simulation.Tests` - Tests de progression
- `Narratum.Persistence.Tests` - Tests de persistance

### Tests d'intégration

- Tests cross-modules
- Scénarios narratifs complets
- Tests de bout en bout (sans UI)

### Tests de déterminisme

- Reproductibilité exacte
- Même séquence → même résultat
- Tests de régression

## Types de tests requis

### 1. Tests de domaine

- Création d'univers
- Création de personnages
- Relations entre entités
- Validation des invariants

### 2. Tests de règles

- Validation des règles individuelles
- Scénarios d'échec (règles violées)
- Règles composites
- Ordre d'exécution

### 3. Tests de progression

- Progression d'arc narratif
- Génération d'événements
- Transitions d'état valides et invalides
- Actions utilisateur

### 4. Tests de persistance

- Sauvegarde / restauration d'état
- Intégrité des données
- Gestion des slots de sauvegarde
- Tests de migration

### 5. Tests de scénarios

- Scénarios narratifs complets (sans texte)
- Cohérence sur 50+ actions
- Reproductibilité déterministe
- Tests de non-régression

## Objectifs de qualité

- **Couverture de code** : > 80%
- **Tous les tests passent** : 100%
- **Scénarios de référence** : Au moins 5 scénarios complets
- **Performance** : Tests rapides (< 5s pour toute la suite)

## Stack de test

- **xUnit** - Framework de test
- **FluentAssertions** - Assertions expressives
- **NSubstitute** - Mocks (si nécessaire, éviter autant que possible)
- **Coverlet** - Couverture de code

## Principes

- Couverture exhaustive des règles métier
- Tests déterministes et reproductibles
- Tests isolés et indépendants
- Pas de dépendances externes dans les tests
- Tests rapides et maintenables

## Exemple de test de scénario

```csharp
[Fact]
public void CompleteNarrativeScenario_ShouldBeReproducible()
{
    // Arrange: Create world, characters, and initial state
    var world = CreateTestWorld();
    var hero = CreateCharacter("Aric");
    var state = InitializeState(world, hero);
    
    // Act: Execute 50 deterministic actions
    var actions = GenerateDeterministicActions(50);
    foreach (var action in actions)
    {
        state = progressionService.Progress(state, action);
    }
    
    // Assert: Verify exact expected state
    state.EventHistory.Should().HaveCount(50);
    state.CurrentChapterId.Should().Be(expectedChapterId);
    state.Characters[hero.Id].Location.Should().Be(expectedLocation);
}
```
