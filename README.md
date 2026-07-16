# Narratum

Moteur narratif déterministe développé en .NET 10, évoluant vers un système complet de génération d'histoires interactives.

## Description

Narratum est un moteur narratif conçu selon les principes d'architecture hexagonale, garantissant un comportement déterministe et reproductible. Le projet suit une approche **anti-bidouille** par phases strictes, construisant des fondations solides avant d'ajouter l'IA.

## Statut actuel

📍 **PHASES 1-4 COMPLÈTES | PHASES 5-6 EN COURS**

> **Projet en Production** : Moteur narratif déterministe + IA intégrée + Interface Web fonctionnelle

## Caractéristiques Actuelles

### ✅ Moteur Narratif (Phases 1-2)
- **Déterminisme** : Toutes les opérations sont reproductibles avec les mêmes entrées
- **Architecture hexagonale** : Séparation claire entre logique métier et infrastructure
- **Mémoire hiérarchique** : 4 niveaux (Event, Chapter, Arc, World)
- **Validation de cohérence** : Détection automatique de contradictions

### ✅ Orchestration & IA (Phases 3-4)
- **Pipeline multi-agents** : NarratorAgent, CharacterAgent, SummaryAgent, ConsistencyAgent
- **LLM local intégré** : Foundry Local pour génération 100% locale
- **Prompts localisés** : Support FR/EN
- **Skills system** : Capacités narratives modulaires

### 🔄 Interface Utilisateur (Phase 6 - 70%)
- **Application Web** : ASP.NET Core + Blazor
- **Wizard de création** : Création guidée d'histoires
- **Dashboard** : Gestion et visualisation
- **Persistance complète** : SQLite intégré

## Structure du projet

### Modules Core (Phase 1)
- **Core** : Abstractions et interfaces fondamentales (0 dépendances)
- **Domain** : Logique métier du moteur narratif
- **State** : Gestion de l'état immutable
- **Rules** : Moteur de règles narratives
- **Simulation** : Orchestration de la simulation
- **Persistence** : Sauvegarde et chargement des états (SQLite)
- **Tests** : Tests Phase 1 (110 tests)

### Modules Avancés (Phases 2-6)
- **Memory** : Système de mémoire hiérarchique (Phase 2)
- **Orchestration** : Pipeline multi-agents (Phase 3)
- **Llm** : Abstraction LLM locale (Phase 4)
- **Web** : Application Web Blazor (Phase 6)
- **Playground** : Application console de démonstration

### Tests
- **Memory.Tests** : 171 tests Phase 2
- **Orchestration.Tests** : Tests Phase 3
- **Llm.Tests** : Tests Phase 4
- **E2E Tests** : Playwright pour tests end-to-end

### Documentation
- **Docs/** : Documentation complète par phase
- **STATUS-CURRENT.md** : État actuel détaillé

## Prérequis

- .NET 10 SDK

## État d'avancement

| Phase | Statut | Modules | Tests | Documentation |
|-------|--------|---------|-------|---------------|
| **Phase 1** - Fondations | ✅ 100% | 7 modules | 110 tests | ✅ Complète |
| **Phase 2** - Mémoire | ✅ 100% | 2 modules | 171 tests | ✅ Complète |
| **Phase 3** - Orchestration | ✅ 100% | 2 modules | ~50 tests | 📝 À finaliser |
| **Phase 4** - LLM | ✅ 100% | 2 modules | ~30 tests | 📝 À finaliser |
| **Phase 5** - Narration | 🔄 90% | Intégré | E2E tests | 📝 À créer |
| **Phase 6** - Web UI | 🔄 70% | 1 module | Playwright | 📝 À créer |

### Fonctionnalités Disponibles MAINTENANT

✅ Moteur narratif déterministe complet  
✅ Agents IA spécialisés (4 agents)  
✅ Mémoire narrative hiérarchique  
✅ Orchestration multi-agents  
✅ Interface utilisateur Web  
✅ Génération narrative locale (Foundry Local)  
✅ Fonctionnement 100% local  

**Les fondations sont solides. L'IA est intégrée. L'interface fonctionne.**

## Documentation

- **[ROADMAP.md](Docs/ROADMAP.md)** - Plan complet des 6 phases
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Architecture hexagonale et principes
- **[Phase1.md](Docs/Phase1.md)** - Détails de la phase actuelle
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Guide de contribution

## Philosophie de développement

✔️ Architecture propre avant fonctionnalités
✔️ Tests > démo
✔️ Déterminisme garanti
✔️ Pas de dette technique
✔️ Projet qui va au bout

> "Retarder volontairement le plaisir du résultat visible pour construire quelque chose qui dure."