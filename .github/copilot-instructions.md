# Narratum - GitHub Copilot Instructions

Narratum is a deterministic narrative engine built in .NET 10 following hexagonal architecture principles. The project evolves in strict phases, building solid foundations before adding AI features.

## Build, Test, and Lint Commands

### Build
```bash
# Build entire solution
dotnet build Narratum.sln -c Debug

# Build specific project
dotnet build Core -c Debug
dotnet build Memory -c Debug
dotnet build Orchestration -c Debug
```

### Test
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Tests -c Debug --no-build
dotnet test Memory.Tests -c Debug --no-build
dotnet test Orchestration.Tests -c Debug --no-build

# Run specific test class
dotnet test Memory.Tests --filter "FactExtractorServiceTests" -c Debug --no-build

# Run specific test method
dotnet test Tests --filter "ExtractFromEvent_IsDeterministic_SameFacts" -c Debug --no-build

# Run with verbose output
dotnet test -v normal
```

### Code Quality
- **TreatWarningsAsErrors**: `true` - The build will fail on any warning
- **EnforceCodeStyleInBuild**: `true` - Code style is enforced at build time
- **AnalysisLevel**: `latest` - Using latest code analyzers
- No separate linting command - analysis runs during build

## High-Level Architecture

### Hexagonal Architecture (Ports & Adapters)

The project follows strict layered architecture with concentric circles:

```
Tests → Simulation → Rules → State → Domain → Core
Tests → Persistence → State, Domain
Orchestration → Memory, Domain, Core
```

**Core (Center)**
- Pure abstractions and interfaces
- Zero dependencies
- Contains: `Id`, `DomainEvent`, `Result<T>`, `IRepository`, `IStoryRule`

**Domain**
- Business logic
- Events: `CharacterDeathEvent`, `CharacterMovedEvent`, `CharacterEncounterEvent`
- Entities: Characters, Locations, Relationships

**State**
- Immutable state management
- State transitions and snapshots
- No mutations - only replacements

**Rules**
- Narrative rule engine
- 9 rules validating story consistency (e.g., `CharacterMustBeAliveRule`, `TimeMonotonicityRule`)
- All rules are deterministic and composable

**Simulation**
- Orchestrates simulation execution
- Manages time progression
- Coordinates modules

**Persistence**
- EF Core with SQLite
- Serialization/deserialization
- State saving/loading

**Memory** (Phase 2+)
- Fact extraction from events
- Canonical memory store
- Temporal tracking

**Orchestration** (Phase 2+)
- Multi-agent pipeline
- Specialized agents: Narrator, Character, Summary, Consistency
- Prompt generation

### Module Dependencies

**Never create circular dependencies.** Dependencies flow inward toward Core:
- Outer layers can depend on inner layers
- Inner layers never depend on outer layers
- Domain knows nothing about Persistence, Simulation, or Rules
- Core has zero dependencies on anything

### Data Flow

1. Inputs (commands, events) → Ports
2. Simulation orchestrates processing
3. Rules evaluate applicable rules
4. State updated immutably
5. Results persisted via Persistence adapters
6. Outputs returned via ports

## Key Conventions

### Determinism is Mandatory

**All operations must be deterministic** - same inputs always produce same outputs:

- ❌ Never use unseeded random generators
- ❌ Never access system clock directly in business logic
- ❌ Never rely on non-deterministic external I/O in domain layer
- ✅ Use seeded random via `Random(seed)` when needed
- ✅ Pass timestamps as parameters
- ✅ Ensure collection ordering is stable (OrderBy, stable sorts)

**Testing determinism:**
```csharp
// Always verify deterministic operations
var result1 = service.Process(input);
var result2 = service.Process(input);
Assert.Equal(result1, result2); // Must be identical
```

### Immutability

**State is never modified, only replaced:**

```csharp
// ❌ Wrong - mutation
state.Characters.Add(newCharacter);

// ✅ Correct - replacement
var newState = state with { 
    Characters = state.Characters.Append(newCharacter).ToImmutableList() 
};
```

### ID System

IDs are typed records wrapping Guid:

```csharp
// ✅ Correct
var id = new Id(Guid.NewGuid());
var guidString = id.Value.ToString();

// ❌ Wrong - Id is not a string
var id = new Id("some-string");
```

When using entity name maps (e.g., in Memory module):
```csharp
// ✅ Correct - key is Guid.ToString()
EntityNameMap: new Dictionary<string, string>
{
    { characterGuid.ToString(), "Aric" }
}

// For name resolution
var name = context.GetEntityName(event.ActorIds[0].Value.ToString());
```

### No AI/LLM in Phase 1

Phase 1 (current) is **deliberately LLM-free:**

- ❌ No LangChain, Semantic Kernel, or similar frameworks
- ❌ No calls to OpenAI, Anthropic, or other LLM APIs
- ✅ Pure algorithmic narrative engine
- ✅ AI integration comes in Phase 4-5

### Project Structure Conventions

- Each module is a separate project under solution folders
- Tests are colocated: `Tests/`, `Memory.Tests/`, `Orchestration.Tests/`
- Use `Directory.Build.props` for shared build settings
- XML documentation required for public APIs (warning suppression via NoWarn 1591)

### Code Style

- **Nullable reference types**: Enabled globally
- **Implicit usings**: Enabled
- **Latest C# language features**: Use them
- **File-scoped namespaces**: Preferred
- **Record types**: Use for immutable data (events, value objects)
- **FluentAssertions**: Use in tests for better readability

### Testing Patterns

**Test framework**: xUnit + FluentAssertions + NSubstitute

```csharp
// Naming: MethodName_Scenario_ExpectedBehavior
[Fact]
public void ExtractFromEvent_IsDeterministic_SameFacts()
{
    // Arrange
    var service = CreateService();
    
    // Act
    var facts1 = service.ExtractFromEvent(event, context);
    var facts2 = service.ExtractFromEvent(event, context);
    
    // Assert
    facts1.Should().BeEquivalentTo(facts2);
}
```

**Determinism tests are critical** - always verify operations produce identical results on repeated calls.

### Orchestration Philosophy (Phase 2+)

**The application orchestrates, not the LLM:**

- Business logic stays in Core/Domain
- LLMs are generation engines, not the decision-makers
- Prompts are dynamically generated by the application
- Agents are adapters, not the brain
- Pipeline controls flow, not agent chaining

### Documentation

- Phase-based documentation in `Docs/` folder
- Phase completion reports (e.g., `PHASE2.2-COMPLETION.md`)
- Commands guides for each phase (e.g., `PHASE2.2-COMMANDS-GUIDE.md`)
- Architecture decisions documented in `ARCHITECTURE.md`
- Quick reference in `QUICK-REFERENCE.md`

### Anti-Patterns to Avoid

❌ **Never add technical debt** - this project values long-term quality over quick results  
❌ **Never bypass architecture layers** - respect hexagonal boundaries  
❌ **Never add LLM dependencies in Phase 1** - foundations first  
❌ **Never use mutable state** - immutability is non-negotiable  
❌ **Never skip determinism validation** - test it explicitly  
❌ **Never introduce circular dependencies** - check dependency flow  

### Development Philosophy

This project follows an **anti-shortcut** approach:

> "Retarder volontairement le plaisir du résultat visible pour construire quelque chose qui dure."

Translated: *Deliberately delay the pleasure of visible results to build something that lasts.*

**Priorities:**
1. ✅ Clean architecture before features
2. ✅ Tests > demos
3. ✅ Guaranteed determinism
4. ✅ Zero technical debt
5. ✅ Follow-through to completion

When in doubt:
- Choose the architecturally sound approach over the quick fix
- Add tests before declaring something "done"
- Verify determinism explicitly
- Document non-obvious design decisions
