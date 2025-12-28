# Narratum

Moteur narratif déterministe développé en .NET 10.

## Description

Narratum est un moteur narratif conçu selon les principes d'architecture hexagonale, garantissant un comportement déterministe et reproductible.

## Caractéristiques

- **Déterminisme** : Toutes les opérations sont reproductibles avec les mêmes entrées
- **Architecture hexagonale** : Séparation claire entre logique métier et infrastructure
- **Aucune dépendance LLM** : Moteur purement algorithmique
- **Sans interface utilisateur** : Bibliothèque core réutilisable

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

## Documentation

Consultez [ARCHITECTURE.md](ARCHITECTURE.md) pour comprendre l'architecture du système.

Consultez [CONTRIBUTING.md](CONTRIBUTING.md) pour contribuer au projet.

Consultez [Docs/Phase1.md](Docs/Phase1.md) pour les détails de la phase 1.