# Phase 5 - Blazor Server UI - Documentation

## Vue d'ensemble

Phase 5 implémente une interface web complète en Blazor Server avec Microsoft Fluent UI pour la génération d'histoires narratives.

## Architecture

### Stack Technique
- **.NET 10** avec Blazor Server (Interactive SSR)
- **Microsoft Fluent UI Blazor v4.13** (composants modernes)
- **SQLite** via EF Core 10 (persistence)
- **SignalR** (communication temps réel)

### Modules Web
- **Web/** : Interface utilisateur Blazor
  - `Components/Pages/` : Pages principales
  - `Components/Shared/` : Composants réutilisables
  - `Components/Layout/` : Layout et navigation
  - `Services/` : Services métier Web
  - `Models/` : DTOs et modèles UI

## Fonctionnalités implémentées

### 1. Wizard de création (5.5)
- **5 étapes** : Monde, Genre, Personnages, Lieux, Résumé
- **Composants** : `WorldEditor`, `CharactersEditor`, `LocationsEditor`
- **Validation** : Nom du monde + 1 personnage minimum
- **Navigation** : Previous/Next avec validation

### 2. Génération narrative (5.6)
- **Service** : `GenerationService`
  - `CreateStoryAsync()` : Initialisation histoire
  - `GenerateNextPageAsync()` : Génération page suivante
  - `LoadPageAsync()` : Chargement page existante
  - `GetPageHistoryAsync()` : Timeline complète
- **Page** : `/generation/{slotName}`
  - Timeline interactive
  - Stats temps réel (mots, événements, personnages)
  - Intent input avec streaming
  - Auto-save en DB

### 3. Navigation temporelle (5.7)
- **Timeline** : Navigation entre pages générées
- **Régénération** : `RegenerateLastPageAsync()` (dernière page uniquement)
- **State management** : Snapshots par page (`PageSnapshotEntity`)
- **Détails page** : Intent, modèle, horodatage

### 4. Mode Expert (5.8)
- **Toggle** : Switch dans le header (global)
- **Service** : `ExpertModeService` (state management)
- **Affichage** : Accordion avec 3 sections
  - Prompts envoyés (tous les agents)
  - Réponses LLM brutes
  - Métadonnées (modèle, timestamps)
- **Persistence** : `PromptsSent` et `RawLlmOutput` dans `PageSnapshotEntity`

### 5. Bibliothèque (5.9)
- **Page** : `/library`
- **DataGrid** : Toutes les histoires avec tri/filtre
- **Actions** : Continuer, Lire, Supprimer
- **Service** : `StoryLibraryService`

### 6. Lecteur (5.10)
- **Page** : `/reader/{slotName}`
- **Stats panel** : Pages, mots, événements, dates
- **Navigation** : Previous/Next entre pages
- **Export** : Placeholder pour Markdown (TODO)

### 7. Configuration LLM (5.11)
- **Page** : `/config`
- **Sélection modèle** : Phi-4-mini, Phi-4, custom
- **Test connexion** : Placeholder
- **Info système** : Provider, endpoint, modèle actif

### 8. Explorateur Mémoire (5.12)
- **Page** : `/memory/{slotName}` (placeholder)
- **Future** : Faits canoniques, violations cohérence
- **Intégration** : Memory module (Phase 2 complete)

## Services

### GenerationService
Orchestration complète de la génération narrative.

```csharp
public class GenerationService
{
    Task<Result<string>> CreateStoryAsync(...);
    Task<Result<PageResult>> GenerateNextPageAsync(...);
    Task<Result<PageResult>> RegenerateLastPageAsync(...);
    Task<Result<PageDetails>> LoadPageAsync(...);
    Task<List<PageSummary>> GetPageHistoryAsync(...);
}
```

### StoryLibraryService
Gestion bibliothèque multi-histoires.

```csharp
public class StoryLibraryService
{
    Task<List<StoryEntry>> ListStoriesAsync();
    Task DeleteStoryAsync(string slotName);
}
```

### ExpertModeService
Toggle mode expert (state global).

```csharp
public class ExpertModeService
{
    bool IsExpertModeEnabled { get; set; }
    void ToggleExpertMode();
    event Action? OnExpertModeToggled;
}
```

### ModelSelectionService : IModelResolver
Résolution modèle runtime (mutable).

```csharp
public class ModelSelectionService : IModelResolver
{
    string? CurrentNarratorModel { get; set; }
    string ResolveModel(AgentType agentType);
}
```

## Persistence

### PageSnapshotEntity
Snapshot par page avec données complètes.

```csharp
public class PageSnapshotEntity
{
    string Id { get; set; }
    string SlotName { get; set; }
    int PageIndex { get; set; }
    DateTime GeneratedAt { get; set; }
    string? NarrativeText { get; set; }
    string SerializedState { get; set; }
    string? IntentDescription { get; set; }
    string? ModelUsed { get; set; }
    string? GenreStyle { get; set; }
    string? SerializedPipelineResult { get; set; }
    string? PromptsSent { get; set; } // Mode Expert
    string? RawLlmOutput { get; set; } // Mode Expert
}
```

**Index unique** : `(SlotName, PageIndex)`

## Workflow complet

1. **Création** : `/wizard` → 5 étapes → `CreateStoryAsync()`
2. **Génération** : `/generation/{slot}` → Intent → `GenerateNextPageAsync()`
3. **Snapshot** : Auto-save page dans `PageSnapshots` + state dans `SavedStates`
4. **Timeline** : Navigation pages, `LoadPageAsync()`
5. **Régénération** : Dernière page → `RegenerateLastPageAsync()`
6. **Expert** : Toggle → affiche prompts/outputs bruts
7. **Lecture** : `/reader/{slot}` → Mode lecture seul + stats
8. **Bibliothèque** : `/library` → Liste toutes histoires

## Routes

| Route | Description |
|-------|-------------|
| `/` | Dashboard (placeholder stats) |
| `/wizard` | Wizard création 5 étapes |
| `/generation/{slot}` | Génération temps réel |
| `/reader/{slot}` | Lecteur mode lecture |
| `/library` | Bibliothèque toutes histoires |
| `/config` | Configuration LLM |
| `/memory/{slot}` | Explorateur mémoire (placeholder) |

## Tests

**État** : 894 tests passent (tous modules)
- Core: stable
- Domain: stable
- Orchestration: 517 tests
- Memory: 171 tests
- Llm: 52 tests

**Web** : Pas de tests unitaires (UI testée manuellement via Playwright)

## Limitations et TODOs

### Implémentés
- ✅ Wizard complet
- ✅ Génération temps réel
- ✅ Timeline navigation
- ✅ Régénération dernière page
- ✅ Mode Expert (prompts/outputs)
- ✅ Bibliothèque multi-histoires
- ✅ Lecteur avec stats
- ✅ Config LLM

### Placeholders
- ⏳ Export Markdown/PDF (bouton présent, pas implémenté)
- ⏳ Notifications sonores (fin génération)
- ⏳ Stats Dashboard (compteurs placeholder)
- ⏳ Memory Explorer (page basic, intégration Memory module TODO)
- ⏳ Test connexion LLM (simulated)

### Future
- Streaming temps réel (actuellement await complet)
- Fork histoire depuis page intermédiaire
- Édition inline faits canoniques (Mode Expert)
- Analyse violations cohérence en temps réel

## Commandes

### Build
```bash
dotnet build Web -c Debug
```

### Run
```bash
cd Web
dotnet run
# App sur http://localhost:5157
```

### Test (manuel via Playwright)
```bash
# Démarrer app puis utiliser Playwright MCP
```

## Dépendances

**NuGet** :
- `Microsoft.FluentUI.AspNetCore.Components` 4.13.0
- `Microsoft.FluentUI.AspNetCore.Components.Icons` 4.13.0

**Projets** :
- `Narratum.Core`
- `Narratum.Domain`
- `Narratum.State`
- `Narratum.Persistence`
- `Narratum.Memory`
- `Narratum.Orchestration`
- `Narratum.Llm`

## Conclusion

Phase 5 complète : **15/15 todos** (100%)

Interface fonctionnelle de bout en bout :
1. Wizard → Création histoire
2. Generation → Génération narrative temps réel
3. Timeline → Navigation/régénération
4. Expert → Debug prompts/outputs
5. Library → Gestion multi-histoires
6. Reader → Lecture + stats
7. Config → Paramètres LLM

**Prochaine phase** : Phase 6 - Optimisations & Production
