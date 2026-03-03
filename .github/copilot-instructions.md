# Narratum - GitHub Copilot Instructions

Narratum is a deterministic narrative engine built in .NET 10 following hexagonal architecture principles. It has a Blazor Server UI, a local-LLM integration layer (Foundry Local / Ollama), and a multi-agent orchestration pipeline. **Phases 1–5 are complete** (894 tests passing).

## Build, Test, and Lint Commands

```bash
# Build entire solution
dotnet build Narratum.sln -c Debug

# Run all tests
dotnet test Narratum.sln -c Debug --no-build

# Run a specific test project
dotnet test Tests -c Debug --no-build
dotnet test Memory.Tests -c Debug --no-build
dotnet test Orchestration.Tests -c Debug --no-build
dotnet test Llm.Tests -c Debug --no-build

# Run a specific test class
dotnet test Memory.Tests --filter "FactExtractorServiceTests" -c Debug --no-build

# Run a specific test method
dotnet test Tests --filter "ExtractFromEvent_IsDeterministic_SameFacts" -c Debug --no-build
```

**Code Quality** — no separate lint step; analysis runs during build:
- `TreatWarningsAsErrors=true` — build fails on any warning
- `EnforceCodeStyleInBuild=true` — code style enforced at build time
- `AnalysisLevel=latest` — latest Roslyn analyzers active

## High-Level Architecture

### Module Dependency Graph

Dependencies flow **inward toward Core only** — never outward, never circular:

```
Web → Orchestration, Persistence, Memory
Llm → Orchestration                         (Llm depends on Orchestration.Stages for AgentType)
Orchestration → Memory, Domain, Core
Memory → Domain, Core
Simulation → Rules → State → Domain → Core
Persistence → State, Domain, Core
Tests / *.Tests → all modules under test
```

### Modules

| Module | Responsibility |
|---|---|
| **Core** | Pure abstractions — `Id`, `DomainEvent`, `Result<T>`, `IRepository`, `IStoryRule`. Zero dependencies. |
| **Domain** | Business entities & events — `Character`, `Location`, `CharacterDeathEvent`, `CharacterMovedEvent`, etc. |
| **State** | Immutable world/character/story state snapshots and transitions. |
| **Rules** | 9 deterministic narrative rules (e.g., `CharacterMustBeAliveRule`, `TimeMonotonicityRule`). |
| **Simulation** | Orchestrates the simulation loop — time progression, event dispatch. |
| **Persistence** | EF Core + SQLite. `SnapshotService` saves/loads world state. `PageSnapshotEntity` stores generated pages. |
| **Memory** | Fact extraction from events, canonical memory store, 4-level hierarchical summaries. LLM-free. |
| **Orchestration** | Multi-agent pipeline — `NarratorAgent`, `CharacterAgent`, `SummaryAgent`, `ConsistencyAgent`. The application controls flow; LLMs are generation engines. |
| **Llm** | `ILlmClient` abstraction over `IChatClient` (Microsoft.Extensions.AI). Supports Foundry Local and Ollama. |
| **Web** | Blazor Server UI — wizard, generation page, reader, story library, config, expert mode. |

### Data Flow (Generation)

```
Web (Blazor) → GenerationService → Orchestration Pipeline
  → ContextBuilder (Memory facts + State)
  → PromptBuilder (deterministic prompt assembly)
  → AgentExecutor → LlmClientFactory → IChatClient (local model)
  → OutputValidator → StateIntegrator
  → Persistence (PageSnapshotEntity auto-save)
```

## Key Conventions

### Determinism is Mandatory

**Same inputs must always produce same outputs** in all non-LLM code:

- ❌ No unseeded random generators
- ❌ No direct `DateTime.Now` / `DateTime.UtcNow` in business logic — pass timestamps as parameters
- ❌ No non-deterministic collection ordering — use `OrderBy` with stable keys
- ✅ `Random(seed)` when randomness is needed

Always write an explicit determinism test for any new service:
```csharp
var result1 = service.Process(input);
var result2 = service.Process(input);
result1.Should().BeEquivalentTo(result2);
```

### Immutability

State is **never mutated, only replaced**:

```csharp
// ❌ Wrong
state.Characters.Add(newCharacter);

// ✅ Correct
var newState = state with {
    Characters = state.Characters.Append(newCharacter).ToImmutableList()
};
```

`CharacterState` and `WorldState` are records — use `Func<T, T>` not `Action<T>` for modifications.

### Result<T> Pattern

`Result<T>` uses discriminated union pattern matching — there is **no `.IsSuccess` or `.Error` property**:

```csharp
// ✅ Correct — use Match or switch expression
var output = result.Match(
    onSuccess: value => value.ToString(),
    onFailure: msg => $"Error: {msg}"
);

// Or pattern match
if (result is Result<T>.Success s) { ... }
if (result is Result<T>.Failure f) { ... }
```

### ID System

`Id` is a typed record wrapping `Guid`, not a string:

```csharp
var id = new Id(Guid.NewGuid());
var guidString = id.Value.ToString();   // ✅ Id.Value is Guid
```

Entity name map keys are `Guid.ToString()`:
```csharp
EntityNameMap: new Dictionary<string, string> { { characterGuid.ToString(), "Aric" } }
var name = context.GetEntityName(event.ActorIds[0].Value.ToString());
```

### Llm Module

`LlmClientConfig` is a **sealed record** (immutable). Registered as singleton. To change the model at runtime use `IModelResolver`, not mutation.

`LlmClientFactory` implements **`IAsyncDisposable` only** — always `await using`, never `using`:
```csharp
await using var factory = new LlmClientFactory(config);  // ✅
using var factory = new LlmClientFactory(config);          // ❌ IDISP001/IDISP003 error
```

`LlmProviderType` supports `FoundryLocal` and `Ollama`. Model is resolved per `AgentType` via `LlmClientConfig.ResolveModel(agentType)` with priority: `NarratorModel` > `AgentModelMapping` > `DefaultModel`.

When working with `Microsoft.Extensions.AI` (version 10.2.0), `ChatResponse.Usage` is of type `UsageDetails` (not `ChatResponseUsage`).

### Orchestration Philosophy

**The application orchestrates, not the LLM:**
- All business logic stays in Core/Domain/Rules
- LLMs only receive structured prompts and return text — they make no decisions
- Agents (`INarratorAgent`, `ICharacterAgent`, etc.) are adapters
- `Pipeline` controls stage sequencing: `ContextBuilder` → `PromptBuilder` → `AgentExecutor` → `OutputValidator` → `StateIntegrator`

### Persistence Notes

`SnapshotService.DeserializeCharacterStates`, `DeserializeEvents`, and `DeserializeWorldState` are **Phase 1.5 stubs** that return empty collections. `LoadStateAsync` does **not** actually restore characters, events, or world. These must be implemented before any feature that depends on persisted state restore.

### Testing Patterns

**Stack**: xUnit + FluentAssertions + NSubstitute. 4 test projects: `Tests`, `Memory.Tests`, `Orchestration.Tests`, `Llm.Tests`.

Naming: `MethodName_Scenario_ExpectedBehavior`

```csharp
[Fact]
public void ExtractFromEvent_IsDeterministic_SameFacts()
{
    // Arrange / Act / Assert
    facts1.Should().BeEquivalentTo(facts2);
}
```

### Code Style

- File-scoped namespaces preferred
- Record types for all immutable data (events, value objects, config)
- Nullable reference types enabled globally
- XML doc comments required on public APIs (`GenerateDocumentationFile=true`; CS1591 suppressed so missing docs are warnings-not-errors)
- Implicit usings enabled

### Anti-Patterns

❌ Never bypass hexagonal layer boundaries  
❌ Never mutate state — only replace  
❌ Never use non-deterministic operations in Core/Domain/Memory/Orchestration  
❌ Never put business logic in prompts or let the LLM decide narrative structure  
❌ Never introduce circular project dependencies  
❌ Never use `using` (sync dispose) on `LlmClientFactory` — it is `IAsyncDisposable` only
