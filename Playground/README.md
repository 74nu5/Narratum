# Narratum Playground

Interactive demonstration of the Narratum narrative engine Phase 1 foundation.

## What is this?

The Playground is a console application that showcases all the core functionality built in Phase 1:

- ✅ Story world creation
- ✅ Character management
- ✅ Location definition
- ✅ Story arc creation
- ✅ Narrative state transitions
- ✅ Event recording
- ✅ Rule validation
- ✅ State snapshots

## Running the Demo

```powershell
cd D:\Perso\Narratum
dotnet run --project Playground
```

## What You'll See

The demo displays:

1. **Story World**: "The Hidden Realm" with description
2. **Characters**: Aric, Lyra, and Kael with traits and vital status
3. **Locations**: The Forgotten Tower, Whispering Forest, Crystal Caverns
4. **Story Arc**: "The Dark Discovery" with chapters
5. **Narrative State**: Characters in different locations
6. **Events**: Movement, revelations, and interactions
7. **Time Progression**: Advancing the narrative timeline
8. **Validation**: Rule engine checking story consistency
9. **Snapshots**: Creating and validating state snapshots

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
└── Simulation
```

## Future Enhancements

After Phase 2-3, this could become:

- Interactive menu system
- Save/load story demonstrations
- Rule violation scenarios
- Memory and coherence examples
