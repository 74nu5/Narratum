# Playground - Narratum

Application de démonstration interactive pour tester les différentes phases du moteur narratif Narratum.

## 🎮 Utilisation

```bash
cd Playground
dotnet run
```

Le menu principal vous permet de choisir quelle phase tester :

```
NARRATUM
Narrative Engine Playground
Choisissez une phase à tester

? Quelle phase voulez-vous tester ?
  Phase 1 & 2 - Story Walkthrough + Memory System
  Phase 4 - LLM Integration (Foundry Local)
  Quitter
```

## 📋 Phases disponibles

### Phase 1 & 2 - Story Walkthrough + Memory System

**Démonstration complète de la narration déterministe et du système de mémoire.**

**Inclus :**
- Création d'un monde narratif (The Hidden Realm)
- 3 personnages avec traits distincts
- 3 chapitres narratifs avec progression temporelle
- Système de snapshots (sauvegarde d'état)
- Validation de règles narratives
- Extraction de faits depuis les événements
- Validation de cohérence narrative
- Construction d'un état canonique du monde

**Résultat :**
- Histoire complète en 3 actes
- 10 heures de temps narratif
- 4 chapitres de mémoire
- Détection automatique d'incohérences

### Phase 4 - LLM Integration (Foundry Local)

**Test de l'intégration LLM avec Microsoft Foundry Local.**

**Prérequis :**
- Microsoft Foundry Local SDK installé
- Au moins un modèle téléchargé (Phi-4 ou Phi-4-mini recommandés)

**Test effectué :**
- Configuration du client LLM via Dependency Injection
- Génération avec 4 types d'agents :
  - **Narrator** : Génération de récits épiques
  - **Character** : Pensées de personnages
  - **Summary** : Résumés concis
  - **Consistency** : Validation de cohérence

**Fonctionnalités validées :**
- System + User prompts
- Paramètres de génération (température, max tokens)
- Métadonnées de requête
- Architecture IChatClient → ILlmClient

## 🏗️ Architecture technique

**Technologie :**
- .NET 10.0
- Spectre.Console pour l'interface CLI
- Microsoft.Extensions.DependencyInjection
- Architecture hexagonale (Ports & Adapters)

**Structure :**
```
Playground/
├── Program.cs              # Menu principal
├── Phases/
│   ├── Phase1And2Demo.cs   # Demo Phase 1+2
│   └── Phase4FoundryLocalDemo.cs # Demo Phase 4 LLM
├── README-MENU.md          # Cette doc
└── README.md               # Doc originale Phase 1+2
```

## 🧪 Tests manuels

### Scénario 1 : Vérifier la déterminance

Lancez Phase 1 & 2 plusieurs fois. Les snapshots doivent avoir les mêmes hashes d'intégrité.

### Scénario 2 : Tester différents modèles LLM

Dans Phase 4, sélectionnez différents modèles (Phi-4, Phi-4-mini) et comparez :
- Vitesse de génération
- Qualité du contenu
- Cohérence des réponses

### Scénario 3 : Gestion d'erreurs

Lancez Phase 4 sans Foundry Local installé pour vérifier la gestion d'erreur élégante.

## 📝 Ajouter une nouvelle phase de test

Le menu utilise un enum pour type-safety et le pattern converter de Spectre.Console.

1. **Ajouter une valeur à l'enum** dans `Program.cs` :
```csharp
enum MenuChoice
{
    Phase1And2,
    Phase4,
    PhaseX,  // ← Nouvelle phase
    Quit
}
```

2. **Ajouter le texte d'affichage** dans le converter :
```csharp
.UseConverter(choice => choice switch
{
    MenuChoice.Phase1And2 => "Phase 1 & 2 - Story Walkthrough + Memory System",
    MenuChoice.Phase4 => "Phase 4 - LLM Integration (Foundry Local)",
    MenuChoice.PhaseX => "Phase X - Nouvelle fonctionnalité",  // ← Ici
    MenuChoice.Quit => "Quitter",
    _ => choice.ToString()
})
```

3. **Ajouter le case dans le switch** :
```csharp
switch (choice)
{
    case MenuChoice.Phase1And2:
        Phase1And2Demo.Run();
        break;
    case MenuChoice.Phase4:
        await Phase4FoundryLocalDemo.RunAsync();
        break;
    case MenuChoice.PhaseX:  // ← Nouveau case
        PhaseXDemo.Run();
        break;
}
```

4. **Créer le fichier de démo** dans `Phases/`:
```csharp
namespace Narratum.Playground.Phases;

public static class PhaseXDemo
{
    public static void Run()
    {
        // Votre code de test...
    }
}
```

**Avantages de cette approche :**
- ✅ Type-safe : impossible de typo dans les `if/else` avec strings
- ✅ Exhaustivité : le compilateur vérifie que tous les cases sont gérés
- ✅ Refactoring-friendly : renommer facilement les choix
- ✅ Pattern Spectre.Console recommandé : `.UseConverter()` pour l'affichage

## 🐛 Dépannage

**Erreur "NETSDK1047: Le fichier de composants n'a aucune cible"**
→ Lancer `dotnet restore Playground` avant le build.

**Erreur LLM : "Foundry Local non disponible"**
→ Vérifier que le SDK Foundry Local est installé et qu'au moins un modèle est téléchargé.

**L'application se ferme immédiatement**
→ Utiliser `dotnet run` (pas juste double-clic sur l'exe).

## 🔄 Navigation

Après chaque démonstration, l'application vous demande si vous voulez revenir au menu principal :
- `Oui` (défaut) → Retour au menu
- `Non` → Quitter l'application

Le menu utilise une variable de contrôle `shouldContinue` pour gérer la boucle proprement (pas de `while true`).

Pour quitter : 
- Sélectionner "Quitter" dans le menu, ou
- Répondre "Non" à la confirmation de retour au menu
