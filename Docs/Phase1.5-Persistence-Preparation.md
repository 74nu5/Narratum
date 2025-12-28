# Phase 1.5: Persistence - Getting Started

## Overview

Phase 1.5 implements the persistence layer using:
- **Entity Framework Core** for ORM
- **SQLite** for local development and testing
- **State serialization** for snapshots

## Current Status

✅ **Phase 1.4 Complete**: 49/49 tests passing
✅ **Architecture Ready**: Clean integration points established
✅ **Baseline Stable**: No breaking changes, backward compatible

## What Phase 1.5 Will Deliver

### Database Schema
- SQLite database with tables for:
  - WorldSnapshots
  - CharacterSnapshots
  - LocationSnapshots
  - EventRecords
  - RelationshipSnapshots

### Entity Framework Models
- EF Core DbContext (NarrativumDbContext)
- Entity configurations (FluentAPI)
- Migrations support

### Persistence Service
```csharp
public interface IPersistenceService
{
    Task<Result<Unit>> SaveStateAsync(string filename, StoryState state);
    Task<Result<StoryState>> LoadStateAsync(string filename);
    Task<Result<Unit>> DeleteStateAsync(string filename);
    Task<Result<IReadOnlyList<string>>> ListSavedStatesAsync();
}
```

### Snapshot Service
```csharp
public interface ISnapshotService
{
    StateSnapshot CreateSnapshot(StoryState state);
    StoryState RestoreFromSnapshot(StateSnapshot snapshot);
}
```

## Expected Test Coverage

- **Save/Load Tests** (5-6 tests)
  - Save valid state
  - Load saved state
  - Verify data integrity
  - Handle missing files
  - Handle corrupted data

- **Migration Tests** (3-4 tests)
  - Create database
  - Apply migrations
  - Schema validation
  - Version tracking

- **Serialization Tests** (3-4 tests)
  - Snapshot creation
  - Snapshot restoration
  - Deterministic round-trip
  - Complex state preservation

- **Integration Tests** (3-5 tests)
  - Complete workflow (create → save → load → verify)
  - Multiple save/load cycles
  - Concurrent operations
  - Cleanup operations

**Total Expected**: 14-19 new tests → 63-68 total tests

## Files to Create

```
Persistence/
├── IPersistenceService.cs        # Persistence interface
├── PersistenceService.cs         # Implementation
├── ISnapshotService.cs           # Snapshot interface
├── SnapshotService.cs            # Implementation
├── NarrativumDbContext.cs        # EF DbContext
├── Migrations/
│   └── [Auto-generated]          # EF migrations
└── EntityConfigurations/
    ├── WorldStateConfiguration.cs
    ├── CharacterStateConfiguration.cs
    └── EventConfiguration.cs

Tests/
└── Phase1Step5PersistenceTests.cs # 14-19 new tests
```

## Implementation Strategy

### Step 1: EF Core Setup
1. Add EF Core dependencies (if not present)
2. Create NarrativumDbContext
3. Define entity mappings
4. Generate initial migration

### Step 2: Core Services
1. Implement ISnapshotService
   - Convert StoryState → StateSnapshot
   - Convert StateSnapshot → StoryState
   - Handle nested objects (Characters, Locations, etc.)

2. Implement IPersistenceService
   - Save snapshots to database
   - Load snapshots from database
   - Handle file operations
   - Implement error handling

### Step 3: Testing
1. Create PersistenceTests.cs with:
   - Database initialization tests
   - Save/load round-trip tests
   - Data integrity verification
   - Error scenarios

2. Verify Phase 1.2-1.4 tests still pass

### Step 4: Documentation
1. Update Phase1.md with Phase 1.5 status
2. Create Step1.5-Persistence-DONE.md
3. Update PHASE1-STATUS.md

## Key Decisions to Make

### Q: SQLite vs SQL Server?
**Decision**: SQLite for:
- ✅ Zero setup for development
- ✅ Portable database file
- ✅ Easy for tests (in-memory option)
- ✅ No server required

### Q: Async or Sync APIs?
**Decision**: Async APIs:
- ✅ Prepared for future cloud scenarios
- ✅ No blocking I/O
- ✅ Scalable pattern
- ✅ `async/await` throughout

### Q: Full EF Core or custom serialization?
**Decision**: Hybrid:
- ✅ EF Core for database schema
- ✅ Custom snapshot logic for StoryState
- ✅ Explicit control over serialization
- ✅ Easier to test and debug

### Q: One file or multiple states?
**Decision**: Multiple states:
- ✅ Save multiple game states
- ✅ Named saves (checkpoint-1, auto-save, etc.)
- ✅ Load/delete functionality
- ✅ Replay from different points

## Validation Criteria (Phase 1.5 Done When...)

- ✅ EF Core DbContext created
- ✅ Database schema defined (5+ tables)
- ✅ ISnapshotService implemented
- ✅ IPersistenceService implemented
- ✅ 14-19 tests passing
- ✅ All Phase 1.2-1.4 tests still passing (49+14 = 63+)
- ✅ Save/load round-trip verified
- ✅ Data integrity maintained
- ✅ Documentation updated
- ✅ 0 compilation errors/warnings

## Development Order

1. **Define** → Create interfaces and DbContext
2. **Migrate** → Generate EF Core migration
3. **Implement** → Add service implementations
4. **Test** → Create comprehensive tests
5. **Verify** → Ensure all 49 baseline tests pass
6. **Document** → Update all documentation

## Quick Reference

### Database Initialization
```csharp
// In tests or startup
await context.Database.EnsureCreatedAsync();
// or for migrations
await context.Database.MigrateAsync();
```

### Save State Pattern
```csharp
var snapshot = snapshotService.CreateSnapshot(state);
await persistenceService.SaveStateAsync("game-1", state);
```

### Load State Pattern
```csharp
var result = await persistenceService.LoadStateAsync("game-1");
if (result.IsSuccess)
{
    var loadedState = result.Value;
    // Use loaded state
}
```

## Common Pitfalls to Avoid

❌ Don't forget to handle null values in snapshots
❌ Don't assume all state is serializable
❌ Don't forget to close database connections
❌ Don't skip testing the load path
❌ Don't forget migration naming conventions

## Resources

- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [SQLite with EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [Testing with EF Core](https://learn.microsoft.com/en-us/ef/core/testing/)

## Next Commands

After this Phase 1.5 is complete:

```
"Développe l'étape 1.5 #file:Phase1.md"
```

This will:
1. Create persistence interfaces and services
2. Set up EF Core with SQLite
3. Implement snapshot serialization
4. Add comprehensive tests
5. Update documentation

---

**Status**: Ready to begin Phase 1.5 ✅
**Baseline Tests**: 49/49 passing
**Build**: Clean, 0 errors
**Next Step**: Create Persistence/IPersistenceService.cs
