# Architecture

## Vue d'ensemble

Narratum suit une architecture hexagonale (ports et adaptateurs) pour garantir une séparation claire entre la logique métier et les détails d'implémentation.

## Principes architecturaux

### 1. Architecture hexagonale

L'architecture est organisée en couches concentriques :

- **Core (Centre)** : Abstractions et interfaces pures, aucune dépendance
- **Domain** : Logique métier déterministe
- **Ports** : Interfaces définissant les contrats (dans Core et Domain)
- **Adaptateurs** : Implémentations concrètes (Persistence, etc.)

### 2. Déterminisme

Toutes les opérations du moteur sont déterministes :
- Pas de générateurs aléatoires non-seedés
- Pas d'accès à l'horloge système non contrôlé
- Pas d'I/O non déterministe dans la logique métier
- Reproduction exacte des résultats avec les mêmes entrées

### 3. Séparation des préoccupations

Chaque module a une responsabilité claire et unique :

#### Core
Définit les abstractions fondamentales sans dépendances externes.

#### Domain
Contient la logique métier pure du moteur narratif.
- Entités et Value Objects
- Logique de domaine
- Invariants métier

#### State
Gère l'état du système de manière immuable.
- Représentation de l'état
- Transitions d'état
- Snapshots

#### Rules
Implémente le moteur de règles narratives.
- Définition des règles
- Évaluation déterministe
- Application des effets

#### Simulation
Orchestre l'exécution de la simulation.
- Boucle de simulation
- Gestion du temps
- Coordination des modules

#### Persistence
Fournit les adaptateurs de persistance.
- Sérialisation/désérialisation
- Stockage des états
- Chargement des configurations

#### Tests
Valide le comportement du système.
- Tests unitaires par module
- Tests d'intégration
- Tests de déterminisme

## Dépendances

```
Tests → Simulation → Rules → State → Domain → Core
Tests → Persistence → State, Domain
```

Les flèches indiquent les dépendances autorisées. Aucune dépendance circulaire n'est permise.

## Flux de données

1. Les entrées (commandes, événements) entrent via les ports
2. Le module Simulation orchestre le traitement
3. Les Rules évaluent les règles applicables
4. Le State est mis à jour de manière immuable
5. Les résultats peuvent être persistés via Persistence
6. Les sorties sont retournées via les ports

## Garanties

- **Déterminisme** : Même entrée → Même sortie
- **Immutabilité** : Les états ne sont jamais modifiés, uniquement remplacés
- **Testabilité** : Chaque module est testable indépendamment
- **Isolation** : La logique métier est isolée de l'infrastructure
