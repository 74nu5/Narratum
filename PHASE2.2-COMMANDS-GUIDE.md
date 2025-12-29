# Phase 2.2 - Commands & Build Guide

## Build Commands

### Compile the Memory project
```bash
cd d:\Perso\Narratum
dotnet build Memory -c Debug
```

### Compile the Memory.Tests project
```bash
cd d:\Perso\Narratum
dotnet build Memory.Tests -c Debug
```

### Compile entire solution
```bash
cd d:\Perso\Narratum
dotnet build Narratum.sln -c Debug
```

## Test Commands

### Run all Memory.Tests
```bash
cd d:\Perso\Narratum
dotnet test Memory.Tests -c Debug --no-build
```

### Run specific test class
```bash
cd d:\Perso\Narratum
dotnet test Memory.Tests --filter "FactExtractorServiceTests" -c Debug --no-build
```

### Run specific test method
```bash
cd d:\Perso\Narratum
dotnet test Memory.Tests --filter "ExtractFromEvent_IsDeterministic_SameFacts" -c Debug --no-build
```

### Run with verbose output
```bash
cd d:\Perso\Narratum
dotnet test Memory.Tests -c Debug --no-build -v normal
```

## Project Structure

```
d:\Perso\Narratum\
├── Memory\
│   ├── Narratum.Memory.csproj
│   ├── Models\
│   ├── Services\
│   │   ├── IFactExtractor.cs
│   │   └── FactExtractorService.cs
│   └── MemoryEnums.cs
│
├── Memory.Tests\
│   ├── Memory.Tests.csproj
│   ├── Usings.cs
│   ├── FactExtractorServiceTests.cs
│   └── [other test files from Phase 2.1]
│
├── Domain\
│   └── Event.cs (CharacterDeathEvent, CharacterMovedEvent, CharacterEncounterEvent)
│
├── Core\
│   └── Id.cs (Guid-based identifier)
│
└── Narratum.sln
```

## File Creation Timeline

### Step 1: Create IFactExtractor interface
```csharp
// d:\Perso\Narratum\Memory\Services\IFactExtractor.cs
- EventExtractorContext record
- IFactExtractor interface
- ~95 lines
```

### Step 2: Create FactExtractorService
```csharp
// d:\Perso\Narratum\Memory\Services\FactExtractorService.cs
- FactExtractorService main class
- CharacterDeathEventExtractor
- CharacterMovedEventExtractor
- CharacterEncounterEventExtractor
- ~260 lines
```

### Step 3: Create comprehensive tests
```csharp
// d:\Perso\Narratum\Memory.Tests\FactExtractorServiceTests.cs
- 15 test methods
- Determinism verification
- Deduplication tests
- Entity name resolution tests
- ~410 lines
```

### Step 4: Create documentation
```markdown
- PHASE2.2-COMPLETION.md (detailed report)
- PHASE2.2-SUMMARY.md (quick reference)
- PHASE2.2-DONE.md (status summary)
```

## Compilation Results

### Initial Compilation (After creating FactExtractorService.cs)
```
Error: CharacterDeathEvent is not found
Reason: Missing usings in FactExtractorService.cs
Fix: Added using Narratum.Core; and using Narratum.Domain;
```

### Test Compilation (Initial)
```
Errors: ID conversion errors
Reason: Id is a record with Guid Value, not a string
Fix: Used Id(Guid) constructor instead of Id(string)
       Updated tests to use new Guid() calls
```

### Test Execution (Initial)
```
4 tests failed
Reason: Entity name resolution using wrong string conversions
Fix: Changed deathEvent.ActorIds[0].ToString() 
     to deathEvent.ActorIds[0].Value.ToString()
     Applied same fix to all extractors
```

### Final Compilation
```
✅ SUCCESS
Memory: Narratum.Memory net10.0 succeeded
Memory.Tests: Memory.Tests net10.0 succeeded
0 errors, 0 warnings
```

### Final Test Execution
```
✅ SUCCESS
62 tests passed
0 tests failed
100% success rate
```

## Development Workflow

### 1. Create interface
- Define IFactExtractor contract
- Create EventExtractorContext
- Document with XML comments

### 2. Implement main service
- Create FactExtractorService class
- Route events to extractors
- Implement deduplication logic
- Ensure deterministic ordering

### 3. Implement extractors
- CharacterDeathEventExtractor
- CharacterMovedEventExtractor
- CharacterEncounterEventExtractor

### 4. Write tests
- Test each extractor independently
- Test determinism guarantees
- Test deduplication
- Test entity name resolution
- Test fallback behavior

### 5. Debug and fix
- Fix import errors
- Fix type conversion issues
- Verify determinism
- Ensure all tests pass

### 6. Document
- Write completion report
- Create quick reference
- Document design decisions
- Provide usage examples

## Useful Patterns

### Creating an event with entity mapping
```csharp
var characterGuid = Guid.NewGuid();
var characterId = new Id(characterGuid);
var contextMap = new Dictionary<string, string>
{
    { characterGuid.ToString(), "Aric" }
};
var context = new EventExtractorContext(
    Guid.NewGuid(), 
    DateTime.UtcNow, 
    contextMap
);
```

### Testing determinism
```csharp
var facts1 = service.ExtractFromEvent(event, context);
var facts2 = service.ExtractFromEvent(event, context);

Assert.Equal(facts1.Count, facts2.Count);
for (int i = 0; i < facts1.Count; i++)
{
    Assert.Equal(facts1[i].Content, facts2[i].Content);
    Assert.Equal(facts1[i].FactType, facts2[i].FactType);
}
```

### Creating service with extractors
```csharp
var extractors = new IEventFactExtractor[]
{
    new CharacterDeathEventExtractor(),
    new CharacterMovedEventExtractor(),
    new CharacterEncounterEventExtractor()
};
var service = new FactExtractorService(extractors);
```

## Troubleshooting

### Build Error: "Type X is not found"
**Solution:** Add missing using statement
```csharp
using Narratum.Domain;
using Narratum.Core;
```

### Build Error: "Cannot convert from string to Guid"
**Solution:** Use correct constructor
```csharp
// ❌ Wrong
var id = new Id("some-string");

// ✅ Correct
var id = new Id(Guid.NewGuid());
```

### Test Failure: Name not resolved
**Solution:** Ensure EntityNameMap uses Guid strings
```csharp
// ❌ Wrong
EntityNameMap: new Dictionary<string, string>
{
    { "char1", "Aric" }  // "char1" is a string literal
}

// ✅ Correct
EntityNameMap: new Dictionary<string, string>
{
    { characterGuid.ToString(), "Aric" }  // Guid as string
}
```

### Test Failure: Facts contain "Character_Id { Value = ..."
**Solution:** Use `.Value.ToString()` for ID conversion
```csharp
// ❌ Wrong
var name = context.GetEntityName(deathEvent.ActorIds[0].ToString());
// Returns: "Id { Value = 12345... }"

// ✅ Correct  
var name = context.GetEntityName(deathEvent.ActorIds[0].Value.ToString());
// Returns: "Aric" (or generates "Character_12345...")
```

## Performance Tips

- Service creation is cheap (just dictionary setup)
- Event extraction is O(1) per event (plus sorting)
- Deduplication is O(n log n) for n facts
- Use single service instance for multiple events

## Next Steps for Phase 2.3

1. Create MemoryService class
2. Implement fact storage in CanonicalState
3. Create temporal tracking
4. Add conflict detection

---

*Phase 2.2 Development Commands Guide*
