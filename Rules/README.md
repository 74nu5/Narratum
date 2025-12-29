# Rules

Ce dossier contient le moteur de règles et les définitions de règles narratives.

## Responsabilités

- Implémenter le moteur d'évaluation des règles
- Définir les règles narratives déterministes
- Gérer les conditions et les effets des règles
- Valider les invariants du système

## Architecture (Phase 1)

### Interface IStoryRule

```csharp
public interface IStoryRule
{
    RuleResult Validate(StoryState state, StoryAction action);
}
```

Les règles sont :
- **Composables** - Peuvent être combinées
- **Ordonnées** - Exécutées dans un ordre défini
- **Testables** - Indépendamment vérifiables

### Types de règles

1. **Règles de validation**
   - Vérifient qu'une action est autorisée
   - Exemple : "Un personnage mort ne peut pas agir"

2. **Règles d'invariants**
   - Garantissent la cohérence de l'état
   - Exemple : "Le temps narratif ne recule jamais"

3. **Règles de progression**
   - Déterminent les événements résultants
   - Exemple : "Un déplacement génère un événement CharacterMoved"

## Invariants critiques

- Un personnage mort ne peut pas agir
- Un lieu inexistant ne peut pas être ciblé
- Le temps narratif est monotone
- Un événement ne peut pas être annulé
- Les relations sont symétriques

## Principes

- Évaluation déterministe des règles
- Pas de side-effects imprévisibles
- Règles composables et testables
- Validation avant toute modification d'état
