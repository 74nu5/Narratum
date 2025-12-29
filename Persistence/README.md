# Persistence

Ce dossier gère la persistance et la sérialisation des états et configurations.

## Responsabilités

- Implémenter la sauvegarde et le chargement des états
- Gérer la sérialisation/désérialisation des données
- Fournir les adaptateurs de stockage (fichiers, bases de données)

## Architecture (Phase 1)

### Technologie
- **SQLite** - Base de données locale
- **Entity Framework Core 9.0** ou **Dapper** - ORM
- **Repository Pattern** - Abstraction de la persistance

### Tables principales

- `Worlds` - Univers narratifs
- `Arcs` - Arcs narratifs
- `Chapters` - Chapitres
- `Characters` - Personnages
- `Locations` - Lieux
- `Events` - Événements (immuables)
- `States` - Snapshots d'états
- `SaveSlots` - Sauvegardes utilisateur

### Repositories

- `IWorldRepository` - CRUD des mondes
- `ICharacterRepository` - CRUD des personnages
- `IEventRepository` - Insertion et lecture des événements
- `IStateRepository` - Sauvegarde/restauration des états
- `ISaveSlotRepository` - Gestion des sauvegardes

## Fonctionnalités

### Sauvegarde d'état
- Snapshot complet du `StoryState`
- Slots de sauvegarde multiples
- Métadonnées (timestamp, nom, description)

### Restauration
- Chargement exact de l'état sauvegardé
- Validation de l'intégrité
- Gestion des migrations de schéma

### Historique
- Conservation de tous les événements
- Requêtes sur l'historique
- Rejeu possible (replay)

## Règles

> **La persistance ne contient aucune logique métier.**

- Pas de validation métier dans les repositories
- Pas de transformation de données métier
- Sérialisation fidèle et déterministe

## Principes

- Sérialisation déterministe
- Indépendance du format de stockage
- Intégrité des données persistées
- Transactions pour garantir la cohérence
