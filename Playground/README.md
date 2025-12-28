# Narratum Playground

Interactive demonstration of the Narratum narrative engine Phase 1 + Phase 2 capabilities.

## What is this?

The Playground is a console application that showcases all core functionality built in Phase 1 and Phase 2:

**Phase 1 Foundation**:
- ✅ Story world creation
- ✅ Character management
- ✅ Location definition
- ✅ Story arc creation
- ✅ Narrative state transitions
- ✅ Event recording
- ✅ Rule validation
- ✅ State snapshots

**Phase 2 Memory System**:
- ✅ Fact extraction from events
- ✅ Memory chapter creation
- ✅ Narrative summarization
- ✅ Coherence validation
- ✅ Entity tracking
- ✅ Canonical state aggregation

## Running the Demo

```powershell
cd D:\Perso\Narratum
dotnet run --project Playground
```

## What You'll See

**Phase 1 Narrative Foundation**:
1. **Story World**: "The Hidden Realm" with description
2. **Characters**: Aric, Lyra, and Kael with traits and vital status
3. **Locations**: The Forgotten Tower, Whispering Forest, Crystal Caverns
4. **Story Arc**: "The Dark Discovery" with 3 story chapters
5. **Chapter 1**: The Beginning - Discovery and alliance formation
6. **Chapter 2**: The Turning Point - Quest escalation and combat
7. **Chapter 3**: The Revelation - Sacrifice and conclusion
8. **Rule Validation**: Story consistency checks at each milestone
9. **State Snapshots**: Timeline saves with integrity hashes

**Phase 2 Memory System**:
1. **Fact Extraction**: 10 narrative events → 34 extracted facts
2. **Event Processing**: Event-by-event fact identification
3. **Memory Chapters**: 4 thematic story chapters created
4. **Narrative Summary**: Chapter-level summaries generated
5. **Coherence Validation**: Narrative consistency checking
   - Detects contradictions (e.g., character both alive and dead)
   - Validates relationship continuity
6. **Canonical State**: Complete world model aggregation
   - Time progression tracking
   - Character state consistency
   - Entity relationship preservation

## Output Format

Uses **Spectre.Console** for beautiful formatted output with:

- Colored text and emphasis
- Formatted tables for data display
- Progress indicators
- Styled panels for summaries

## Architecture

```
Playground (Console App)
    ↓ references
├── Core
├── Domain
├── State
├── Persistence
├── Simulation
└── Memory (Phase 2)
    ├── Services
    ├── Models
    ├── Extraction
    └── Coherence
```

## Future Enhancements

Phase 3+ enhancements:

- **Agent Integration**: Narrative agents that use memory to reason about stories
- **LLM Integration**: Large language models for fact extraction and summarization
- **Interactive Mode**: User-driven narrative branching
- **Multi-World Simulation**: Stories spanning multiple worlds with shared history
- **Temporal Analysis**: Timeline visualization and manipulation
- **Relationship Networks**: Visual representation of character relationships
- **Contradiction Resolution**: Interactive conflict resolution system
