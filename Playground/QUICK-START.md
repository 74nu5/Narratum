# Playground - Tests Phase 4 : Guide de démarrage rapide

## ✅ Modifications effectuées

Le projet **Playground** a été transformé en une application de test interactive avec menu de navigation pour tester différentes phases de Narratum.

### Fichiers créés
- `Phases/Phase1And2Demo.cs` - Code de la démo Phase 1+2 déplacé depuis Program.cs
- `Phases/Phase4FoundryLocalDemo.cs` - Nouvelle démo pour tester l'intégration LLM
- `README-MENU.md` - Documentation du système de menu
- `CHANGES.md` - Ce fichier
- `QUICK-START.md` - Guide de démarrage

### Fichiers modifiés
- `Program.cs` - Menu principal avec navigation
- `Playground.csproj` - Ajout RuntimeIdentifier + références Orchestration & Llm

## 🚀 Lancer les tests

### Option 1 : Menu interactif (recommandé)

```bash
cd Playground
dotnet run
```

Puis choisir :
1. **Phase 1 & 2** - Story Walkthrough + Memory System
2. **Phase 4** - LLM Integration (Foundry Local)
3. Quitter

### Option 2 : Tests spécifiques

**Tester Phase 1 & 2 uniquement** :
- Lancer le Playground et sélectionner "Phase 1 & 2"
- Vérifier que les hashes de snapshots sont identiques entre runs

**Tester Phase 4 uniquement** :
- Lancer le Playground et sélectionner "Phase 4"
- Choisir le modèle (Phi-4 ou Phi-4-mini)
- Observer la génération pour 4 agents (Narrator, Character, Summary, Consistency)

## 📊 Phase 4 - Ce qui est testé

### Configuration
✅ Dependency Injection avec Foundry Local  
✅ Résolution du service ILlmClient  
✅ Sélection de modèle au runtime  

### Génération LLM
✅ **Narrator** - Génération de récits épiques  
✅ **Character** - Pensées de personnages  
✅ **Summary** - Résumés concis  
✅ **Consistency** - Validation de cohérence  

### Architecture
✅ System + User prompts  
✅ Paramètres de génération (température, max tokens)  
✅ Métadonnées de requête  
✅ Bridge IChatClient → ILlmClient  

## 🔍 Vérifications

### Build
```bash
dotnet build Narratum.sln -c Debug
# Résultat : 0 erreurs, 0 warnings
```

### Tests unitaires
```bash
dotnet test Narratum.sln -c Debug
# Résultat : 894 tests passent (4 projets de test)
```

### Structure
```
Playground/
├── Program.cs              ← Menu principal
├── Phases/
│   ├── Phase1And2Demo.cs   ← Demo Phase 1+2
│   └── Phase4FoundryLocalDemo.cs ← Demo Phase 4 LLM
├── README.md               ← Doc originale
├── README-MENU.md          ← Doc du menu
└── QUICK-START.md          ← Ce fichier
```

## ⚙️ Configuration technique

**Playground.csproj :**
- `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` - Requis par Llm (Foundry Local SDK)
- Références : Core, Domain, State, Persistence, Simulation, Memory, **Orchestration**, **Llm**
- NuGet : Spectre.Console, Microsoft.Extensions.DependencyInjection

## 🐛 Résolution de problèmes

### "Foundry Local non disponible"
→ Installer le SDK Foundry Local et télécharger au moins un modèle (Phi-4 ou Phi-4-mini)

### "NETSDK1047"
→ Lancer `dotnet restore Playground` puis `dotnet build Playground`

### Génération trop lente
→ Utiliser Phi-4-mini plutôt que Phi-4 (plus petit modèle)

### Erreur au runtime
→ Vérifier que tous les projets buildent : `dotnet build Narratum.sln`

## 📌 Points clés

1. **Menu persistant** : Retour automatique au menu après chaque démo
2. **Gestion d'erreurs** : Toutes les exceptions sont capturées et affichées proprement
3. **Navigation intuitive** : Utilisation de Spectre.Console pour un rendu terminal moderne
4. **Architecture propre** : Séparation claire entre menu et phases de test

## 📈 Prochaines étapes

Pour ajouter une nouvelle phase de test :

1. Créer `Phases/PhaseXDemo.cs`
2. Implémenter `public static void Run()` ou `public static async Task RunAsync()`
3. Ajouter le choix dans le menu (`Program.cs`)
4. Gérer le choix avec un `if (choice.StartsWith("Phase X"))`

Voir `README-MENU.md` pour plus de détails.

---

**Statut :** ✅ Prêt pour les tests  
**Build :** ✅ 0 erreurs, 0 warnings  
**Tests :** ✅ 894/894 passent  
**Phase 4 :** ✅ Implémentation complète
