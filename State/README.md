# State

Ce dossier gère la représentation et la gestion de l'état du système narratif.

## Responsabilités

- Définir les structures d'état du système
- Gérer les transitions d'état de manière déterministe
- Fournir les mécanismes de snapshot et de restauration d'état

## Composants principaux (Phase 1)

### StoryState
Source unique de vérité pour l'état complet de l'histoire.

**Contient :**
- `WorldState` - État du monde
- `CharacterStates` - Collection des états des personnages
- `EventHistory` - Historique complet et immuable des événements
- `CurrentChapterId` - Chapitre narratif actuel
- `NarrativeTime` - Temps narratif actuel

### CharacterState
État d'un personnage à un moment donné.

**Attributs :**
- État vital (vivant, mort, inconnu)
- Localisation actuelle
- Faits connus par le personnage
- Dernier événement impliquant le personnage

### WorldState
État global de l'univers narratif.

**Attributs :**
- Temps narratif
- Arc actif
- Métadonnées de session

## Règles

> **Aucune logique métier hors du StoryState et des Rules.**

- Les états sont la source de vérité
- Toute modification passe par des transitions contrôlées
- L'historique des événements est immuable

## Principes

- Immutabilité des états (ou mutation contrôlée)
- Transitions déterministes
- Traçabilité complète des changements d'état
- Snapshots pour sauvegarde/restauration
