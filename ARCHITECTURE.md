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

---

## Évolution architecturale

### Phase actuelle (Phase 1)

L'architecture actuelle est **volontairement simple** et **sans IA** :
- Core déterministe
- Domain pur
- State immuable
- Rules algorithmiques
- Persistence locale

### Phases futures (2-6)

L'architecture évoluera progressivement pour intégrer :

#### Phase 4-5 : Modules IA
- **Narratum.LLM** : Abstraction des modèles locaux (llama.cpp, Ollama)
- **Narratum.Orchestration** : Pipeline multi-agents
- **Narratum.Memory** : Gestion de contexte et résumés hiérarchiques

#### Phase 6 : Interface
- **Narratum.UI** : Interface utilisateur (Blazor WebView)
- **Narratum.Api** : API REST pour communication UI

#### Agents IA spécialisés (Phase 5)
1. **NarratorAgent** : Génération du texte narratif principal
2. **CharacterAgent** : Dialogues et réactions des personnages
3. **SummaryAgent** : Résumés factuels (température basse)
4. **ConsistencyAgent** : Vérification de cohérence (pas de contradictions)

### Principe fondamental

**L'orchestration reste dans l'application, jamais dans le LLM.**

Les LLMs sont des **moteurs de génération**, pas le cerveau métier :
- La logique reste dans Core/Domain
- Les agents IA sont des adaptateurs
- Le déterminisme est garanti par l'orchestrateur
- Les prompts sont générés dynamiquement par l'application

### Architecture cible

```
UI (Phase 6)
  ↓
API REST (Phase 6)
  ↓
Orchestrateur (Phase 3-5)
  ↓
┌─────────────────────────────┐
│ Agents IA (Phase 4-5)       │
│ - NarratorAgent             │
│ - CharacterAgent            │
│ - SummaryAgent              │
│ - ConsistencyAgent          │
└─────────────────────────────┘
  ↓
┌─────────────────────────────┐
│ Core & Domain (Phase 1-2)   │
│ - StoryWorld                │
│ - Characters                │
│ - Rules Engine              │
│ - State Management          │
└─────────────────────────────┘
  ↓
Persistence (Phase 1)
```

### Migration garantie

Chaque phase est conçue pour :
- Ne pas casser les phases précédentes
- Ajouter des fonctionnalités sans modifier le core
- Maintenir le déterminisme
- Permettre de désactiver les modules avancés (mode "LLM OFF")

Consultez [ROADMAP.md](Docs/ROADMAP.md) pour le plan complet.
