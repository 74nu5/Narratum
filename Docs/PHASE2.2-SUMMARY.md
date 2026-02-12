# Phase 2.2 - Quick Reference Guide

## What is Phase 2.2?

**Extraction Layer** - Converts domain events into atomic facts that can be stored in memory.

**Key Property:** Determinism - Same event always produces the same facts.

## How It Works

```
Domain Event (CharacterDeathEvent, etc.)
    ↓
IFactExtractor Service
    ↓
Specialized Extractors (1 per event type)
    ↓
IReadOnlyList<Fact> (deduplicated, sorted)
```

## Core Classes

### FactExtractorService
**Main orchestrator**
```csharp
var service = new FactExtractorService(extractors);
var facts = service.ExtractFromEvent(deathEvent, context);
// Results: IReadOnlyList<Fact>
```

### EventExtractorContext
**Provides world state and entity mapping**
```csharp
var context = new EventExtractorContext(
    WorldId: Guid.NewGuid(),
    EventTimestamp: DateTime.UtcNow,
    EntityNameMap: new Dictionary<string, string>
    {
        { characterGuid.ToString(), "Aric" }
    }
);
```

## Supported Event Types

| Event Type | Facts Produced | Example |
|---|---|---|
| CharacterDeathEvent | 1 | "Aric died" |
| CharacterMovedEvent | 2 | "Aric moved...", "Aric is at..." |
| CharacterEncounterEvent | 2 | "Aric and Lyra met...", "Aric knows Lyra" |

## Important Properties

✅ **Deterministic** - Same input → Same output (guaranteed)
✅ **Deduplicating** - Removes identical facts across multiple events
✅ **Context-Aware** - Resolves entity names from mappings
✅ **Immutable** - Returns sealed records
✅ **Extensible** - Add new extractors without modifying existing code

## Test Coverage

- 15 new tests for Phase 2.2
- 47 existing tests from Phase 2.1
- **Total: 62/62 passing (100%)**

## Key Design Decisions

1. **Strategy Pattern** - Pluggable extractors per event type
2. **Deterministic Ordering** - Sort by Content, then Id
3. **Deduplication** - Handled at service level
4. **Entity Resolution** - Guid → String mapping with fallback
5. **Confidence Scoring** - 1.0 for facts, 0.8 for inferred

## Files

```
Memory/
  Services/
    ├── IFactExtractor.cs (interface + context)
    └── FactExtractorService.cs (implementation)

Memory.Tests/
  └── FactExtractorServiceTests.cs (15 tests)
```

## Quick Start

```csharp
// 1. Create extractors
var extractors = new IEventFactExtractor[]
{
    new CharacterDeathEventExtractor(),
    new CharacterMovedEventExtractor(),
    new CharacterEncounterEventExtractor()
};

// 2. Create service
var service = new FactExtractorService(extractors);

// 3. Create context with entity names
var context = new EventExtractorContext(
    Guid.NewGuid(), DateTime.UtcNow,
    new Dictionary<string, string> { { id.ToString(), "Name" } }
);

// 4. Extract facts
var facts = service.ExtractFromEvent(anyEvent, context);
```

## Next Steps (Phase 2.3)

- Store extracted facts in CanonicalState (memory)
- Build temporal tracking
- Detect conflicts
- Infer relationships

---

**Status:** ✅ Complete - 62/62 tests passing
