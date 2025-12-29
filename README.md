# Narratum

Moteur narratif déterministe développé en .NET 10, évoluant vers un système complet de génération d'histoires interactives.

## Description

Narratum est un moteur narratif conçu selon les principes d'architecture hexagonale, garantissant un comportement déterministe et reproductible. Le projet suit une approche **anti-bidouille** par phases strictes, construisant des fondations solides avant d'ajouter l'IA.

## Statut actuel

📍 **PHASE 1 - Fondations (SANS IA)**

> **Principe directeur** : Aucun LLM ne doit écrire une ligne tant que le moteur narratif n'est pas béton.

## Caractéristiques (Phase 1)

- **Déterminisme** : Toutes les opérations sont reproductibles avec les mêmes entrées
- **Architecture hexagonale** : Séparation claire entre logique métier et infrastructure
- **Aucune dépendance LLM** : Moteur purement algorithmique (pour l'instant)
- **Sans interface utilisateur** : Bibliothèque core réutilisable (UI viendra en Phase 6)

## Structure du projet

- **Core** : Abstractions et interfaces fondamentales
- **Domain** : Logique métier du moteur narratif
- **State** : Gestion de l'état du système
- **Rules** : Moteur de règles narratives
- **Simulation** : Orchestration de la simulation
- **Persistence** : Sauvegarde et chargement des états
- **Tests** : Tests unitaires et d'intégration
- **Docs** : Documentation technique

## Prérequis

- .NET 10 SDK

## Vision à long terme

Narratum évoluera pour intégrer :

- Agents IA spécialisés (Narrator, Character, Summary, Consistency)
- Mémoire narrative longue
- Orchestration multi-agents
- Interface utilisateur immersive
- Fonctionnement 100% local (128 Go RAM, GPU AMD RX 6950 XT)

**Mais pas avant que les fondations soient solides.**

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