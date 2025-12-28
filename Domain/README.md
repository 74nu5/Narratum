# Domain

Ce dossier contient la logique métier du moteur narratif déterministe.

## Responsabilités

- Modéliser les concepts du domaine narratif
- Implémenter les règles métier pures
- Garantir le déterminisme des opérations

## Entités principales (Phase 1)

### Univers narratif
- **StoryWorld** - Représente un univers narratif cohérent
- **StoryArc** - Arc narratif structurant
- **StoryChapter** - Unité de progression atomique

### Personnages et lieux
- **Character** - Entité persistante avec traits fixes
- **Location** - Lieu dans l'univers narratif

### Événements
- **Event** - Fait narratif immuable et canonique
- Types d'événements : rencontre, mort, révélation, déplacement

### Relations
- **Relationship** - Relations entre personnages
- Règle : les relations sont bidirectionnelles

## Règles du domaine

- Un personnage mort ne peut pas être ressuscité
- Les traits fixes d'un personnage ne changent jamais
- Un événement ne disparaît jamais (immuable)
- Le temps narratif est monotone (ne recule jamais)

## Principes

- Logique métier pure sans dépendances externes
- Déterminisme complet
- Invariants du domaine strictement appliqués
- Entités immuables ou à mutation contrôlée
