# Core

Ce dossier contient les abstractions et interfaces fondamentales du moteur narratif.

## Responsabilités

- Définir les contrats et interfaces de base du système
- Fournir les types et abstractions partagés par tous les modules
- Établir les fondations architecturales indépendantes de toute implémentation

## Contenu prévu (Phase 1)

### Interfaces principales
- `IStoryRule` - Contrat pour les règles narratives
- `IRepository<T>` - Abstraction pour la persistance
- `IStateTransitionService` - Service de transition d'état
- `IProgressionService` - Service de progression narrative

### Types de base
- `Result<T>` - Type résultat pour la gestion d'erreurs
- `DomainEvent` - Base pour les événements de domaine
- Enums et constantes globales

## Principes

- Aucune dépendance externe
- Uniquement des abstractions et interfaces
- Pas de logique métier concrète
- Pas de dépendance vers d'autres modules du projet
