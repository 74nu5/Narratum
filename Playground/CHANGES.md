# Playground - Test de la Phase 4

Le projet Playground a été modifié pour inclure un menu interactif permettant de tester différentes phases de Narratum.

## Fichiers créés/modifiés

- **Program.cs** : Menu principal avec navigation
- **Phases/Phase1And2Demo.cs** : Démonstration Phase 1+2 (existant, déplacé)
- **Phases/Phase4FoundryLocalDemo.cs** : Nouvelle démo Phase 4 LLM
- **Playground.csproj** : Ajout de RuntimeIdentifier win-x64, références Orchestration et Llm
- **README.md** : Documentation complète

## Commandes de test

`ash
# Build
dotnet build Playground -c Debug

# Run
cd Playground
dotnet run
`

## Structure du menu

1. Phase 1 & 2 - Story Walkthrough + Memory System (existant)
2. Phase 4 - LLM Integration (Foundry Local) (nouveau)
3. Quitter

## Phase 4 - Fonctionnalités testées

- Configuration DI avec Foundry Local
- Sélection de modèle (Phi-4 ou Phi-4-mini)
- Génération avec 4 agents (Narrator, Character, Summary, Consistency)
- System + User prompts
- Paramètres de génération (température, max tokens)
- Gestion d'erreurs

## Notes

- Le Playground nécessite maintenant RuntimeIdentifier=win-x64 à cause de la dépendance Llm
- Tous les tests passent, 0 erreurs, 0 warnings
- Architecture propre : Menu → Phases (séparation des responsabilités)
