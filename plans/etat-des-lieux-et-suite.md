# État des Lieux Narratum — Février 2026

**Date**: 10 février 2026
**Statut Global**: Phase 4 complète — Intégration LLM via Microsoft.Extensions.AI ✅

---

## Résumé Exécutif

Narratum est en **très bon état** — architecture hexagonale solide, tests exhaustifs :

| Phase | Statut | Tests |
|-------|--------|-------|
| **Phase 1** — Fondations | ✅ 100% | 110 tests |
| **Phase 2** — Mémoire & Cohérence | ✅ 100% (2.1→2.7) | 274 tests |
| **Phase 3** — Orchestration | ✅ 100% (3.1→3.8 complet) | 461 tests |
| **Phase 4** — Intégration LLM | ✅ 100% | 52 tests |
| **Phase 5** — Narration Contrôlée | ⏳ Non démarré | — |
| **Phase 6** — UI | ⏳ Non démarré | — |

**Total : 894 tests — 100% passing ✅**

---

## Architecture — Graphe de Dépendances

```
Core (0 deps)
 └→ Domain
     └→ State
         ├→ Rules → Simulation
         ├→ Persistence (EF Core 9 + SQLite)
         ├→ Memory (EF Core 10 + SQLite)
         │   └→ Orchestration
         │       └→ Llm (IChatClient → ILlmClient)
         └→ Playground (Spectre.Console)

Tests → Core, Domain, State, Rules, Simulation, Persistence
Memory.Tests → Memory
Orchestration.Tests → Orchestration, Core, Domain, State, Memory
```

**Aucune dépendance circulaire.** ✅

---

## Ce qui est construit

### Phase 1–3 (COMPLÈTES ✅)
Voir sections détaillées ci-dessous.

### Phase 4 — Intégration LLM (EN COURS 🔨)

**Approche** : Utilisation de `Microsoft.Extensions.AI` (`IChatClient`) — l'abstraction officielle .NET.
Pas de client HTTP manuel : on utilise les SDK existants.

**Projet `Narratum.Llm`** — Structure :

| Composant | Fichier | Description |
|-----------|---------|-------------|
| **Configuration** | `LlmProviderType.cs` | Enum : FoundryLocal, Ollama |
| **Configuration** | `LlmClientConfig.cs` | Config avec routing par agent, `NarratorModel` paramétrable |
| **Adaptateur** | `ChatClientLlmAdapter.cs` | Bridge `IChatClient` → `ILlmClient` (Narratum) |
| **Lifecycle** | `ILlmLifecycleManager.cs` | Interface lifecycle provider local |
| **Lifecycle** | `FoundryLocalLifecycleManager.cs` | SDK Foundry Local : download, load, start/stop |
| **Factory** | `ILlmClientFactory.cs` | Interface factory |
| **Factory** | `LlmClientFactory.cs` | Crée IChatClient selon provider puis wraps dans l'adaptateur |
| **DI** | `LlmServiceCollectionExtensions.cs` | `AddNarratumLlm()`, `AddNarratumFoundryLocal()`, `AddNarratumOllama()` |

**SDKs utilisés** :

| Provider | Package NuGet | IChatClient |
|----------|---------------|-------------|
| Foundry Local | `Microsoft.AI.Foundry.Local` + `OpenAI` | `OpenAIClient.GetChatClient().AsIChatClient()` |
| Ollama | `OllamaSharp` | `OllamaApiClient` (implémente IChatClient nativement) |

**Routing par agent** :
- Chaque agent peut avoir un modèle LLM différent via `AgentModelMapping`
- Le modèle du Narrateur est paramétrable via `NarratorModel`
- Priorité : `NarratorModel` (Narrator) > `AgentModelMapping` > `DefaultModel`
- Les métadonnées `llm.agentType` sont passées dans chaque `LlmRequest`

**Patch orchestrateur** : `FullOrchestrationService.ExecuteAgentsAsync()` passe désormais le `AgentType` dans les métadonnées de `LlmRequest`.

#### Tâches Phase 4

| Étape | Statut |
|-------|--------|
| 4.1 Créer Narratum.Llm | ✅ Fait |
| 4.2 Configuration (types, routing) | ✅ Fait |
| 4.3 Adaptateur IChatClient → ILlmClient | ✅ Fait |
| 4.4 FoundryLocal Lifecycle Manager | ✅ Fait |
| 4.5 Factory + DI | ✅ Fait |
| 4.6 Patch orchestrateur (metadata AgentType) | ✅ Fait |
| 4.7 Tests unitaires Narratum.Llm | ✅ Fait (52 tests) |
| 4.8 Tests intégration (skip si provider absent) | ⏳ À faire si besoin |
| 4.9 Documentation | ✅ Fait |

---

## Détails Phase 1–3

### Phase 1 — Fondations (COMPLÈTE ✅)
- **Core** : Id, Result<T>, DomainEvent, IStoryRule, IRepository
- **Domain** : StoryWorld, Character, Location, 4 types d'Event, Relationship
- **State** : StoryState, CharacterState, WorldState (immuables)
- **Rules** : 9 règles narratives, RuleEngine
- **Simulation** : 7 types d'action, StateTransitionService, ProgressionService
- **Persistence** : EF Core + SQLite, Snapshots
- **Playground** : Démo CLI Spectre.Console (histoire 3 chapitres)

### Phase 2 — Mémoire & Cohérence (COMPLÈTE ✅)
- **Memory.Models** : Fact, CanonicalState, CoherenceViolation, Memorandum
- **Memory.Services** : FactExtractorService, SummaryGeneratorService, CoherenceValidator, MemoryService, MemoryQueryService
- **Memory.Store** : MemoryDbContext, SQLiteMemoryRepository, MemorandumEntity
- 7 phases (2.1→2.7) toutes complétées avec tests d'intégration

### Phase 3 — Orchestration (100% ✅)

47 fichiers de production, 22 fichiers de tests, 461 tests.

| Composant | Statut |
|-----------|--------|
| Pipeline (5 stages) | ✅ ContextBuilder, PromptBuilder, AgentExecutor, OutputValidator, StateIntegrator |
| Agents simulés (4) | ✅ NarratorAgent, CharacterAgent, SummaryAgent, ConsistencyAgent |
| Abstraction LLM | ✅ ILlmClient + MockLlmClient |
| Système de Prompts | ✅ IPromptTemplate, PromptRegistry, 4 templates |
| Validation | ✅ StructureValidator, CoherenceValidatorAdapter, RetryHandler |
| Logging | ✅ PipelineLogger, MetricsCollector, AuditTrail |
| Orchestration Service | ✅ FullOrchestrationService (service principal) |
| **Intégration E2E** | ✅ **Phase 3.8 — 64 tests end-to-end** |

---

## Ce qui reste à faire

### Phase 5 : Narration Contrôlée
- NarratorAgent, CharacterAgent, ConsistencyAgent réels
- Température maîtrisée, prompts stricts
- Cohérence sur 20+ itérations

### Phase 6 : UI
- Blazor WebView / MAUI / Avalonia
- API REST ASP.NET Core

---

## Backlog technique

| Priorité | Item | Impact |
|----------|------|--------|
| 🟡 | Consolider docs Phase 2 (7 fichiers → 1) | Maintenabilité |
| 🟡 | Consolider docs Phase 3 | Maintenabilité |
| 🟡 | Version EF Core divergente (Memory=10, Persistence=9) | Compatibilité |
| 🟢 | Nettoyer les 20+ fichiers PHASE*.md à la racine | Organisation |
| 🟢 | Créer exemple Memory end-to-end | Onboarding |

---

## Commandes

```bash
# Build (0 erreurs, 0 warnings)
dotnet build Narratum.sln

# Tests (894 passing)
dotnet test

# Test spécifique
dotnet test Orchestration.Tests --filter "NomDuTest"

# Test un projet
dotnet test Memory.Tests -v normal
```
