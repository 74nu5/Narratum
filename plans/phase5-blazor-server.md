# Phase 5 — Interface Web Blazor Server

**Statut** : 📋 Planifié
**Prérequis** : Phase 4 (LLM) complète ✅

---

## Objectif

Créer une interface web moderne pour Narratum permettant de :
- Créer et configurer des mondes narratifs (personnages, lieux, relations)
- Lancer la génération de récits via le pipeline d'orchestration 5 étapes
- Visualiser la progression en temps réel (streaming via SignalR)
- **Choisir le modèle LLM** à la volée pendant la génération (par agent ou globalement)
- **Mode Expert** : afficher/modifier les données internes de l'histoire et des pages (state, prompts, events, faits)
- **Navigation temporelle** : revenir en arrière à n'importe quelle page générée et relancer depuis ce point
- **Multi-histoires** : gérer plusieurs histoires en parallèle, sauvegarde automatique en continu, switch rapide
- **Genre / style narratif** : choisir un genre (fantaisie, SF, polar...) qui influence les prompts des agents
- **Export d'histoire** : exporter en Markdown, texte brut, ou PDF
- **Statistiques** : métriques par histoire (mots, personnages, événements, cohérence)
- **Notification** : indicateur visuel/sonore quand la génération LLM est terminée

## Décisions Architecturales

| Décision | Choix | Justification |
|----------|-------|---------------|
| **Rendering** | Blazor Server (Interactive SSR) | Pas d'API, accès direct services via DI, SignalR natif |
| **Composants UI** | Microsoft Fluent UI Blazor v4.13+ | Look Microsoft moderne, officiel, maintenable |
| **Persistance** | SQLite via module Persistence existant | Réutilise l'infrastructure en place |
| **Utilisateurs** | Single-user | Pas de gestion de sessions/auth nécessaire |
| **Langue** | Français uniquement | Interface et histoires en français |
| **Thème** | Dark mode par défaut + toggle clair | Préférence utilisateur |
| **Responsive** | Desktop-first, responsive basique | Focus desktop, lisible sur tablette |
| **API** | Aucune | Blazor Server = accès direct aux services .NET |

---

## Stack Technique

### Packages NuGet

| Package | Usage |
|---------|-------|
| `Microsoft.FluentUI.AspNetCore.Components` v4.13+ | Composants UI Fluent |
| `Microsoft.FluentUI.AspNetCore.Components.Icons` | Icônes Fluent |
| `Microsoft.AspNetCore.Components.Web` | Blazor Server core |

### Configuration Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Blazor Server + Interactive SSR
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Fluent UI
builder.Services.AddFluentUIComponents();

// Services Narratum (réutilisation directe)
builder.Services.AddNarratumFoundryLocal(defaultModel: "phi-4-mini");
// + Persistence, Memory, Orchestration services

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

---

## Architecture du Projet

```
Narratum.Web/
├── Narratum.Web.csproj
├── Program.cs                          # Entry point + DI
├── appsettings.json                    # Config (DB path, LLM, thème)
├── Components/
│   ├── App.razor                       # Root component
│   ├── Routes.razor                    # Router
│   ├── Layout/
│   │   ├── MainLayout.razor            # Layout principal (sidebar + content)
│   │   ├── NavMenu.razor               # Navigation latérale
│   │   ├── ThemeToggle.razor           # Toggle dark/light
│   │   ├── ExpertModeToggle.razor      # Toggle Mode Expert on/off
│   │   ├── ActiveStoryIndicator.razor  # Indicateur histoire active + switch rapide
│   │   ├── ModelSelector.razor         # Sélecteur modèle LLM (header)
│   │   └── MainLayout.razor.css        # CSS isolation
│   ├── Pages/
│   │   ├── Home.razor                  # Dashboard d'accueil (multi-histoires)
│   │   ├── Story/
│   │   │   ├── Create.razor            # Wizard création d'histoire
│   │   │   ├── Generate.razor          # Génération narrative en cours
│   │   │   ├── Read.razor              # Lecteur d'histoire
│   │   │   └── Library.razor           # Bibliothèque des histoires
│   │   ├── Memory/
│   │   │   └── FactsExplorer.razor     # Visualisation mémoire/faits
│   │   └── Settings/
│   │       └── LlmConfig.razor         # Configuration LLM
│   └── Shared/
│       ├── StoryCard.razor             # Card résumé d'une histoire
│       ├── CharacterCard.razor         # Card personnage
│       ├── LocationCard.razor          # Card lieu
│       ├── WorldEditor.razor           # Éditeur de monde (sous-composant wizard)
│       ├── CharacterEditor.razor       # Éditeur de personnages (sous-composant wizard)
│       ├── LocationEditor.razor        # Éditeur de lieux (sous-composant wizard)
│       ├── PipelineProgress.razor      # Progression pipeline 5 étapes
│       ├── NarrativeTextRenderer.razor # Rendu du texte narratif
│       ├── EventTimeline.razor         # Timeline des événements
│       ├── PageTimeline.razor          # Timeline des pages (navigation temporelle)
│       ├── GenerationNotifier.razor    # Notification fin de génération (visuel + son)
│       ├── StoryStatsBadge.razor       # Badge statistiques compactes
│       ├── StoryStatsPanel.razor       # Panneau statistiques détaillées
│       ├── ExportDialog.razor          # Dialog export (format, options)
│       ├── ErrorDisplay.razor          # Affichage d'erreurs Result<T>
│       └── Expert/
│           ├── StateInspector.razor    # Visualisation/édition StoryState
│           ├── PipelineDebugPanel.razor# Détails pipeline (prompts, outputs bruts)
│           ├── EventDetailView.razor   # Détail événements (metadata, acteurs)
│           ├── CharacterStateView.razor# État complet personnages
│           └── RawOutputViewer.razor   # Output LLM brut (avant/après validation)
├── Services/
│   ├── StorySessionService.cs          # État de la session en cours (multi-histoires)
│   ├── WorldBuilderService.cs          # Orchestration création de monde
│   ├── NarrativeGenerationService.cs   # Bridge UI ↔ FullOrchestrationService
│   ├── StoryLibraryService.cs          # Gestion bibliothèque (Persistence)
│   ├── StoryTimelineService.cs         # Navigation temporelle (page snapshots)
│   ├── ExpertModeService.cs            # Gestion mode expert + édition state
│   ├── ModelSelectionService.cs        # Sélection modèle LLM à la volée
│   ├── StoryExportService.cs           # Export histoire (Markdown, texte brut, PDF)
│   ├── StoryStatisticsService.cs       # Statistiques (mots, personnages, événements)
│   ├── GenerationNotificationService.cs# Notification fin de génération
│   └── ThemeService.cs                 # Gestion thème dark/light
├── Models/
│   ├── StoryCreationModel.cs           # ViewModel wizard création (inclut GenreStyle)
│   ├── WorldSetupModel.cs              # ViewModel éditeur monde
│   ├── CharacterFormModel.cs           # ViewModel formulaire personnage
│   ├── LocationFormModel.cs            # ViewModel formulaire lieu
│   ├── LlmConfigModel.cs              # ViewModel config LLM
│   ├── GenerationState.cs             # État génération en cours
│   ├── PageSnapshot.cs                # Snapshot d'une page (pour navigation temporelle)
│   └── ExpertViewModels.cs            # ViewModels mode expert
└── wwwroot/
    ├── css/
    │   └── app.css                     # Styles globaux + dark mode overrides
    └── favicon.ico
```

---

## Couche Services (Bridge UI ↔ Domain)

### StorySessionService

Maintient l'état de la session de narration. **Scoped** (1 par circuit SignalR). Gère **plusieurs histoires** en parallèle avec auto-save.

```csharp
public class StorySessionService
{
    // Histoire active
    string? ActiveSlotName { get; }
    StoryState? CurrentState { get; }
    StoryWorld? CurrentWorld { get; }
    IReadOnlyList<Character> Characters { get; }
    IReadOnlyList<Location> Locations { get; }
    
    // Multi-histoires
    IReadOnlyList<StoryEntry> ActiveStories { get; }  // Toutes les histoires en DB
    
    // Navigation temporelle
    int CurrentPageIndex { get; }                       // Page courante (0-based)
    int TotalPages { get; }                             // Nombre total de pages
    IReadOnlyList<PageSnapshot> PageHistory { get; }    // Toutes les pages
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    
    // Workflow
    bool CanGenerate { get; }           // Monde + personnages + lieux configurés
    bool IsGenerating { get; }
    GenerationState? LastGeneration { get; }
    
    // Événements pour rafraîchir l'UI
    event Action? OnStateChanged;
    event Action<string>? OnStoryLoaded;    // Quand une histoire est chargée
    event Action<int>? OnPageChanged;       // Quand on navigue dans les pages
    
    // Gestion multi-histoires
    Task LoadStoryAsync(string slotName);               // Charger une histoire existante
    Task<string> CreateNewStoryAsync(string displayName);// Crée un slot, retourne slotName
    Task SwitchToStoryAsync(string slotName);           // Switch rapide entre histoires
    Task RefreshStoriesListAsync();                     // Rafraîchir la liste
    
    // Création monde
    void InitializeWorld(string name, string description);
    void AddCharacter(Character character);
    void AddLocation(Location location);
    void SetRelationship(Id char1, Id char2, Relationship rel);
    void ApplyStoryAction(StoryAction action);
    
    // Navigation temporelle
    Task GoToPageAsync(int pageIndex);      // Charger le state à la page N
    Task GoBackAsync();                     // Page précédente
    Task GoForwardAsync();                  // Page suivante (si existe)
    Task ForkFromCurrentPageAsync();        // Créer un embranchement depuis la page courante
    
    // Auto-save (appelé après chaque génération)
    Task AutoSaveAsync();
    
    // Expert mode: édition directe du state
    void ReplaceState(StoryState newState);  // ⚠️ Mode Expert uniquement
    
    void Reset();
}
```

### NarrativeGenerationService

Bridge entre l'UI et `FullOrchestrationService`. Gère le streaming des étapes et la sélection de modèle.

```csharp
public class NarrativeGenerationService
{
    // Génération avec progression
    Task<Result<FullPipelineResult>> GenerateAsync(
        StoryState state,
        NarrativeIntent intent,
        IProgress<PipelineStageProgress> progress,    // Progression 5 étapes
        CancellationToken ct);
    
    // Régénération avec instructions modifiées
    Task<Result<FullPipelineResult>> RegenerateLastAsync(
        StoryState state,
        string additionalInstructions,
        IProgress<PipelineStageProgress> progress,
        CancellationToken ct);
    
    // Santé LLM
    Task<bool> IsLlmHealthyAsync(CancellationToken ct);
    string CurrentProvider { get; }
    string CurrentModel { get; }
}

public record PipelineStageProgress(
    string StageName,       // "ContextBuilder", "PromptBuilder", etc.
    int StageIndex,         // 0-4
    int TotalStages,        // 5
    bool IsComplete,
    TimeSpan Elapsed,
    string? StatusMessage
);
```

### ModelSelectionService

Gère la sélection du modèle LLM à la volée. **⚠️ `LlmClientConfig` est un `sealed record` singleton (immutable).** La sélection de modèle passe par un wrapper mutable `IModelResolver` que l'adaptateur LLM interroge à chaque appel, PAS par une mutation de la config.

```csharp
/// <summary>
/// Service mutable (Scoped) qui résout le modèle courant à chaque appel.
/// Consulté par ChatClientLlmAdapter au lieu de LlmClientConfig directement.
/// </summary>
public class ModelSelectionService : IModelResolver
{
    // Modèle courant
    string CurrentDefaultModel { get; }
    string? CurrentNarratorModel { get; }
    IReadOnlyDictionary<AgentType, string> AgentModelMapping { get; }
    
    // Modèles disponibles
    Task<IReadOnlyList<string>> GetAvailableModelsAsync();  // Liste depuis le provider
    
    // Changement à la volée
    void SetDefaultModel(string modelName);
    void SetNarratorModel(string? modelName);
    void SetAgentModel(AgentType agent, string modelName);
    void ClearAgentModel(AgentType agent);  // Retour au default
    
    // Événement
    event Action? OnModelChanged;
}
```

### StoryTimelineService

Gère la navigation temporelle. Chaque génération crée un **PageSnapshot** sauvegardé en DB. L'utilisateur peut naviguer librement entre les pages.

```csharp
public class StoryTimelineService
{
    // Snapshots
    Task<IReadOnlyList<PageSnapshot>> GetPageHistoryAsync(string slotName);
    Task SavePageSnapshotAsync(string slotName, PageSnapshot snapshot);
    Task<StoryState> LoadStateAtPageAsync(string slotName, int pageIndex);
    
    // Branching : quand on revient en arrière et qu'on régénère
    Task TruncateAfterPageAsync(string slotName, int pageIndex);  // Supprime les pages après pageIndex
    
    // Cleanup
    Task DeleteAllSnapshotsAsync(string slotName);
}

public record PageSnapshot(
    int PageIndex,                  // Numéro de page (0 = état initial)
    string SlotName,                // Histoire parente
    DateTime GeneratedAt,           // Timestamp génération
    string? NarrativeText,          // Texte généré (null pour page 0 = état initial)
    string SerializedState,         // StoryState sérialisé (JSON)
    string? IntentDescription,      // Ce que l'utilisateur a demandé
    string? ModelUsed,              // Modèle LLM utilisé
    string? GenreStyle,             // Genre narratif (fantaisie, SF, polar...)
    // Mode Expert : données internes
    string? SerializedPipelineResult, // FullPipelineResult JSON (mode expert)
    string? PromptsSent,            // Prompts envoyés aux agents (mode expert)
    string? RawLlmOutput            // Output brut LLM avant validation (mode expert)
);
```

### ExpertModeService

Contrôle le mode expert. **Scoped** — un toggle par session.

```csharp
public class ExpertModeService
{
    bool IsExpertMode { get; }
    void Toggle();
    void Enable();
    void Disable();
    
    event Action<bool>? OnModeChanged;
    
    // Édition du state (Expert only) — Func car CharacterState est un record immutable
    Result<StoryState> ModifyCharacterState(StoryState state, Id characterId, Func<CharacterState, CharacterState> modifier);
    Result<StoryState> ModifyWorldState(StoryState state, Func<WorldState, WorldState> modifier);
    Result<StoryState> AddEvent(StoryState state, Event newEvent);
    Result<StoryState> RemoveLastEvent(StoryState state);
}
```

### StoryLibraryService

Encapsule `IPersistenceService` avec des méthodes UI-friendly. Gère le multi-histoires.

```csharp
public class StoryLibraryService
{
    Task<IReadOnlyList<StoryEntry>> GetAllStoriesAsync();
    Task<StoryState?> LoadStoryAsync(string slotName);
    Task<bool> SaveStoryAsync(string slotName, StoryState state, string? displayName = null);
    Task<bool> DeleteStoryAsync(string slotName);         // + supprime tous les PageSnapshots
    Task<bool> ExistsAsync(string slotName);
    Task<string> DuplicateStoryAsync(string sourceSlot, string newDisplayName);  // Fork complet
}

public record StoryEntry(
    string SlotName,
    string DisplayName,
    DateTime SavedAt,
    int TotalEvents,
    string? GenreStyle,             // Genre narratif choisi
    int PageCount,                  // Nombre de pages générées
    int TotalWordCount,             // Nombre total de mots générés
    string? LastModelUsed           // Dernier modèle LLM utilisé
);
```

### StoryExportService

Gère l'export de l'histoire dans différents formats.

```csharp
public class StoryExportService
{
    /// <summary>
    /// Exporte l'histoire complète en Markdown.
    /// </summary>
    Task<string> ExportAsMarkdownAsync(string slotName);
    
    /// <summary>
    /// Exporte l'histoire complète en texte brut.
    /// </summary>
    Task<string> ExportAsPlainTextAsync(string slotName);
    
    /// <summary>
    /// Exporte l'histoire en PDF (génération côté serveur).
    /// </summary>
    Task<byte[]> ExportAsPdfAsync(string slotName);
    
    /// <summary>
    /// Retourne le nom de fichier suggéré.
    /// </summary>
    string GetSuggestedFileName(string displayName, string format);
}
```

### StoryStatisticsService

Calcule les statistiques d'une histoire à partir des PageSnapshots.

```csharp
public class StoryStatisticsService
{
    Task<StoryStats> GetStatsAsync(string slotName);
}

public record StoryStats(
    int TotalPages,
    int TotalWords,
    int TotalEvents,
    int UniqueCharactersCount,
    int UniqueLocationsCount,
    IReadOnlyDictionary<string, int> CharacterAppearances,  // nom → nombre de pages
    IReadOnlyDictionary<string, int> EventTypeDistribution,  // type → count
    int CoherenceViolationCount,
    double AverageWordsPerPage,
    string? GenreStyle,
    IReadOnlyList<string> ModelsUsed                        // modèles distincts utilisés
);
```

### GenerationNotificationService

Gère les notifications de fin de génération. **Scoped** (1 par circuit).

```csharp
public class GenerationNotificationService
{
    bool NotificationsEnabled { get; }
    void Enable();
    void Disable();
    void Toggle();
    
    /// <summary>
    /// Déclenché quand la génération est terminée (succès ou échec).
    /// L'UI joue un son et/ou affiche un toast.
    /// </summary>
    event Action<GenerationNotification>? OnGenerationComplete;
}

public record GenerationNotification(
    bool IsSuccess,
    string? NarrativePreview,   // Premiers 100 chars du texte généré
    TimeSpan Duration,
    string ModelUsed
);
```

---

## Pages — Design Détaillé

### 1. Page d'accueil (`/`) — Dashboard Multi-Histoires

**Layout** : Cards en grille montrant **toutes les histoires actives** + actions rapides.

| Section | Composants Fluent UI | Données |
|---------|---------------------|---------|
| Header | `FluentLabel` H1 + description | "Narratum — Moteur Narratif" |
| Actions rapides | `FluentButton` (Accent) | "Nouvelle histoire" |
| **Mes histoires** | `FluentCard` × N avec bouton "Continuer" | Via StoryLibraryService — **toutes** les histoires en DB |
| Statut LLM | `FluentBadge` + `FluentIcon` + **modèle actif** | Santé du provider, modèle sélectionné |
| Statistiques | `FluentCounterBadge` | Total histoires, événements, mots générés |

Chaque `StoryCard` affiche : nom, **genre**, date dernière modif, nombre de pages, nombre de mots, dernier modèle utilisé, boutons "Continuer" / "Lire" / "Exporter" / "Supprimer".

### 2. Création d'histoire (`/story/create`)

**Wizard multi-étapes** avec `FluentWizard` :

| Étape | Contenu | Validation |
|-------|---------|------------|
| 1. Monde | Nom, description, paramètres | Nom requis, description ≥ 20 chars |
| 2. Genre / Style | **Genre narratif** (fantaisie, SF, polar, horreur, historique, libre) + style (Descriptive, Action, Introspective, Dialogue) | Genre requis |
| 3. Personnages | Liste + formulaire ajout (nom, traits) | Min 1 personnage |
| 4. Lieux | Liste + formulaire ajout (nom, description, connections) | Min 1 lieu |
| 5. Relations | Matrice relations entre personnages | Optionnel |
| 6. **Modèle LLM** | **Sélection du modèle narrateur**, provider, modèle par agent | **Modèle sélectionnable** avec liste des modèles disponibles |
| 7. Résumé | Récapitulatif + bouton "Commencer" | Tout validé |

**Composants Fluent UI** : `FluentWizard`, `FluentTextField`, `FluentTextArea`, `FluentSelect`, `FluentSlider`, `FluentDataGrid`, `FluentButton`.

À la fin du wizard : **auto-save immédiat** en DB (crée un slot) + redirection vers la page de génération.

### 3. Génération narrative (`/story/generate/{slotName}`)

**Vue principale** — le cœur de l'application.

```
┌──────────────────────────────────────────────────────────────────┐
│  📖 Mon Histoire  │  Modèle: [Phi-4 ▾]  │  Page 3/5  │ ← → │  │  ← Header page
├──────────┬───────────────────────────────────┬───────────────────┤
│          │                                   │                   │
│ Timeline │      Texte narratif               │  Panneau droit    │
│ pages    │      (généré / en cours)          │  - Personnages    │
│          │                                   │  - Lieux          │
│ [Page 0] │  "Le crépuscule tombait sur       │  - Événements     │
│ [Page 1] │   la forêt enchantée..."          │                   │
│ [Page 2] │                                   │  ──────────       │
│▸[Page 3] │  ┌──Pipeline Progress──┐          │  Mode Expert ▾   │
│          │  │ ■■■□□ AgentExec    │          │  (si activé)      │
│          │  └────────────────────┘          │  - State JSON     │
│          │                                   │  - Prompts        │
│          │  [Continuer] [Dialoguer]          │  - Output brut    │
│          │  [Décrire]   [Résumer]            │                   │
│          │  [🔄 Régénérer] (dernière page)   │  ──────────       │
│          │  [📊 Stats] [📥 Exporter]         │  Stats: 1234 mots │
│          │                                   │                   │
├──────────┴───────────────────────────────────┴───────────────────┤
│  Auto-saved ✓  │  LLM: Phi-4 OK  │  Events: 12  │  🔔 Notif ON     │  ← Footer
└──────────────────────────────────────────────────────────────────┘
```

| Zone | Composants | Comportement |
|------|-----------|-------------|
| **Sélecteur de modèle** | `FluentSelect` dans le header de page | Change le modèle à la volée via `ModelSelectionService` |
| **Timeline des pages** | `PageTimeline.razor` (sidebar gauche) | Liste cliquable de toutes les pages, navigation temporelle |
| Pipeline Progress | `FluentProgress` + `FluentTimeline` | 5 étapes avec statut temps réel (SignalR) |
| Texte narratif | `FluentCard` + rendu Markdown | StreamRendering, ajout progressif |
| Actions | `FluentButton` : Continuer, Dialoguer, Décrire, Résumer | NarrativeIntent mappé |
| Régénérer | `FluentDialog` : instructions modifiées | **Dernière page uniquement** — appel RegenerateLastAsync, remplace le snapshot |
| Personnages | Sidebar `FluentNavMenu` | État actuel des personnages |
| Événements | `FluentTimeline` | Timeline chronologique |
| **Navigation ←→** | `FluentButton` (header) | GoBack/GoForward dans les pages |
| **Mode Expert** | `FluentAccordion` (panneau droit) | Affiché seulement si ExpertMode activé |

**Navigation temporelle** :
- Cliquer sur une page dans la timeline = charge le state à cette page
- Boutons ←→ dans le header pour navigation séquentielle
- **Régénération uniquement sur la dernière page** — le snapshot est remplacé en place
- Si on navigue en arrière et on clique "Continuer" → **fork** : les pages suivantes sont tronquées, nouvelle page générée depuis ce point
- **Auto-save** : chaque génération réussie crée un `PageSnapshot` en DB

**Sélection de modèle** :
- Le `FluentSelect` dans le header montre les modèles disponibles (listés depuis le provider)
- Changer le modèle s'applique à la prochaine génération
- Le modèle utilisé est enregistré dans chaque `PageSnapshot`

**Streaming** : `@attribute [StreamRendering]` pour afficher le texte au fur et à mesure.
**Temps réel** : `IProgress<PipelineStageProgress>` → `StateHasChanged()`.

### 4. Bibliothèque (`/story/library`)

| Composants | Usage |
|-----------|-------|
| `FluentDataGrid<StoryEntry>` | Liste triable/filtrable de **toutes** les histoires |
| `FluentSearchBox` | Recherche par nom |
| `FluentMenuButton` | Actions : **Continuer**, Lire, **Exporter**, Dupliquer, Supprimer |
| `FluentDialog` | Confirmation suppression |
| `FluentBadge` | Nombre de pages, dernier modèle utilisé |

**Continuer** = charge l'histoire et redirige vers `/story/generate/{slot}` à la dernière page.
**Dupliquer** = crée une copie complète (nouveau slot) avec tous les snapshots.

### 5. Lecteur d'histoire (`/story/read/{slotName}`)

| Zone | Composants |
|------|-----------|
| Navigation chapitres | `FluentNavMenu` ou `FluentBreadcrumb` |
| **Navigation pages** | `PageTimeline.razor` (réutilisé, mode lecture seule) |
| Texte narratif | `FluentCard` avec styles prose |
| Personnages mentionnés | `FluentPersona` chips |
| Faits extraits | `FluentAccordion` sidebar |
| **Statistiques** | `StoryStatsPanel` — mots, personnages, événements, modèles |
| **Export** | `FluentButton` "📥 Exporter" → `ExportDialog` (format, téléchargement) |
| **Mode Expert** | Panneau additionnel si activé (state, prompts, metadata de chaque page) |

### 6. Configuration LLM (`/settings/llm`)

| Composants | Usage |
|-----------|-------|
| `FluentSelect` | Provider (FoundryLocal / Ollama) |
| `FluentTextField` | URL base (Ollama) |
| **`FluentSelect`** | **Modèle par défaut** (liste dynamique depuis provider) |
| **`FluentSelect`** | **Modèle narrateur** (override optionnel) |
| `FluentDataGrid` | Mapping Agent → Modèle (**éditable inline**) |
| `FluentButton` | Test de connexion |
| `FluentButton` | **Lister les modèles disponibles** |
| `FluentMessageBar` | Résultat du test (succès/erreur) |

### 7. Explorateur Mémoire (`/memory/{worldId}`)

| Composants | Usage |
|-----------|-------|
| `FluentTreeView` | Hiérarchie Event → Chapter → Arc → World |
| `FluentDataGrid<Fact>` | Liste des faits canoniques |
| `FluentBadge` | Type de fait (Location, Relationship, etc.) |
| `FluentMessageBar` | Violations de cohérence |
| **Mode Expert** | Édition directe des faits (ajout/suppression) |

---

## Layout Principal

```
┌──────────────────────────────────────────────────────────────────────┐
│  🌙/☀️  │  Narratum  │  📖 Mon Histoire ▾  │  Modèle: [Phi-4 ▾]  │  🔬 Expert  │  ⚙️  │
├──────────┬───────────────────────────────────────────────────────────┤
│          │                                                           │
│ 🏠 Accueil│                                                          │
│ ✨ Nouvelle│              Zone de contenu                            │
│ 📚 Biblio │           (Page active ici)                              │
│ 🧠 Mémoire│                                                          │
│ ⚙️ Config │                                                          │
│          │                                                           │
│          │                                                           │
│          │                                                           │
├──────────┴───────────────────────────────────────────────────────────┤
│  Auto-saved ✓  │  LLM: Phi-4 ✅  │  Stories: 3  │  Page 3/5       │
└──────────────────────────────────────────────────────────────────────┘
```

**Header** : Theme toggle + Titre + **Story switcher** (dropdown des histoires actives) + **Model selector** + **Expert mode toggle** + Settings
**Sidebar** : Navigation principale (Accueil, Nouvelle, Bibliothèque, Mémoire, Config)
**Footer** : Statut auto-save, santé LLM, compteurs, page courante

**Composants** : `FluentLayout`, `FluentNavMenu`, `FluentHeader`, `FluentFooter`, `FluentBodyContent`.

---

## Dépendances du Projet

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Fluent UI Blazor -->
    <PackageReference Include="Microsoft.FluentUI.AspNetCore.Components" Version="4.13.*" />
    <PackageReference Include="Microsoft.FluentUI.AspNetCore.Components.Icons" Version="4.13.*" />

    <!-- Projets Narratum -->
    <ProjectReference Include="..\Core\Narratum.Core.csproj" />
    <ProjectReference Include="..\Domain\Narratum.Domain.csproj" />
    <ProjectReference Include="..\State\Narratum.State.csproj" />
    <ProjectReference Include="..\Rules\Narratum.Rules.csproj" />
    <ProjectReference Include="..\Simulation\Narratum.Simulation.csproj" />
    <ProjectReference Include="..\Persistence\Narratum.Persistence.csproj" />
    <ProjectReference Include="..\Memory\Narratum.Memory.csproj" />
    <ProjectReference Include="..\Orchestration\Narratum.Orchestration.csproj" />
    <ProjectReference Include="..\Llm\Narratum.Llm.csproj" />
  </ItemGroup>
</Project>
```

---

## Todos — Plan d'Implémentation

### 5.0 — Évolution Persistence (prérequis navigation temporelle + fix désérialisation)
- **FIX CRITIQUE** : Implémenter les méthodes stub `DeserializeCharacterStates()`, `DeserializeEvents()`, `DeserializeWorldState()` dans `SnapshotService.cs` (actuellement retournent des collections vides → LoadStateAsync ne restaure rien)
- Ajouter une **nouvelle table** `PageSnapshots` au `NarrativumDbContext` pour stocker les snapshots page par page
- Entity `PageSnapshotEntity` : Id, SlotName, PageIndex, GeneratedAt, NarrativeText, SerializedState, IntentDescription, ModelUsed, GenreStyle (nullable), SerializedPipelineResult (nullable), PromptsSent (nullable), RawLlmOutput (nullable)
- Index composite sur (SlotName, PageIndex) — supporte plusieurs pages par histoire
- **⚠️ NE PAS supprimer l'index unique sur SlotName** dans `SaveStateSnapshot` — le mécanisme existant de sauvegarde en dépend. `PageSnapshots` est une table séparée.
- Migration ou recréation du schema
- **Dépendances** : aucune

### 5.1 — Scaffolding du projet Blazor
- Créer `Narratum.Web` via `dotnet new blazor`
- Ajouter au `Narratum.sln`
- Configurer `Program.cs` (Blazor Server + Fluent UI + services Narratum)
- Configurer `appsettings.json` (chemin DB, config LLM, thème)
- Vérifier le build
- **Dépendances** : 5.0

### 5.2 — Layout et navigation
- `MainLayout.razor` avec Fluent UI layout (header, sidebar, content, footer)
- `NavMenu.razor` avec `FluentNavMenu` (Accueil, Nouvelle, Bibliothèque, Mémoire, Config)
- `ThemeToggle.razor` dark/light
- **`ExpertModeToggle.razor`** — toggle Mode Expert dans le header
- **`ActiveStoryIndicator.razor`** — dropdown switch rapide entre histoires actives
- **`ModelSelector.razor`** — sélecteur de modèle LLM dans le header
- `ThemeService.cs` pour persistance du choix
- CSS global (`app.css`) avec variables dark/light
- **Dépendances** : 5.1

### 5.3 — Page d'accueil (Dashboard Multi-Histoires)
- `Home.razor` avec cards d'actions rapides
- `StoryCard.razor` composant réutilisable (**affiche nombre de pages, dernier modèle, bouton "Continuer"**)
- `StoryLibraryService.cs` (bridge vers `IPersistenceService`)
- Affichage de **toutes les histoires** en DB avec actions rapides (Continuer, Lire, Supprimer)
- Stats globales + statut LLM + modèle actif
- **Dépendances** : 5.2

### 5.4 — Services bridge (couche de services UI)
- `StorySessionService.cs` — état de session Scoped, **gestion multi-histoires** (ActiveSlotName, switch, auto-save)
- `NarrativeGenerationService.cs` — bridge vers `FullOrchestrationService`
- `WorldBuilderService.cs` — orchestration création de monde
- **`ModelSelectionService.cs`** — implémente `IModelResolver` (mutable, Scoped), sélection modèle à la volée, liste modèles disponibles. ⚠️ `LlmClientConfig` est un record immutable singleton → le service wraps la config et override le modèle courant
- **`StoryTimelineService.cs`** — navigation temporelle (PageSnapshots CRUD, truncate, load at page)
- **`ExpertModeService.cs`** — toggle mode expert, édition directe du state (signatures `Func<T, T>` car records immutables)
- `StoryExportService.cs` — export Markdown / texte brut / PDF
- `StoryStatisticsService.cs` — calcul statistiques (mots, personnages, événements)
- `GenerationNotificationService.cs` — notification fin de génération (Scoped, événement + son)
- Models/ViewModels pour les formulaires
- **Dépendances** : 5.0, 5.1

### 5.5 — Wizard de création d'histoire
- `Create.razor` avec `FluentWizard` 7 étapes (ou `FluentTabs`/Stepper si `FluentWizard` indisponible)
- `WorldEditor.razor` — formulaire monde (nom, description)
- **Étape 2 : Genre / Style narratif** — `FluentSelect` (fantaisie, SF, polar, horreur, historique, libre) + `NarrativeStyle` (Descriptive, Action, Introspective, Dialogue). Stocké dans le modèle de création et dans les PageSnapshots
- `CharacterEditor.razor` — formulaire personnages avec traits dynamiques
- `LocationEditor.razor` — formulaire lieux avec connexions
- **Étape 6 : Sélection modèle** — `FluentSelect` avec modèles listés dynamiquement depuis le provider
- Validation formulaires (FluentValidationMessage)
- Résumé + lancement → **auto-save immédiat** en DB (crée un slot + PageSnapshot 0)
- **Dépendances** : 5.4

### 5.6 — Génération narrative temps réel
- `Generate.razor` avec vue split (timeline pages | texte | sidebar)
- **`PageTimeline.razor`** — timeline cliquable de toutes les pages (sidebar gauche)
- `PipelineProgress.razor` — progression 5 étapes en temps réel
- `NarrativeTextRenderer.razor` — rendu texte narratif (Markdown)
- `EventTimeline.razor` — timeline événements
- **Sélecteur de modèle dans le header de page** (change le modèle pour la prochaine génération)
- **`GenerationNotifier.razor`** — notification visuelle/sonore quand la génération est terminée
- `StoryStatsBadge.razor` — stats compactes (mots, pages) dans la sidebar
- Intégration `StreamRendering` pour affichage progressif
- Boutons d'action (Continuer, Dialoguer, Décrire, Résumer)
- **Auto-save** : chaque génération crée un PageSnapshot en DB
- **Dépendances** : 5.4, 5.5

### 5.7 — Navigation temporelle & régénération
- **Navigation ←→** dans les pages (GoBack, GoForward, GoToPage)
- Cliquer sur une page dans la timeline = charge le state à cette page
- **Régénération** : disponible **uniquement sur la dernière page** — `FluentDialog` pour instructions modifiées, le snapshot de la dernière page est remplacé
- **Fork** : si l'utilisateur est sur une page intermédiaire et clique "Continuer" → tronque les pages suivantes et génère une nouvelle page depuis ce point
- Modèle utilisé + genre enregistrés dans chaque PageSnapshot
- **Dépendances** : 5.6

### 5.8 — Mode Expert
- **Composants Expert** :
  - `StateInspector.razor` — visualisation JSON du StoryState (collapsible tree view)
  - `PipelineDebugPanel.razor` — prompts envoyés, outputs bruts LLM, résultats de validation
  - `EventDetailView.razor` — détails complets des événements (metadata, actorIds, data)
  - `CharacterStateView.razor` — état complet personnages (faits connus, vitalStatus, relations IDs)
  - `RawOutputViewer.razor` — output LLM brut avant/après validation
- **Édition** : modifier directement le state (personnages, monde, événements) via formulaires inline
- **Toggle** : visible uniquement quand `ExpertModeService.IsExpertMode == true`
- Intégré dans : Generate.razor (panneau droit), Read.razor (sidebar), FactsExplorer.razor
- **Dépendances** : 5.4, 5.6

### 5.9 — Bibliothèque d'histoires (Multi-Histoires)
- `Library.razor` avec `FluentDataGrid`
- Recherche, tri, filtrage
- Actions : **Continuer** (→ Generate), Lire (→ Read), **Exporter** (→ ExportDialog), **Dupliquer** (fork complet), Supprimer (+ tous snapshots)
- **Chaque histoire affiche** : nom, **genre**, pages, mots, dernier modèle, date
- **Dépendances** : 5.3

### 5.10 — Lecteur d'histoire
- `Read.razor` avec navigation **par pages** (PageTimeline réutilisé, mode lecture seule)
- Affichage narratif stylé (prose)
- Sidebar personnages/faits
- **`StoryStatsPanel.razor`** — statistiques détaillées de l'histoire (mots, personnages, événements, modèles utilisés)
- **Bouton "📥 Exporter"** → `ExportDialog.razor` (Markdown, texte brut, PDF, téléchargement)
- **Mode Expert** : affiche metadata de chaque page (modèle, prompts, state) si activé
- **Dépendances** : 5.9

### 5.11 — Configuration LLM
- `LlmConfig.razor` — formulaire config
- **Sélection provider, modèle par défaut, modèle narrateur** (listes dynamiques)
- **Mapping Agent → Modèle** (éditable inline dans DataGrid)
- **Bouton "Lister les modèles"** — interroge le provider en direct
- Test de connexion en direct
- Sauvegarde config dans `appsettings.json` ou localStorage
- **Dépendances** : 5.2

### 5.12 — Explorateur Mémoire & Faits
- `FactsExplorer.razor` — vue hiérarchique des faits
- TreeView par niveau (Event → Chapter → Arc → World)
- DataGrid des faits canoniques
- Affichage violations de cohérence
- **Mode Expert** : édition directe des faits (ajout/suppression)
- **Dépendances** : 5.4

### 5.13 — Gestion d'erreurs & UX polish
- `ErrorDisplay.razor` — composant générique pour `Result<T>` failures
- `FluentMessageBar` pour notifications (succès, erreur, warning)
- Loading states avec `FluentProgressRing`
- **`GenerationNotifier.razor`** — son + toast quand génération terminée (toggle on/off dans footer)
- Gestion circuit SignalR (reconnexion)
- Gestion des cas limites (LLM down, DB locked, etc.)
- **Auto-save feedback** : indicateur visuel "sauvegardé" dans le footer
- **Dépendances** : 5.6, 5.9

### 5.14 — Tests & Documentation
- Vérifier le build complet du solution
- Test manuel des workflows complets (création, génération, navigation temporelle, mode expert, multi-histoires)
- Documenter dans `Docs/Phase5-Design.md`
- Mettre à jour `plans/etat-des-lieux-et-suite.md`
- **Dépendances** : 5.13

---

## Workflows Utilisateur — Scénarios Complets

### Scénario 1 : Création et génération

```
1. Accueil → "Nouvelle histoire"
2. Wizard : Nom du monde → **Genre/Style** → Personnages → Lieux → Relations → Modèle LLM → Résumé
3. Clic "Commencer" → Auto-save en DB (PageSnapshot 0 = état initial) → /story/generate/{slot}
4. Pipeline : ContextBuilder ━ PromptBuilder ━ AgentExecutor ━ Validator ━ Integrator
5. Texte narratif apparaît progressivement → Auto-save PageSnapshot 1
6. Utilisateur choisit : "Continuer" / "Dialogue entre X et Y" / "Décrire le lieu"
7. Pipeline relancé → Nouveau texte → Auto-save PageSnapshot 2
8. Répéter...
```

### Scénario 2 : Changer de modèle en cours de route

```
1. En cours de génération sur /story/generate/{slot}
2. Header → Sélecteur de modèle → Choisir "Phi-4" au lieu de "Phi-4-mini"
3. Clic "Continuer" → Le pipeline utilise le nouveau modèle
4. PageSnapshot enregistre le modèle utilisé pour cette page
5. Le lecteur montre quel modèle a généré chaque page
```

### Scénario 3 : Navigation temporelle (retour en arrière)

```
1. Histoire avec 5 pages générées, on est à la page 5
2. Clic sur Page 2 dans la timeline (sidebar gauche)
3. L'état se recharge tel qu'il était après la page 2
4. L'utilisateur peut relire cette page
5. S'il clique "Continuer" ou "Régénérer" → Fork :
   - Pages 3, 4, 5 sont supprimées (tronquées)
   - Nouvelle page 3 est générée
   - L'histoire continue depuis ce point
```

### Scénario 4 : Multi-histoires en parallèle

```
1. Accueil : 3 histoires listées (toutes en DB)
2. Clic "Continuer" sur "L'Épée Enchantée" → /story/generate/lepee-enchantee
3. Génère 2 pages
4. Header → Story Switcher → Choisir "Le Dragon Noir"
5. → /story/generate/le-dragon-noir (auto-save de l'histoire précédente)
6. Génère 1 page
7. Retour à l'accueil → Les 3 histoires montrent leur progression mise à jour
```

### Scénario 5 : Mode Expert

```
1. Header → Toggle "Mode Expert" ON
2. Panneau droit s'ouvre dans /story/generate/{slot}
3. Affiche : StoryState JSON, prompts envoyés aux agents, output brut LLM
4. L'utilisateur peut :
   - Voir les prompts exacts envoyés
   - Voir le texte brut avant validation/reformatage
   - Modifier un personnage (changer un trait, ajouter un fait connu)
   - Modifier le monde (ajouter un lieu, changer la description)
   - L'état modifié est utilisé pour la prochaine génération
5. Toggle "Mode Expert" OFF → Les panneaux disparaissent
```

### Scénario 6 : Régénérer la dernière page

```
1. La dernière page (page 3) vient d'être générée, le texte ne plaît pas
2. Clic "🔄 Régénérer" (bouton disponible uniquement sur la dernière page)
3. Dialog : "Ajoutez des instructions pour la régénération"
   → "Plus de dialogue, moins de description. Le personnage Aric doit être plus agressif."
4. Pipeline relancé avec les instructions additionnelles
5. Le snapshot de la page 3 est remplacé par la nouvelle version
6. 🔔 Notification sonore quand la régénération est terminée
```

### Scénario 7 : Consulter les stats et exporter

```
1. Dans le lecteur /story/read/{slot} ou la bibliothèque
2. Panel "Statistiques" visible : 5 pages, 2340 mots, 3 personnages, 12 événements
3. Distribution personnages : Aric (5 pages), Lyra (3 pages), Thorn (2 pages)
4. Clic "📥 Exporter" → Dialog : choix format (Markdown / Texte brut / PDF)
5. Téléchargement du fichier "Mon-Histoire.md" ou "Mon-Histoire.pdf"
```

---

## Points d'Attention

1. **Persistence : évolution du schéma** — La table `PageSnapshots` doit être ajoutée avant tout (5.0). **⚠️ NE PAS toucher à l'index unique sur SlotName** dans `SaveStateSnapshot` — il est utilisé par le mécanisme de sauvegarde existant. `PageSnapshots` est une table séparée.
2. **Persistence : FIX stub désérialisation (CRITIQUE)** — `SnapshotService.DeserializeCharacterStates/Events/WorldState()` retournent des collections vides (stubs Phase 1.5). **Sans ce fix, LoadStateAsync ne restaure ni personnages ni événements.** Doit être corrigé en 5.0.
3. **EF Core versions** : Memory utilise EF Core 10, Persistence utilise EF Core 9 → harmoniser si possible.
4. **ILlmClient est singleton** mais `StorySessionService` est scoped → pattern correct (singleton injecté dans scoped OK).
5. **ModelSelectionService** : `LlmClientConfig` est un `sealed record` immutable enregistré comme singleton. Le changement de modèle à la volée passe par un `IModelResolver` (Scoped, mutable) consulté par l'adaptateur LLM à chaque appel, PAS par une mutation du record.
6. **Mode Expert + édition** : les modifications du state doivent passer par les mêmes validations (RuleEngine) que les actions normales, sauf si l'utilisateur force le bypass. Signatures `Func<T, T>` car les records sont immutables.
7. **Navigation temporelle : taille DB** — Chaque PageSnapshot contient le StoryState complet sérialisé. Pour 50 pages, ça peut devenir volumineux. Prévoir un mécanisme de compression ou de delta-encoding futur.
8. **Auto-save** : doit être await avec gestion d'erreur propre (pas fire-and-forget — risque de perte silencieuse, contraire au principe de fiabilité). Indicateur visuel "sauvegardé" / "erreur de sauvegarde" dans le footer.
9. **SignalR circuit** : Gérer proprement la déconnexion/reconnexion. L'auto-save protège contre les pertes de données.
10. **Determinism** : L'UI ne doit jamais introduire de non-déterminisme dans le domain. **⚠️ Note : `StoryState.CreatedAt`, `CharacterState.LastUpdatedAt`, `WorldState.NarrativeTime` utilisent `DateTime.UtcNow` directement** — violation existante à corriger éventuellement.
11. **FluentWizard** : Vérifier disponibilité dans Fluent UI Blazor v4.13 — sinon, implémenter avec `FluentTabs`/Stepper. Le todo 5.5 prévoit le fallback.
12. **Régénération** : Uniquement sur la dernière page. Le snapshot est remplacé en place, pas de branching arborescent (trop complexe pour V1).
13. **Fork** : Quand l'utilisateur navigue en arrière et clique "Continuer", les pages suivantes sont définitivement supprimées et une nouvelle page est générée depuis ce point.
14. **Genre/Style** : Le genre narratif influence les prompts via le `PromptRegistry` existant et le `NarrativeStyle` enum. Le genre est stocké dans le `PageSnapshot` et dans le `SaveSlotMetadata`.
15. **Export PDF** : Nécessite un package de génération PDF côté serveur (ex: QuestPDF, iText, ou simple conversion HTML→PDF). À évaluer lors de l'implémentation 5.10.
