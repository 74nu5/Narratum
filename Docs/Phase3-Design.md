# Phase 3 — Orchestration (Design et Architecture)

**Status**: 📋 DESIGN DOCUMENT
**Phase**: Phase 3 — Orchestration (LLM en Boîte Noire)
**Dependencies**: Phase 1 (✅ COMPLETE), Phase 2 (✅ COMPLETE)

---

## 📋 Table des Matières

1. [Objectif et Contexte](#objectif-et-contexte)
2. [Principes Directeurs](#principes-directeurs)
3. [Architecture Globale](#architecture-globale)
4. [Modules et Composants](#modules-et-composants)
5. [Pipeline d'Exécution](#pipeline-dexécution)
6. [Agents Simulés](#agents-simulés)
7. [Système de Prompts](#système-de-prompts)
8. [Logging et Observabilité](#logging-et-observabilité)
9. [APIs Publiques](#apis-publiques)
10. [Plan de Développement](#plan-de-développement)
11. [Tests et Validation](#tests-et-validation)
12. [Interdictions Volontaires](#interdictions-volontaires)

---

## Objectif et Contexte

### Vision Phase 3

Phase 3 construit un **système d'orchestration** qui permet au moteur narratif de:

- 🎭 **Orchestrer** les agents de génération narrative
- 📝 **Piloter** les prompts de manière déterministe
- 🔄 **Coordonner** les étapes du pipeline de génération
- ✅ **Valider** les sorties avant intégration
- 🔒 **Garantir** que le système fonctionne même avec un LLM "stupide"

### Le Principe Fondamental

> **Le système doit fonctionner même si le LLM est stupide.**

```csharp
// Si on peut remplacer le LLM par ceci et que tout marche:
public class StupidLlm : ILlmClient
{
    public Task<string> GenerateAsync(string prompt)
    {
        return Task.FromResult("TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE");
    }
}
// Alors l'orchestration est robuste.
```

### Pourquoi avant l'intégration LLM réelle?

Si l'orchestration dépend de la qualité du LLM pour fonctionner:
- Le système est fragile
- Les bugs sont impossibles à isoler
- Les tests sont non-déterministes

Phase 3 prouve que **l'architecture est solide** avant d'y injecter de la créativité.

### Transition depuis Phase 2

Phase 2 fournit:
- ✅ Système de mémoire (Memorandum, Fact, CanonicalState)
- ✅ Extraction de faits (IFactExtractor)
- ✅ Résumés hiérarchiques (ISummaryGenerator)
- ✅ Validation de cohérence (ICoherenceValidator)
- ✅ Persistance des memorias (SQLiteMemoryRepository)

Phase 3 **ajoute**:
- 🎭 Pipeline d'orchestration
- 📝 Système de prompts
- 🤖 Agents simulés (MockLlm)
- 🔄 Boucle de réécriture contrôlée
- 📊 Logging exhaustif

---

## Principes Directeurs

### 1️⃣ LLM en Boîte Noire

Le LLM est traité comme une **fonction pure** avec entrée/sortie:

```csharp
// Le LLM est une boîte noire
public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request);
}

// L'orchestrateur ne dépend pas de la qualité du LLM
public class Orchestrator
{
    private readonly ILlmClient _llm; // Peut être mock ou réel

    public async Task<NarrativeOutput> GenerateAsync(NarrativeContext context)
    {
        var response = await _llm.GenerateAsync(BuildRequest(context));
        return ValidateAndProcess(response); // Validation indépendante du LLM
    }
}
```

### 2️⃣ Pipeline Déterministe

Chaque étape du pipeline est:
- **Ordonnée** (séquence fixe)
- **Traçable** (logs à chaque étape)
- **Validée** (vérification avant passage à l'étape suivante)
- **Reproductible** (même entrée = même comportement)

```
Context → [Prompt Builder] → [LLM Call] → [Validator] → [Integrator] → Output
              ↑                              ↓
              └──────── [Retry/Rewrite] ─────┘
```

### 3️⃣ Validation Post-Génération

Tout output du LLM est **validé** avant intégration:

```csharp
public interface IOutputValidator
{
    ValidationResult Validate(LlmResponse response, NarrativeContext context);
}

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, object> Metadata
);
```

### 4️⃣ Agents Spécialisés

Chaque agent a une **responsabilité unique**:

| Agent | Responsabilité | Input | Output |
|-------|----------------|-------|--------|
| SummaryAgent | Résumer les événements | Events | Summary string |
| NarratorAgent | Générer la prose | Context + Summary | Narrative text |
| CharacterAgent | Générer les dialogues | Character + Situation | Dialogue |
| ConsistencyAgent | Vérifier la cohérence | Text + Facts | Corrections |

### 5️⃣ Aucune Décision Autonome

Un agent ne peut **jamais**:
- Modifier l'état du monde sans validation
- Créer des événements non-validés
- Ignorer les règles narratives

```csharp
// INTERDIT
public class BadAgent
{
    public void Generate()
    {
        _storyState.AddEvent(new Event(...)); // Non validé!
    }
}

// CORRECT
public class GoodAgent
{
    public ProposedAction Generate()
    {
        return new ProposedAction(...); // Doit être validé par l'orchestrateur
    }
}
```

---

## Architecture Globale

### Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Narratum.Orchestration (nouveau module)                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  IOrchestrationService (interface publique)                       │ │
│  │  - Exécuter un cycle narratif                                    │ │
│  │  - Coordonner les agents                                         │ │
│  │  - Gérer le pipeline de génération                               │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                        ↓                                                │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  Pipeline Stages (étapes ordonnées)                              │ │
│  │  1. ContextBuilder      → Construire le contexte                 │ │
│  │  2. PromptBuilder       → Générer les prompts                    │ │
│  │  3. AgentExecutor       → Appeler les agents                     │ │
│  │  4. OutputValidator     → Valider les sorties                    │ │
│  │  5. StateIntegrator     → Intégrer dans l'état                   │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                        ↓                                                │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  Agents (simulés en Phase 3)                                     │ │
│  │  - ISummaryAgent        (résumés)                                │ │
│  │  - INarratorAgent       (prose)                                  │ │
│  │  - ICharacterAgent      (dialogues)                              │ │
│  │  - IConsistencyAgent    (cohérence)                              │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                        ↓                                                │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  LLM Abstraction (boîte noire)                                   │ │
│  │  - ILlmClient           (interface)                              │ │
│  │  - MockLlmClient        (simulation Phase 3)                     │ │
│  │  - [OllamaClient]       (Phase 4+)                               │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                        ↓                                                │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  Logging & Observability                                         │ │
│  │  - PipelineLogger       (trace complète)                         │ │
│  │  - MetricsCollector     (performance)                            │ │
│  │  - AuditTrail           (décisions)                              │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                              ↓
              Narratum.Memory (Phase 2 ✅)
              Narratum.Simulation (Phase 1 ✅)
              Narratum.State (Phase 1 ✅)
              Narratum.Domain (Phase 1 ✅)
              Narratum.Core (Phase 1 ✅)
```

### Intégration dans la solution

```
Narratum/
├── Core/                          (Phase 1 ✅)
├── Domain/                        (Phase 1 ✅)
├── State/                         (Phase 1 ✅)
├── Persistence/                   (Phase 1 ✅)
├── Rules/                         (Phase 1 ✅)
├── Simulation/                    (Phase 1 ✅)
├── Memory/                        (Phase 2 ✅)
├── Orchestration/                 (Phase 3 🆕)
│   ├── Pipeline/
│   │   ├── IOrchestrationService.cs
│   │   ├── OrchestrationService.cs
│   │   ├── PipelineContext.cs
│   │   └── PipelineResult.cs
│   ├── Stages/
│   │   ├── IContextBuilder.cs
│   │   ├── IPromptBuilder.cs
│   │   ├── IAgentExecutor.cs
│   │   ├── IOutputValidator.cs
│   │   └── IStateIntegrator.cs
│   ├── Agents/
│   │   ├── IAgent.cs
│   │   ├── ISummaryAgent.cs
│   │   ├── INarratorAgent.cs
│   │   ├── ICharacterAgent.cs
│   │   ├── IConsistencyAgent.cs
│   │   └── Mock/
│   │       ├── MockSummaryAgent.cs
│   │       ├── MockNarratorAgent.cs
│   │       ├── MockCharacterAgent.cs
│   │       └── MockConsistencyAgent.cs
│   ├── LLM/
│   │   ├── ILlmClient.cs
│   │   ├── LlmRequest.cs
│   │   ├── LlmResponse.cs
│   │   └── MockLlmClient.cs
│   ├── Prompts/
│   │   ├── IPromptTemplate.cs
│   │   ├── PromptRegistry.cs
│   │   └── Templates/
│   │       ├── SummaryPrompt.cs
│   │       ├── NarratorPrompt.cs
│   │       └── CharacterPrompt.cs
│   ├── Validation/
│   │   ├── IOutputValidator.cs
│   │   ├── StructureValidator.cs
│   │   ├── CoherenceValidator.cs
│   │   └── ValidationResult.cs
│   ├── Logging/
│   │   ├── IPipelineLogger.cs
│   │   ├── PipelineLogger.cs
│   │   ├── PipelineEvent.cs
│   │   └── AuditTrail.cs
│   └── Models/
│       ├── NarrativeContext.cs
│       ├── NarrativeOutput.cs
│       ├── AgentRequest.cs
│       └── AgentResponse.cs
├── Orchestration.Tests/           (Phase 3 🆕)
├── Tests/                         (Phase 1 ✅ + Phase 2 ✅)
├── Memory.Tests/                  (Phase 2 ✅)
└── Playground/                    (Phase 1 ✅)
```

---

## Modules et Composants

### 1. Narratum.Orchestration.Pipeline

**Responsabilité**: Coordonner le flux de génération narrative.

#### IOrchestrationService (Interface)

```csharp
public interface IOrchestrationService
{
    /// <summary>
    /// Exécute un cycle narratif complet.
    /// </summary>
    Task<Result<NarrativeOutput>> ExecuteCycleAsync(
        StoryState currentState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute un agent spécifique.
    /// </summary>
    Task<Result<AgentResponse>> ExecuteAgentAsync(
        AgentType agentType,
        AgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide une sortie avant intégration.
    /// </summary>
    Task<Result<ValidationResult>> ValidateOutputAsync(
        NarrativeOutput output,
        StoryState context);

    /// <summary>
    /// Obtient l'état du pipeline.
    /// </summary>
    PipelineStatus GetStatus();
}

public enum AgentType
{
    Summary,
    Narrator,
    Character,
    Consistency
}

public record NarrativeIntent(
    IntentType Type,
    Id? TargetCharacterId = null,
    Id? TargetLocationId = null,
    string? CustomDirective = null
);

public enum IntentType
{
    ContinueNarrative,      // Continuer l'histoire
    DescribeScene,          // Décrire une scène
    GenerateDialogue,       // Générer un dialogue
    SummarizeChapter,       // Résumer un chapitre
    ResolveConflict         // Résoudre un conflit
}
```

#### OrchestrationService (Implémentation)

```csharp
public class OrchestrationService : IOrchestrationService
{
    private readonly IContextBuilder _contextBuilder;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IAgentExecutor _agentExecutor;
    private readonly IOutputValidator _outputValidator;
    private readonly IStateIntegrator _stateIntegrator;
    private readonly IPipelineLogger _logger;
    private readonly OrchestrationConfig _config;

    public OrchestrationService(
        IContextBuilder contextBuilder,
        IPromptBuilder promptBuilder,
        IAgentExecutor agentExecutor,
        IOutputValidator outputValidator,
        IStateIntegrator stateIntegrator,
        IPipelineLogger logger,
        OrchestrationConfig config)
    {
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _agentExecutor = agentExecutor;
        _outputValidator = outputValidator;
        _stateIntegrator = stateIntegrator;
        _logger = logger;
        _config = config;
    }

    public async Task<Result<NarrativeOutput>> ExecuteCycleAsync(
        StoryState currentState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default)
    {
        var pipelineId = Guid.NewGuid();
        _logger.LogPipelineStart(pipelineId, intent);

        try
        {
            // Étape 1: Construire le contexte
            _logger.LogStageStart(pipelineId, "ContextBuilder");
            var contextResult = await _contextBuilder.BuildAsync(currentState, intent);
            if (contextResult is Result<NarrativeContext>.Failure ctxFail)
            {
                _logger.LogStageFailure(pipelineId, "ContextBuilder", ctxFail.Message);
                return Result<NarrativeOutput>.Fail(ctxFail.Message);
            }
            var context = ((Result<NarrativeContext>.Success)contextResult).Value;
            _logger.LogStageComplete(pipelineId, "ContextBuilder");

            // Étape 2: Construire les prompts
            _logger.LogStageStart(pipelineId, "PromptBuilder");
            var promptsResult = await _promptBuilder.BuildAsync(context, intent);
            if (promptsResult is Result<PromptSet>.Failure promptFail)
            {
                _logger.LogStageFailure(pipelineId, "PromptBuilder", promptFail.Message);
                return Result<NarrativeOutput>.Fail(promptFail.Message);
            }
            var prompts = ((Result<PromptSet>.Success)promptsResult).Value;
            _logger.LogStageComplete(pipelineId, "PromptBuilder");

            // Étape 3: Exécuter les agents
            _logger.LogStageStart(pipelineId, "AgentExecutor");
            var agentResult = await _agentExecutor.ExecuteAsync(prompts, context);
            if (agentResult is Result<RawOutput>.Failure agentFail)
            {
                _logger.LogStageFailure(pipelineId, "AgentExecutor", agentFail.Message);
                return Result<NarrativeOutput>.Fail(agentFail.Message);
            }
            var rawOutput = ((Result<RawOutput>.Success)agentResult).Value;
            _logger.LogStageComplete(pipelineId, "AgentExecutor");

            // Étape 4: Valider les sorties
            _logger.LogStageStart(pipelineId, "OutputValidator");
            var validationResult = await _outputValidator.ValidateAsync(rawOutput, context);

            // Boucle de réécriture si nécessaire
            var retryCount = 0;
            while (!validationResult.IsValid && retryCount < _config.MaxRetries)
            {
                _logger.LogRetry(pipelineId, retryCount, validationResult.Errors);

                var rewriteResult = await _agentExecutor.RewriteAsync(
                    rawOutput,
                    validationResult,
                    context);

                if (rewriteResult is Result<RawOutput>.Failure)
                    break;

                rawOutput = ((Result<RawOutput>.Success)rewriteResult).Value;
                validationResult = await _outputValidator.ValidateAsync(rawOutput, context);
                retryCount++;
            }

            if (!validationResult.IsValid)
            {
                _logger.LogStageFailure(pipelineId, "OutputValidator",
                    string.Join("; ", validationResult.Errors));
                return Result<NarrativeOutput>.Fail(
                    $"Validation failed after {retryCount} retries");
            }
            _logger.LogStageComplete(pipelineId, "OutputValidator");

            // Étape 5: Intégrer dans l'état
            _logger.LogStageStart(pipelineId, "StateIntegrator");
            var output = await _stateIntegrator.IntegrateAsync(rawOutput, context);
            _logger.LogStageComplete(pipelineId, "StateIntegrator");

            _logger.LogPipelineComplete(pipelineId, output);
            return Result<NarrativeOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogPipelineError(pipelineId, ex);
            return Result<NarrativeOutput>.Fail($"Pipeline failed: {ex.Message}");
        }
    }

    // Autres méthodes...
}
```

#### PipelineContext (Record)

```csharp
public record PipelineContext(
    Guid PipelineId,
    DateTime StartedAt,
    StoryState CurrentState,
    NarrativeIntent Intent,
    IReadOnlyList<Memorandum> RelevantMemoria,
    CanonicalState CanonicalState,
    IReadOnlyDictionary<string, object> Metadata
);

public record PipelineResult(
    Guid PipelineId,
    bool Success,
    NarrativeOutput? Output,
    string? ErrorMessage,
    TimeSpan Duration,
    IReadOnlyList<PipelineStageResult> StageResults
);

public record PipelineStageResult(
    string StageName,
    bool Success,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object> Metadata
);
```

---

### 2. Narratum.Orchestration.Stages

**Responsabilité**: Implémenter chaque étape du pipeline.

#### IContextBuilder

```csharp
public interface IContextBuilder
{
    Task<Result<NarrativeContext>> BuildAsync(
        StoryState currentState,
        NarrativeIntent intent);
}

public record NarrativeContext(
    StoryState State,
    NarrativeIntent Intent,

    // Mémoire pertinente
    IReadOnlyList<Memorandum> RecentMemoria,
    CanonicalState CanonicalState,

    // Personnages actifs
    IReadOnlyList<CharacterContext> ActiveCharacters,

    // Lieu actuel
    LocationContext? CurrentLocation,

    // Historique récent
    IReadOnlyList<Event> RecentEvents,
    string RecentSummary,

    // Métadonnées
    DateTime ContextBuiltAt,
    IReadOnlyDictionary<string, object> Metadata
);

public record CharacterContext(
    Id CharacterId,
    string Name,
    VitalStatus Status,
    IReadOnlySet<string> KnownFacts,
    IReadOnlySet<string> Traits,
    string? CurrentMood
);

public record LocationContext(
    Id LocationId,
    string Name,
    string Description,
    IReadOnlySet<Id> PresentCharacters
);
```

#### IPromptBuilder

```csharp
public interface IPromptBuilder
{
    Task<Result<PromptSet>> BuildAsync(
        NarrativeContext context,
        NarrativeIntent intent);
}

public record PromptSet(
    IReadOnlyList<AgentPrompt> Prompts,
    ExecutionOrder Order
);

public record AgentPrompt(
    AgentType TargetAgent,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string> Variables,
    PromptPriority Priority
);

public enum ExecutionOrder
{
    Sequential,     // Un agent après l'autre
    Parallel,       // Tous en parallèle
    Conditional     // Selon les résultats précédents
}

public enum PromptPriority
{
    Required,       // Doit s'exécuter
    Optional,       // Peut être ignoré si erreur
    Fallback        // Exécuté si le principal échoue
}
```

#### IAgentExecutor

```csharp
public interface IAgentExecutor
{
    Task<Result<RawOutput>> ExecuteAsync(
        PromptSet prompts,
        NarrativeContext context);

    Task<Result<RawOutput>> RewriteAsync(
        RawOutput previousOutput,
        ValidationResult validationResult,
        NarrativeContext context);
}

public record RawOutput(
    IReadOnlyDictionary<AgentType, AgentResponse> Responses,
    DateTime GeneratedAt,
    TimeSpan TotalDuration
);

public record AgentResponse(
    AgentType Agent,
    string Content,
    bool Success,
    string? ErrorMessage,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object> Metadata
);
```

#### IOutputValidator

```csharp
public interface IOutputValidator
{
    Task<ValidationResult> ValidateAsync(
        RawOutput output,
        NarrativeContext context);
}

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings,
    IReadOnlyDictionary<string, object> Metadata
)
{
    public static ValidationResult Valid() =>
        new(true, [], [], new Dictionary<string, object>());

    public static ValidationResult Invalid(params string[] errors) =>
        new(false, errors.Select(e => new ValidationError(e, ErrorSeverity.Critical)).ToList(),
            [], new Dictionary<string, object>());
}

public record ValidationError(
    string Message,
    ErrorSeverity Severity,
    string? SuggestedFix = null
);

public record ValidationWarning(
    string Message,
    string? Context = null
);

public enum ErrorSeverity
{
    Minor,      // Peut être ignoré
    Major,      // Devrait être corrigé
    Critical    // Doit être corrigé
}
```

#### IStateIntegrator

```csharp
public interface IStateIntegrator
{
    Task<NarrativeOutput> IntegrateAsync(
        RawOutput rawOutput,
        NarrativeContext context);
}

public record NarrativeOutput(
    string NarrativeText,
    IReadOnlyList<Event> GeneratedEvents,
    IReadOnlyList<StateChange> StateChanges,
    Memorandum GeneratedMemorandum,
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, object> Metadata
);

public record StateChange(
    StateChangeType Type,
    Id EntityId,
    string Description,
    object? OldValue,
    object? NewValue
);

public enum StateChangeType
{
    CharacterMoved,
    CharacterStatusChanged,
    RelationshipUpdated,
    FactRevealed,
    TimeAdvanced
}
```

---

### 3. Narratum.Orchestration.Agents

**Responsabilité**: Définir et simuler les agents de génération.

#### IAgent (Interface de Base)

```csharp
public interface IAgent
{
    AgentType Type { get; }
    string Name { get; }

    Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default);

    bool CanHandle(NarrativeIntent intent);
}
```

#### ISummaryAgent

```csharp
public interface ISummaryAgent : IAgent
{
    Task<Result<string>> SummarizeEventsAsync(
        IReadOnlyList<Event> events,
        int targetLength = 500);

    Task<Result<string>> SummarizeChapterAsync(
        StoryChapter chapter,
        IReadOnlyList<Event> chapterEvents);
}
```

#### INarratorAgent

```csharp
public interface INarratorAgent : IAgent
{
    Task<Result<string>> GenerateNarrativeAsync(
        NarrativeContext context,
        string summary,
        NarrativeStyle style = NarrativeStyle.Descriptive);

    Task<Result<string>> DescribeSceneAsync(
        LocationContext location,
        IReadOnlyList<CharacterContext> presentCharacters);
}

public enum NarrativeStyle
{
    Descriptive,    // Prose riche et détaillée
    Action,         // Rythme rapide
    Introspective,  // Pensées et émotions
    Dialogue        // Focus sur les échanges
}
```

#### ICharacterAgent

```csharp
public interface ICharacterAgent : IAgent
{
    Task<Result<string>> GenerateDialogueAsync(
        CharacterContext speaker,
        CharacterContext? listener,
        DialogueSituation situation);

    Task<Result<string>> GenerateReactionAsync(
        CharacterContext character,
        Event triggeringEvent);
}

public record DialogueSituation(
    string Context,
    EmotionalTone Tone,
    IReadOnlyList<string> TopicsToAddress
);

public enum EmotionalTone
{
    Neutral,
    Friendly,
    Hostile,
    Fearful,
    Excited,
    Sad
}
```

#### IConsistencyAgent

```csharp
public interface IConsistencyAgent : IAgent
{
    Task<Result<ConsistencyCheck>> CheckConsistencyAsync(
        string generatedText,
        CanonicalState canonicalState);

    Task<Result<string>> SuggestCorrectionsAsync(
        string text,
        IReadOnlyList<CoherenceViolation> violations);
}

public record ConsistencyCheck(
    bool IsConsistent,
    IReadOnlyList<ConsistencyIssue> Issues,
    double ConfidenceScore
);

public record ConsistencyIssue(
    string Description,
    string ProblematicText,
    string? SuggestedFix,
    IssueSeverity Severity
);

public enum IssueSeverity
{
    Minor,
    Moderate,
    Severe
}
```

---

### 4. Narratum.Orchestration.LLM

**Responsabilité**: Abstraire l'accès au LLM.

#### ILlmClient (Interface)

```csharp
public interface ILlmClient
{
    Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LlmResponse>> GenerateWithRetryAsync(
        LlmRequest request,
        int maxRetries = 3,
        CancellationToken cancellationToken = default);

    bool IsAvailable { get; }
    LlmClientInfo Info { get; }
}

public record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    LlmParameters Parameters,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record LlmParameters(
    int MaxTokens = 1000,
    double Temperature = 0.7,
    double TopP = 0.9,
    IReadOnlyList<string>? StopSequences = null
);

public record LlmResponse(
    string Content,
    int TokensUsed,
    TimeSpan Duration,
    bool FromCache,
    IReadOnlyDictionary<string, object> Metadata
);

public record LlmClientInfo(
    string Name,
    string Version,
    bool SupportsStreaming,
    int MaxContextLength
);
```

#### MockLlmClient (Simulation Phase 3)

```csharp
public class MockLlmClient : ILlmClient
{
    private readonly MockLlmConfig _config;
    private readonly ILogger<MockLlmClient> _logger;

    public bool IsAvailable => true;

    public LlmClientInfo Info => new(
        Name: "MockLLM",
        Version: "1.0.0",
        SupportsStreaming: false,
        MaxContextLength: 4096
    );

    public MockLlmClient(MockLlmConfig config, ILogger<MockLlmClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockLLM generating response for: {Prompt}",
            request.UserPrompt[..Math.Min(100, request.UserPrompt.Length)]);

        // Simuler un délai réaliste
        Thread.Sleep(_config.SimulatedLatencyMs);

        // Générer une réponse structurellement valide mais "stupide"
        var content = GenerateMockContent(request);

        var response = new LlmResponse(
            Content: content,
            TokensUsed: content.Length / 4, // Approximation
            Duration: TimeSpan.FromMilliseconds(_config.SimulatedLatencyMs),
            FromCache: false,
            Metadata: new Dictionary<string, object>
            {
                { "mock", true },
                { "config", _config.Name }
            }
        );

        return Task.FromResult(Result<LlmResponse>.Ok(response));
    }

    private string GenerateMockContent(LlmRequest request)
    {
        // Analyser le type de prompt pour générer une réponse appropriée
        if (request.SystemPrompt.Contains("summary", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateMockSummary(request);
        }

        if (request.SystemPrompt.Contains("dialogue", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateMockDialogue(request);
        }

        if (request.SystemPrompt.Contains("narrat", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateMockNarrative(request);
        }

        return "MOCK RESPONSE: Structurally valid but content is placeholder.";
    }

    private string GenerateMockSummary(LlmRequest request)
    {
        return "SUMMARY: Events occurred. Characters acted. Time passed. " +
               "The narrative progressed from state A to state B.";
    }

    private string GenerateMockDialogue(LlmRequest request)
    {
        return "\"I understand,\" the character said. " +
               "\"We must proceed with caution.\" " +
               "The words hung in the air, laden with meaning.";
    }

    private string GenerateMockNarrative(LlmRequest request)
    {
        return "The scene unfolded with deliberate purpose. " +
               "Characters moved through the space, their intentions clear. " +
               "The world continued its inexorable march forward, " +
               "carrying all within it toward an uncertain future.";
    }

    public Task<Result<LlmResponse>> GenerateWithRetryAsync(
        LlmRequest request,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        // Mock ne fail jamais, pas besoin de retry
        return GenerateAsync(request, cancellationToken);
    }
}

public record MockLlmConfig(
    string Name = "default",
    int SimulatedLatencyMs = 100,
    bool SimulateOccasionalFailures = false,
    double FailureRate = 0.0
);
```

---

### 5. Narratum.Orchestration.Prompts

**Responsabilité**: Gérer les templates de prompts.

#### IPromptTemplate

```csharp
public interface IPromptTemplate
{
    string Name { get; }
    AgentType TargetAgent { get; }

    string BuildSystemPrompt(NarrativeContext context);
    string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent);

    IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context);
}
```

#### PromptRegistry

```csharp
public class PromptRegistry
{
    private readonly Dictionary<(AgentType, IntentType), IPromptTemplate> _templates = new();

    public void Register(IPromptTemplate template, IntentType intentType)
    {
        _templates[(template.TargetAgent, intentType)] = template;
    }

    public IPromptTemplate? GetTemplate(AgentType agent, IntentType intent)
    {
        return _templates.TryGetValue((agent, intent), out var template)
            ? template
            : null;
    }

    public IReadOnlyList<IPromptTemplate> GetAllTemplates()
    {
        return _templates.Values.ToList();
    }
}
```

#### Exemple: SummaryPromptTemplate

```csharp
public class SummaryPromptTemplate : IPromptTemplate
{
    public string Name => "SummaryPrompt";
    public AgentType TargetAgent => AgentType.Summary;

    public string BuildSystemPrompt(NarrativeContext context)
    {
        return """
            You are a narrative summarizer. Your task is to create concise,
            factual summaries of story events.

            Rules:
            - Be factual and objective
            - Include all major events
            - Mention key characters by name
            - Note any state changes (deaths, movements, revelations)
            - Keep the summary under 500 words

            Format: Plain prose, past tense, third person.
            """;
    }

    public string BuildUserPrompt(NarrativeContext context, NarrativeIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Summarize the following events:");
        sb.AppendLine();

        foreach (var evt in context.RecentEvents)
        {
            sb.AppendLine($"- {FormatEvent(evt)}");
        }

        sb.AppendLine();
        sb.AppendLine("Active characters:");
        foreach (var character in context.ActiveCharacters)
        {
            sb.AppendLine($"- {character.Name} ({character.Status})");
        }

        return sb.ToString();
    }

    public IReadOnlyDictionary<string, string> GetVariables(NarrativeContext context)
    {
        return new Dictionary<string, string>
        {
            { "event_count", context.RecentEvents.Count.ToString() },
            { "character_count", context.ActiveCharacters.Count.ToString() },
            { "location", context.CurrentLocation?.Name ?? "Unknown" }
        };
    }

    private string FormatEvent(Event evt)
    {
        return evt.Type switch
        {
            "CharacterDeath" => $"{evt.ActorIds[0]} died",
            "CharacterMoved" => $"{evt.ActorIds[0]} moved to {evt.LocationId}",
            "CharacterEncounter" => $"{evt.ActorIds[0]} met {evt.ActorIds[1]}",
            "Revelation" => $"A revelation occurred involving {evt.ActorIds[0]}",
            _ => $"Event: {evt.Type}"
        };
    }
}
```

---

### 6. Narratum.Orchestration.Logging

**Responsabilité**: Tracer l'exécution du pipeline.

#### IPipelineLogger

```csharp
public interface IPipelineLogger
{
    void LogPipelineStart(Guid pipelineId, NarrativeIntent intent);
    void LogPipelineComplete(Guid pipelineId, NarrativeOutput output);
    void LogPipelineError(Guid pipelineId, Exception exception);

    void LogStageStart(Guid pipelineId, string stageName);
    void LogStageComplete(Guid pipelineId, string stageName);
    void LogStageFailure(Guid pipelineId, string stageName, string error);

    void LogAgentCall(Guid pipelineId, AgentType agent, string prompt);
    void LogAgentResponse(Guid pipelineId, AgentType agent, string response);

    void LogRetry(Guid pipelineId, int attemptNumber, IReadOnlyList<ValidationError> errors);
    void LogValidation(Guid pipelineId, ValidationResult result);

    IReadOnlyList<PipelineEvent> GetPipelineHistory(Guid pipelineId);
}

public record PipelineEvent(
    Guid PipelineId,
    DateTime Timestamp,
    PipelineEventType Type,
    string Description,
    IReadOnlyDictionary<string, object>? Data = null
);

public enum PipelineEventType
{
    PipelineStarted,
    PipelineCompleted,
    PipelineError,
    StageStarted,
    StageCompleted,
    StageError,
    AgentCalled,
    AgentResponded,
    ValidationPerformed,
    RetryAttempted
}
```

#### AuditTrail

```csharp
public class AuditTrail
{
    private readonly List<AuditEntry> _entries = new();
    private readonly object _lock = new();

    public void Record(AuditEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<AuditEntry> GetEntries(Guid? pipelineId = null)
    {
        lock (_lock)
        {
            return pipelineId.HasValue
                ? _entries.Where(e => e.PipelineId == pipelineId.Value).ToList()
                : _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}

public record AuditEntry(
    Guid Id,
    Guid PipelineId,
    DateTime Timestamp,
    string Action,
    string Actor,
    string Description,
    AuditSeverity Severity,
    IReadOnlyDictionary<string, object>? Details = null
);

public enum AuditSeverity
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}
```

---

## Pipeline d'Exécution

### Flux Complet

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        PIPELINE D'ORCHESTRATION                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  INPUT                                                                  │
│  ├── StoryState (état actuel)                                          │
│  └── NarrativeIntent (ce qu'on veut générer)                           │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  STAGE 1: CONTEXT BUILDER                                              │
│  ├── Récupérer les memorias pertinents (Memory)                        │
│  ├── Construire l'état canonique                                       │
│  ├── Identifier les personnages actifs                                 │
│  ├── Récupérer l'historique récent                                     │
│  └── OUTPUT: NarrativeContext                                          │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  STAGE 2: PROMPT BUILDER                                               │
│  ├── Sélectionner les templates appropriés                             │
│  ├── Injecter les variables du contexte                                │
│  ├── Définir l'ordre d'exécution                                       │
│  └── OUTPUT: PromptSet                                                 │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  STAGE 3: AGENT EXECUTOR                                               │
│  ├── Exécuter chaque agent selon l'ordre                               │
│  │   ├── SummaryAgent (si nécessaire)                                  │
│  │   ├── NarratorAgent                                                 │
│  │   ├── CharacterAgent (si dialogues)                                 │
│  │   └── ConsistencyAgent                                              │
│  ├── Collecter les réponses                                            │
│  └── OUTPUT: RawOutput                                                 │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  STAGE 4: OUTPUT VALIDATOR                                             │
│  ├── Valider la structure                                              │
│  ├── Vérifier la cohérence avec CanonicalState                         │
│  ├── Détecter les contradictions                                       │
│  │   ├── Si INVALIDE → Retry (max 3 fois)                              │
│  │   │   ├── Générer feedback                                          │
│  │   │   ├── Demander réécriture                                       │
│  │   │   └── Revalider                                                 │
│  │   └── Si toujours INVALIDE → FAIL                                   │
│  └── OUTPUT: ValidationResult                                          │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  STAGE 5: STATE INTEGRATOR                                             │
│  ├── Extraire les événements générés                                   │
│  ├── Créer les StateChanges                                            │
│  ├── Générer le Memorandum                                             │
│  └── OUTPUT: NarrativeOutput                                           │
│                                                                         │
│  ════════════════════════════════════════════════════════════════════  │
│                                                                         │
│  OUTPUT                                                                 │
│  ├── NarrativeText (le texte généré)                                   │
│  ├── GeneratedEvents (événements à intégrer)                           │
│  ├── StateChanges (modifications d'état)                               │
│  └── Memorandum (mémoire de ce cycle)                                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Boucle de Retry

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           BOUCLE DE RETRY                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  RawOutput ──────────┐                                                 │
│                      ▼                                                 │
│              ┌───────────────┐                                         │
│              │   Validate    │                                         │
│              └───────┬───────┘                                         │
│                      │                                                 │
│           ┌──────────┴──────────┐                                      │
│           │                     │                                      │
│        VALID               INVALID                                     │
│           │                     │                                      │
│           ▼                     ▼                                      │
│      ┌─────────┐         ┌─────────────┐                               │
│      │ Continue│         │ Retry < 3 ? │                               │
│      │ Pipeline│         └──────┬──────┘                               │
│      └─────────┘                │                                      │
│                      ┌──────────┴──────────┐                           │
│                      │                     │                           │
│                    YES                    NO                           │
│                      │                     │                           │
│                      ▼                     ▼                           │
│              ┌───────────────┐      ┌───────────┐                      │
│              │Build Feedback │      │   FAIL    │                      │
│              └───────┬───────┘      │  Pipeline │                      │
│                      │              └───────────┘                      │
│                      ▼                                                 │
│              ┌───────────────┐                                         │
│              │   Rewrite     │                                         │
│              │   (Agent)     │                                         │
│              └───────┬───────┘                                         │
│                      │                                                 │
│                      └────────────────────────────────────▶ Validate   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Agents Simulés

### Stratégie de Simulation

En Phase 3, tous les agents utilisent `MockLlmClient`. L'objectif est de:

1. **Prouver l'architecture** : Le pipeline fonctionne de bout en bout
2. **Tester la validation** : Les validateurs détectent les erreurs
3. **Vérifier le logging** : Tout est tracé correctement
4. **Mesurer la performance** : Sans overhead LLM réel

### MockSummaryAgent

```csharp
public class MockSummaryAgent : ISummaryAgent
{
    private readonly ILlmClient _llm;
    private readonly ISummaryGenerator _summaryGenerator; // Phase 2

    public AgentType Type => AgentType.Summary;
    public string Name => "MockSummaryAgent";

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        // Utiliser le générateur déterministe de Phase 2 comme base
        var facts = context.RecentEvents
            .SelectMany(e => ExtractFactsFromEvent(e))
            .ToList();

        var summary = _summaryGenerator.SummarizeChapter(facts);

        return Result<AgentResponse>.Ok(new AgentResponse(
            Agent: AgentType.Summary,
            Content: summary,
            Success: true,
            ErrorMessage: null,
            Duration: TimeSpan.FromMilliseconds(50),
            Metadata: new Dictionary<string, object>
            {
                { "fact_count", facts.Count },
                { "mock", true }
            }
        ));
    }

    public bool CanHandle(NarrativeIntent intent) =>
        intent.Type == IntentType.SummarizeChapter;

    // Autres méthodes...
}
```

### MockNarratorAgent

```csharp
public class MockNarratorAgent : INarratorAgent
{
    public AgentType Type => AgentType.Narrator;
    public string Name => "MockNarratorAgent";

    public async Task<Result<AgentResponse>> ProcessAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        // Générer une prose structurellement valide mais générique
        var narrative = BuildMockNarrative(context);

        return Result<AgentResponse>.Ok(new AgentResponse(
            Agent: AgentType.Narrator,
            Content: narrative,
            Success: true,
            ErrorMessage: null,
            Duration: TimeSpan.FromMilliseconds(100),
            Metadata: new Dictionary<string, object> { { "mock", true } }
        ));
    }

    private string BuildMockNarrative(NarrativeContext context)
    {
        var sb = new StringBuilder();

        // Intro basée sur le lieu
        if (context.CurrentLocation != null)
        {
            sb.AppendLine($"In {context.CurrentLocation.Name}, the scene unfolded.");
        }

        // Mentionner les personnages présents
        foreach (var character in context.ActiveCharacters.Take(3))
        {
            sb.AppendLine($"{character.Name} was present, their intentions unclear.");
        }

        // Résumer les événements récents
        sb.AppendLine("Recent events had shaped the current situation.");
        sb.AppendLine($"[{context.RecentEvents.Count} events summarized]");

        // Conclusion générique
        sb.AppendLine("The narrative continued its inexorable progress.");

        return sb.ToString();
    }

    public bool CanHandle(NarrativeIntent intent) =>
        intent.Type is IntentType.ContinueNarrative or IntentType.DescribeScene;

    // Autres méthodes...
}
```

---

## Système de Prompts

### Architecture des Prompts

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       SYSTÈME DE PROMPTS                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  SYSTEM PROMPT                                                  │   │
│  │  ├── Rôle de l'agent                                           │   │
│  │  ├── Règles strictes                                           │   │
│  │  ├── Format de sortie attendu                                  │   │
│  │  └── Interdictions explicites                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  USER PROMPT                                                    │   │
│  │  ├── Contexte actuel (état, personnages, lieu)                 │   │
│  │  ├── Historique récent (résumé)                                │   │
│  │  ├── Intention spécifique                                      │   │
│  │  └── Contraintes additionnelles                                │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  VARIABLES INJECTÉES                                           │   │
│  │  ├── {{character_name}} → Nom du personnage actif              │   │
│  │  ├── {{location_name}} → Lieu actuel                           │   │
│  │  ├── {{recent_summary}} → Résumé des 10 derniers événements    │   │
│  │  ├── {{known_facts}} → Faits établis sur le monde              │   │
│  │  └── {{active_characters}} → Liste des personnages présents    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Exemple de Prompt Complet

```
═══════════════════════════════════════════════════════════════════════════
SYSTEM PROMPT (NarratorAgent)
═══════════════════════════════════════════════════════════════════════════

You are a narrative writer for an interactive story engine.

ROLE:
- Generate descriptive prose that advances the narrative
- Maintain consistency with established facts
- Write in third person, past tense

RULES:
1. NEVER contradict established facts
2. NEVER kill characters without explicit instruction
3. NEVER introduce new characters or locations
4. ALWAYS mention characters by their established names
5. ALWAYS respect character traits and relationships

FORMAT:
- 2-3 paragraphs
- 150-300 words
- End with an open narrative hook

FORBIDDEN:
- Breaking the fourth wall
- Modern anachronisms
- Out-of-character actions

═══════════════════════════════════════════════════════════════════════════
USER PROMPT
═══════════════════════════════════════════════════════════════════════════

CURRENT LOCATION: The Ancient Tower
PRESENT CHARACTERS: Aric (alive, brave), Lyra (alive, wise)
RECENT SUMMARY: Aric and Lyra entered the tower. They discovered ancient
                writings on the walls. A mysterious sound echoed from above.

INTENT: Continue the narrative as they explore the tower.

KNOWN FACTS:
- Aric is brave and determined
- Lyra is wise and cautious
- The tower is ancient and dangerous
- They are searching for the Crystal of Truth

Generate the next scene.

═══════════════════════════════════════════════════════════════════════════
```

---

## Logging et Observabilité

### Structure des Logs

```json
{
  "pipeline_id": "a1b2c3d4-...",
  "timestamp": "2025-12-28T14:30:00Z",
  "event_type": "StageCompleted",
  "stage": "AgentExecutor",
  "duration_ms": 250,
  "details": {
    "agents_called": ["Summary", "Narrator"],
    "total_tokens": 450,
    "retries": 0
  }
}
```

### Métriques Collectées

| Métrique | Description | Objectif Phase 3 |
|----------|-------------|------------------|
| `pipeline_duration_ms` | Durée totale du pipeline | < 2000ms |
| `stage_duration_ms` | Durée par étape | < 500ms chacune |
| `agent_calls_count` | Nombre d'appels aux agents | < 5 par cycle |
| `retry_count` | Nombre de retries | < 2 en moyenne |
| `validation_errors` | Erreurs de validation | < 0.1 par cycle |
| `memory_usage_mb` | Utilisation mémoire | < 100MB |

### Dashboard Cible (ASCII)

```
╔══════════════════════════════════════════════════════════════════════════╗
║                    ORCHESTRATION DASHBOARD                               ║
╠══════════════════════════════════════════════════════════════════════════╣
║                                                                          ║
║  PIPELINE STATUS                                                         ║
║  ├── Last Run: 2025-12-28 14:30:00                                      ║
║  ├── Status: ✅ SUCCESS                                                  ║
║  ├── Duration: 1.2s                                                     ║
║  └── Retries: 0                                                         ║
║                                                                          ║
║  STAGE BREAKDOWN                                                         ║
║  ┌────────────────────┬──────────┬──────────┐                           ║
║  │ Stage              │ Duration │ Status   │                           ║
║  ├────────────────────┼──────────┼──────────┤                           ║
║  │ ContextBuilder     │   50ms   │ ✅       │                           ║
║  │ PromptBuilder      │   20ms   │ ✅       │                           ║
║  │ AgentExecutor      │  900ms   │ ✅       │                           ║
║  │ OutputValidator    │  150ms   │ ✅       │                           ║
║  │ StateIntegrator    │   80ms   │ ✅       │                           ║
║  └────────────────────┴──────────┴──────────┘                           ║
║                                                                          ║
║  AGENTS                                                                  ║
║  ├── SummaryAgent: 200ms (mock)                                         ║
║  ├── NarratorAgent: 500ms (mock)                                        ║
║  └── ConsistencyAgent: 200ms (mock)                                     ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝
```

---

## APIs Publiques

### Entrée de Phase 3 : IOrchestrationService

```csharp
// Setup
var orchestrator = new OrchestrationService(
    contextBuilder,
    promptBuilder,
    agentExecutor,
    outputValidator,
    stateIntegrator,
    pipelineLogger,
    config
);

// Exécuter un cycle narratif
var result = await orchestrator.ExecuteCycleAsync(
    currentState: storyState,
    intent: new NarrativeIntent(IntentType.ContinueNarrative)
);

// Traiter le résultat
result.Match(
    onSuccess: output =>
    {
        Console.WriteLine($"Generated: {output.NarrativeText}");
        Console.WriteLine($"Events: {output.GeneratedEvents.Count}");

        // Intégrer dans l'état (Phase 1)
        foreach (var evt in output.GeneratedEvents)
        {
            storyState = storyState.WithEvent(evt);
        }
    },
    onFailure: error =>
    {
        Console.WriteLine($"Pipeline failed: {error}");
    }
);

// Vérifier le statut
var status = orchestrator.GetStatus();
Console.WriteLine($"Pipeline ready: {status.IsReady}");
Console.WriteLine($"Last run: {status.LastRunAt}");
```

### Configuration

```csharp
public record OrchestrationConfig(
    int MaxRetries = 3,
    TimeSpan StageTimeout = default, // 30 secondes par défaut
    bool EnableDetailedLogging = true,
    bool UseMockAgents = true,       // Phase 3 = true
    MockLlmConfig? MockConfig = null
)
{
    public static OrchestrationConfig Default => new();

    public static OrchestrationConfig ForTesting => new(
        MaxRetries: 1,
        StageTimeout: TimeSpan.FromSeconds(5),
        EnableDetailedLogging: true,
        UseMockAgents: true
    );
}
```

---

## Plan de Développement

### Étape 3.1: Fondations du Pipeline
- [ ] Créer Narratum.Orchestration
- [ ] Implémenter IOrchestrationService
- [ ] Implémenter PipelineContext, PipelineResult
- [ ] Tests: pipeline vide s'exécute sans erreur

### Étape 3.2: Stages du Pipeline
- [ ] Implémenter IContextBuilder + ContextBuilder
- [ ] Implémenter IPromptBuilder + PromptBuilder
- [ ] Implémenter IAgentExecutor + AgentExecutor
- [ ] Implémenter IOutputValidator + OutputValidator
- [ ] Implémenter IStateIntegrator + StateIntegrator
- [ ] Tests: chaque stage individuellement

### Étape 3.3: Abstraction LLM
- [ ] Implémenter ILlmClient
- [ ] Implémenter MockLlmClient
- [ ] Implémenter LlmRequest, LlmResponse
- [ ] Tests: mock répond correctement

### Étape 3.4: Agents Simulés
- [ ] Implémenter ISummaryAgent + MockSummaryAgent
- [ ] Implémenter INarratorAgent + MockNarratorAgent
- [ ] Implémenter ICharacterAgent + MockCharacterAgent
- [ ] Implémenter IConsistencyAgent + MockConsistencyAgent
- [ ] Tests: chaque agent produit une sortie valide

### Étape 3.5: Système de Prompts
- [ ] Implémenter IPromptTemplate
- [ ] Implémenter PromptRegistry
- [ ] Créer templates pour chaque agent
- [ ] Tests: prompts générés correctement

### Étape 3.6: Validation et Retry
- [ ] Implémenter StructureValidator
- [ ] Implémenter CoherenceValidator (intégration Phase 2)
- [ ] Implémenter boucle de retry
- [ ] Tests: retry fonctionne, erreurs détectées

### Étape 3.7: Logging et Observabilité
- [ ] Implémenter IPipelineLogger + PipelineLogger
- [ ] Implémenter AuditTrail
- [ ] Implémenter MetricsCollector
- [ ] Tests: logs complets générés

### Étape 3.8: Intégration Complète
- [ ] Tests d'intégration end-to-end
- [ ] Tests de performance (< 2s par cycle)
- [ ] Tests de robustesse (LLM "stupide")
- [ ] Documentation API complète

---

## Tests et Validation

### Catégories de Tests

#### Tests Unitaires

```csharp
[Fact]
public void ContextBuilder_BuildsValidContext()
{
    // Arrange
    var storyState = CreateTestState();
    var intent = new NarrativeIntent(IntentType.ContinueNarrative);
    var builder = new ContextBuilder(memoryService);

    // Act
    var result = await builder.BuildAsync(storyState, intent);

    // Assert
    Assert.True(result is Result<NarrativeContext>.Success);
    var context = ((Result<NarrativeContext>.Success)result).Value;
    Assert.NotNull(context.CanonicalState);
    Assert.NotEmpty(context.ActiveCharacters);
}

[Fact]
public void MockLlmClient_ReturnsStructuredResponse()
{
    // Arrange
    var client = new MockLlmClient(MockLlmConfig.Default);
    var request = new LlmRequest(
        SystemPrompt: "You are a narrator.",
        UserPrompt: "Describe the scene.",
        Parameters: new LlmParameters()
    );

    // Act
    var result = await client.GenerateAsync(request);

    // Assert
    Assert.True(result is Result<LlmResponse>.Success);
    var response = ((Result<LlmResponse>.Success)result).Value;
    Assert.NotEmpty(response.Content);
    Assert.True(response.Metadata.ContainsKey("mock"));
}

[Fact]
public void OutputValidator_DetectsContradiction()
{
    // Arrange
    var validator = new OutputValidator(coherenceValidator);
    var rawOutput = new RawOutput(/* contient "Aric is dead" */);
    var context = CreateContext(/* Aric est vivant */);

    // Act
    var result = await validator.ValidateAsync(rawOutput, context);

    // Assert
    Assert.False(result.IsValid);
    Assert.NotEmpty(result.Errors);
}
```

#### Tests d'Intégration

```csharp
[Fact]
public async Task Pipeline_ExecutesCompleteCycle()
{
    // Arrange
    var orchestrator = CreateTestOrchestrator();
    var storyState = CreateTestState();
    var intent = new NarrativeIntent(IntentType.ContinueNarrative);

    // Act
    var result = await orchestrator.ExecuteCycleAsync(storyState, intent);

    // Assert
    Assert.True(result is Result<NarrativeOutput>.Success);
    var output = ((Result<NarrativeOutput>.Success)result).Value;
    Assert.NotEmpty(output.NarrativeText);
    Assert.NotNull(output.GeneratedMemorandum);
}

[Fact]
public async Task Pipeline_RetriesOnValidationFailure()
{
    // Arrange
    var orchestrator = CreateTestOrchestrator(
        config: new OrchestrationConfig(MaxRetries: 3)
    );

    // Configurer un agent qui produit une sortie invalide au premier essai
    var mockAgent = new FailFirstMockAgent();

    // Act
    var result = await orchestrator.ExecuteCycleAsync(state, intent);

    // Assert
    Assert.True(result is Result<NarrativeOutput>.Success);
    Assert.Equal(1, mockAgent.RetryCount); // A réessayé une fois
}

[Fact]
public async Task Pipeline_WorksWithStupidLlm()
{
    // Arrange - Le test crucial de Phase 3
    var stupidLlm = new StupidLlmClient(); // Retourne toujours "PLACEHOLDER"
    var orchestrator = CreateOrchestrator(llmClient: stupidLlm);

    // Act
    var result = await orchestrator.ExecuteCycleAsync(state, intent);

    // Assert - Le pipeline doit quand même fonctionner
    Assert.True(result is Result<NarrativeOutput>.Success);
    // La sortie sera "stupide" mais structurellement valide
}
```

#### Tests de Performance

```csharp
[Fact]
public async Task Pipeline_CompletesWithinTimeLimit()
{
    // Arrange
    var orchestrator = CreateTestOrchestrator();
    var stopwatch = Stopwatch.StartNew();

    // Act
    var result = await orchestrator.ExecuteCycleAsync(state, intent);
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 2000);
}

[Fact]
public async Task Pipeline_HandlesLargeContext()
{
    // Arrange
    var largeState = CreateStateWith100Events();
    var orchestrator = CreateTestOrchestrator();

    // Act
    var result = await orchestrator.ExecuteCycleAsync(largeState, intent);

    // Assert
    Assert.True(result is Result<NarrativeOutput>.Success);
}
```

### Critères de Validation

✅ **Fonctionnalité**
- Le pipeline s'exécute de bout en bout
- Tous les stages fonctionnent individuellement
- Les agents produisent des sorties

✅ **Robustesse**
- Le système fonctionne avec MockLlm
- Les erreurs sont gérées gracieusement
- Le retry fonctionne correctement

✅ **Observabilité**
- Tous les événements sont loggés
- Les métriques sont collectées
- L'audit trail est complet

✅ **Performance**
- Cycle complet < 2 secondes
- Chaque stage < 500ms
- Mémoire < 100MB

✅ **Intégration**
- Fonctionne avec Phase 1 (State, Domain)
- Fonctionne avec Phase 2 (Memory)
- Pas de régression

---

## Interdictions Volontaires

### ❌ Pas de LLM Réel
- Aucun appel à Ollama / llama.cpp / API externe
- MockLlmClient uniquement en Phase 3
- Le LLM réel arrive en Phase 4

### ❌ Pas de Décision Autonome
- Un agent ne peut pas modifier l'état directement
- Toutes les actions passent par validation
- L'orchestrateur a le dernier mot

### ❌ Pas de Logique Métier dans les Prompts
- Les prompts sont des templates
- La logique reste dans le code C#
- Les règles sont dans RuleEngine (Phase 1)

### ❌ Pas de Modification du Core
- Phase 1 et Phase 2 restent inchangées
- Orchestration est additive seulement
- Pas de breaking changes

### ❌ Pas de Cache Implicite
- Tous les caches sont explicites
- Invalidation claire
- Reproductibilité garantie

### ❌ Pas de Génération de Texte Libre Non-Validée
- Tout texte généré passe par validation
- Les contradictions sont détectées
- Rien n'est intégré sans vérification

---

## Prochaines Phases (Vue Globale)

### Phase 4 — LLM Minimale
- Remplacer MockLlmClient par OllamaClient
- Premier agent réel : SummaryAgent
- Reste du système identique

### Phase 5 — Narration Contrôlée
- NarratorAgent avec LLM réel
- CharacterAgent avec LLM réel
- LoRA narratif
- Température maîtrisée

### Phase 6 — UI
- Interface utilisateur (Blazor/MAUI/Avalonia)
- API REST
- Export narratif

---

## Conclusion

Phase 3 construit le **système nerveux** de Narratum.

L'orchestration prouve que l'architecture peut:
- Coordonner plusieurs agents
- Valider toutes les sorties
- Récupérer des erreurs
- Tracer chaque décision

**Si le pipeline fonctionne avec un LLM stupide, il fonctionnera avec un LLM intelligent.**

C'est la garantie que nous construisons un système **robuste** et non une démo fragile.

---

**Document Date**: 28 Décembre 2025
**Status**: 📋 DESIGN DOCUMENT
**Next Step**: Étape 3.1 — Fondations du Pipeline
