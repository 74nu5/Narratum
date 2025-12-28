# 🎬 Start Here - Narratum Project Overview

Welcome to **Narratum**, a deterministic narrative engine built with pure .NET 10 and no AI dependencies.

## 🚀 Quick Start

### Current Status
- ✅ **Phase 1.4 COMPLETE**: 49/49 tests passing
- 🏗️ **Architecture**: Hexagonal, fully decoupled
- 📚 **Documentation**: Complete
- ⏳ **Next**: Phase 1.5 Persistence

### Run Tests
```bash
cd d:\Perso\Narratum
dotnet test
```

### Build Project
```bash
dotnet build
```

---

## 📋 What is Narratum?

A **deterministic narrative engine** that:
- ✅ Creates story worlds with rules and invariants
- ✅ Manages characters, locations, and relationships
- ✅ Applies actions with complete validation
- ✅ Enforces 9 narrative rules
- ✅ Generates immutable event history
- ✅ Ensures same input → same output always
- ❌ Uses NO AI or random elements (Phase 1)

---

## 📚 Documentation Map

### Essential Reading (Start Here)
1. 📘 [ARCHITECTURE.md](ARCHITECTURE.md) - How the system is designed
2. 📘 [PHASE1-STATUS.md](PHASE1-STATUS.md) - Current progress summary
3. 📘 [PHASE1-COMPLETION.md](PHASE1-COMPLETION.md) - Detailed Phase 1 overview

### Phase Documentation
- 📘 [Phase1.md](Docs/Phase1.md) - Phase 1 Overview ✅
- 📘 [Phase1-Design.md](Docs/Phase1-Design.md) - Architecture & Design Details
- 📘 [Step1.2-CompletionReport.md](Docs/Step1.2-CompletionReport.md) - Phase 1.2 Core & Domain ✅
- 📘 [Step1.4-RulesEngine-DONE.md](Docs/Step1.4-RulesEngine-DONE.md) - Phase 1.4 Rules Engine ✅
- 📘 [Phase1.5-Persistence-Preparation.md](Docs/Phase1.5-Persistence-Preparation.md) - Next Phase Preview
- 📘 [ROADMAP.md](Docs/ROADMAP.md) - Full 6-Phase Plan

### Additional
- 📘 [CONTRIBUTING.md](CONTRIBUTING.md) - Development Guidelines
- 📘 [Docs/README.md](Docs/README.md) - Documentation Index

---

## 🗂️ Project Structure

```
Narratum/
├── Core/                      # 0 dependencies, abstractions only
│   ├── IStoryRule.cs          # Base contract
│   ├── IRepository.cs         # Persistence contract
│   ├── Id<T>.cs               # Generic identifier
│   ├── Result<T>.cs           # Error handling
│   └── ...more types
│
├── Domain/                    # Business logic entities
│   ├── StoryWorld.cs          # Universe definition
│   ├── Character.cs           # Character entity
│   ├── Location.cs            # Location entity
│   ├── Event*.cs              # Event types (4 types)
│   ├── Relationship.cs        # Character relations
│   └── ...more entities
│
├── State/                     # State management
│   ├── StoryState.cs          # Complete state snapshot
│   ├── CharacterState.cs      # Character state record
│   └── StateSnapshot.cs       # For persistence
│
├── Simulation/                # Actions and services
│   ├── StoryAction.cs         # Base action (7 types)
│   ├── IRule.cs               # Rule interface
│   ├── NarrativeRules.cs      # 9 concrete rules
│   ├── RuleEngine.cs          # Rule orchestration
│   ├── IStateTransitionService.cs
│   ├── StateTransitionService.cs (250+ LOC)
│   ├── IProgressionService.cs
│   └── ProgressionService.cs
│
├── Persistence/               # To be implemented (Phase 1.5)
│   └── (EF Core, SQLite layer)
│
├── Tests/                     # 49 integration tests
│   ├── Phase1Step2*Tests.cs   # 17 tests
│   ├── Phase1Step3*Tests.cs   # 13 tests
│   └── Phase1Step4*Tests.cs   # 19 tests
│
├── Docs/                      # All documentation
│   ├── Phase1.md
│   ├── Phase1-Design.md
│   ├── Step1.4-RulesEngine-DONE.md
│   ├── Phase1.5-Persistence-Preparation.md
│   └── ...more docs
│
└── [Config files]
    ├── Directory.Build.props
    ├── ARCHITECTURE.md
    ├── PHASE1-STATUS.md
    ├── PHASE1-COMPLETION.md
    └── README.md
```

---

## 🎯 Core Concepts

### Immutability
Everything uses **records** and **With* methods**:
```csharp
var newState = state.With(
    currentChapter: newChapter
);
// Original state unchanged
```

### Determinism
Same input → Same output (verified by tests):
```csharp
// Run 100 times with same state + action
// Every result is identical
```

### Result<T> Pattern
Error handling without exceptions:
```csharp
Result<StoryState> ValidateAction(...)
{
    if (!valid) return Result<StoryState>.Failure("reason");
    return Result<StoryState>.Success(newState);
}
```

### Type Safety
Strong typing, no `object` or `dynamic`:
```csharp
// Can't accidentally pass wrong type
Id<Character> characterId = new(Guid.NewGuid());
Id<Location> locationId = new(Guid.NewGuid());
// characterId != locationId (different types)
```

---

## 🧪 Test Coverage

### What's Tested
- ✅ **Domain Entities** (17 tests)
  - Character creation, traits, relations
  - Event generation, immutability
  - World configuration, arcs, chapters

- ✅ **State Management** (13 tests)
  - Action validation for each type
  - State transitions and updates
  - Event history tracking
  - Determinism verification

- ✅ **Rules Engine** (19 tests)
  - Individual rule validation (9 rules)
  - Violation collection
  - Integration with state transitions
  - Complex scenarios

### Test Commands
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter Phase1Step4

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## 🎓 How to Read This Code

### For First-Time Readers

1. **Start with Core**
   - [Core/IStoryRule.cs](Core/IStoryRule.cs) - Base types
   - [Core/Result.cs](Core/Result.cs) - Error pattern

2. **Then Domain**
   - [Domain/StoryWorld.cs](Domain/StoryWorld.cs) - Main entity
   - [Domain/Character.cs](Domain/Character.cs) - Character definition
   - [Domain/Event.cs](Domain/Event.cs) - Base event type

3. **Then State**
   - [State/StoryState.cs](State/StoryState.cs) - State container
   - [State/CharacterState.cs](State/CharacterState.cs) - Character state

4. **Then Actions & Services**
   - [Simulation/StoryAction.cs](Simulation/StoryAction.cs) - Action types
   - [Simulation/StateTransitionService.cs](Simulation/StateTransitionService.cs) - Main service
   - [Simulation/ProgressionService.cs](Simulation/ProgressionService.cs) - Orchestration

5. **Finally Rules**
   - [Simulation/IRule.cs](Simulation/IRule.cs) - Rule interface
   - [Simulation/NarrativeRules.cs](Simulation/NarrativeRules.cs) - Implementations
   - [Simulation/RuleEngine.cs](Simulation/RuleEngine.cs) - Orchestration

### For Architects
- Read [ARCHITECTURE.md](ARCHITECTURE.md) first
- Study [Phase1-Design.md](Docs/Phase1-Design.md) for complete design
- Review tests for integration patterns

### For Implementers
- Check [CONTRIBUTING.md](CONTRIBUTING.md) for coding standards
- Follow established patterns from Phase 1.2-1.4
- All new code must include integration tests

---

## 🏃 Quick Development Guide

### Adding a New Rule (Example: Phase 1.4 Pattern)

1. **Define in NarrativeRules.cs**
```csharp
public class YourNewRule : NarrativeRuleBase
{
    public YourNewRule()
        : base("rule-id", "Rule Name") { }

    public override Result<Unit> Evaluate(StoryState state)
    {
        // Validation logic
        return Result<Unit>.Success(Unit.Default());
    }

    public override Result<Unit> EvaluateForAction(
        StoryState state, StoryAction? action)
    {
        // Action-specific validation
        return Result<Unit>.Success(Unit.Default());
    }
}
```

2. **Add to RuleEngine** (auto-loaded from constructor)
3. **Write tests** in appropriate test file
4. **Verify build and tests**

### Adding a New Action Type (Example: Phase 1.3 Pattern)

1. **Define in StoryAction.cs**
```csharp
public record YourNewAction(
    Guid ActionId,
    // action-specific properties
) : StoryAction(ActionId);
```

2. **Add validation in StateTransitionService**
```csharp
private Result<Unit> ValidateYourNewAction(...)
{
    // Custom validation
    return Result<Unit>.Success(Unit.Default());
}
```

3. **Add action application**
```csharp
private StoryState ApplyYourNewAction(...)
{
    // Return new state with updates
    return state.With(/* updates */);
}
```

4. **Write tests** covering the action
5. **Verify build and tests**

---

## 🚀 Next Steps

### For Phase 1.5 (Persistence)
```bash
# When ready:
"Développe l'étape 1.5 #file:Phase1.md"
```

This will implement:
- Entity Framework Core setup
- SQLite database
- Save/load functionality
- Snapshot serialization
- 14-19 new tests

### Build Skills By
1. Reading existing code
2. Understanding test patterns
3. Following Phase 1 implementations
4. Contributing to Phase 1.5

---

## 📊 Project Health

| Metric | Status |
|--------|--------|
| **Build** | ✅ Clean (0 errors, 0 warnings) |
| **Tests** | ✅ 49/49 Passing |
| **Code Coverage** | ✅ ~95% (all public APIs) |
| **Documentation** | ✅ Complete for Phase 1.4 |
| **Architecture** | ✅ Hexagonal, no cycles |
| **Type Safety** | ✅ Nullable refs enabled |
| **Immutability** | ✅ Records enforced |
| **Determinism** | ✅ Verified by tests |

---

## 🔗 Key Repositories

- **Core Module**: Generic abstractions, zero dependencies
- **Domain Module**: All business logic and entities
- **State Module**: Immutable state management
- **Simulation Module**: Actions, services, rules
- **Tests Module**: 49 integration tests

---

## 💡 Design Principles

1. **No Circular Dependencies** - Every module has clear dependencies
2. **Immutability First** - All state changes are functional
3. **Determinism Guaranteed** - Same input = Same output always
4. **Type Safety** - Generic strong typing throughout
5. **Error Handling** - Result<T> pattern, no exceptions for business logic
6. **Testability** - Everything is testable without mocks
7. **Extensibility** - New rules/actions can be added without changes

---

## ❓ FAQ

**Q: Why no AI in Phase 1?**
A: To build a solid, deterministic foundation that AI can use later.

**Q: Why Records?**
A: Immutability by default, excellent for functional programming patterns.

**Q: Why Result<T>?**
A: Railway-oriented programming, better error handling than exceptions.

**Q: Can I add custom rules?**
A: Yes! Inherit from NarrativeRuleBase and register with RuleEngine.

**Q: Is this production-ready?**
A: Phase 1 is solid for the foundation. Phase 1.5+ add more features.

---

## 📞 Getting Help

- Check [ARCHITECTURE.md](ARCHITECTURE.md) for design questions
- See [CONTRIBUTING.md](CONTRIBUTING.md) for coding guidelines
- Review test files for usage examples
- Read relevant phase completion docs

---

**Welcome to Narratum! Ready to build an amazing narrative engine.**

📌 **Start here**: [ARCHITECTURE.md](ARCHITECTURE.md)
📌 **Current status**: [PHASE1-STATUS.md](PHASE1-STATUS.md)
📌 **Next phase**: [Phase1.5-Persistence-Preparation.md](Docs/Phase1.5-Persistence-Preparation.md)
