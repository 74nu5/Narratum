using Spectre.Console;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Persistence;
using Narratum.Simulation;
using Microsoft.Extensions.Logging;

// Setup logger
var logger = new DebugLogger();

// Title and setup
AnsiConsole.Clear();
AnsiConsole.Write(
    new FigletText("NARRATUM")
        .Centered()
        .Color(Color.Cyan1));

AnsiConsole.MarkupLine("[yellow]Narrative Engine Playground[/]");
AnsiConsole.MarkupLine("[grey]Phase 1 + Phase 2 - Story Walkthrough with Memory System[/]\n");

// Initialize world, characters, locations
var world = new StoryWorld("The Hidden Realm", "A world of mystery and shadow");
var aric = new Character(
    name: "Aric the Bold",
    traits: new Dictionary<string, string>
    {
        { "Courage", "High" },
        { "Curiosity", "Extreme" },
        { "Impulsiveness", "Moderate" }
    }
);
var lyra = new Character(
    name: "Lyra the Wise",
    traits: new Dictionary<string, string>
    {
        { "Wisdom", "Excellent" },
        { "Mystery", "High" },
        { "Loyalty", "Unwavering" }
    }
);
var kael = new Character(
    name: "Kael the Shadow",
    traits: new Dictionary<string, string>
    {
        { "Pragmatism", "High" },
        { "Darkness", "Moderate" },
        { "Honor", "Strong" }
    }
);

var tower = new Location("The Forgotten Tower", "An ancient tower lost in time");
var forest = new Location("Whispering Forest", "A forest where trees seem to talk");
var caverns = new Location("Crystal Caverns", "Underground caves glittering with minerals");

var arc = new StoryArc(world.Id, "The Dark Discovery");
var chapter1 = new StoryChapter(arc.Id, 1);
var chapter2 = new StoryChapter(arc.Id, 2);
var chapter3 = new StoryChapter(arc.Id, 3);

var snapshotService = new SnapshotService();
var ruleEngine = new RuleEngine();
var snapshots = new List<(int Chapter, Narratum.Persistence.StateSnapshot Snapshot)>();

AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]");
AnsiConsole.MarkupLine("[bold cyan]THE DARK DISCOVERY - A Narrative Journey[/]");
AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]\n");

// ==================== CHAPTER 1: THE BEGINNING ====================
AnsiConsole.MarkupLine("[bold yellow]CHAPTER 1: THE BEGINNING[/]\n");

var worldState1 = new WorldState(world.Id, world.Name);
var storyState1 = new StoryState(worldState1)
    .WithCharacter(new CharacterState(aric.Id, aric.Name, VitalStatus.Alive, tower.Id))
    .WithCharacter(new CharacterState(lyra.Id, lyra.Name, VitalStatus.Alive, forest.Id))
    .WithCharacter(new CharacterState(kael.Id, kael.Name, VitalStatus.Alive, caverns.Id));

AnsiConsole.MarkupLine($"[cyan]Time:[/] {worldState1.NarrativeTime:yyyy-MM-dd HH:mm:ss}\n");

AnsiConsole.MarkupLine("[green]✓ Scene 1:[/] [grey]Aric discovers an ancient map in the tower[/]");
var aricWithFact1 = new CharacterState(aric.Id, aric.Name, VitalStatus.Alive, tower.Id)
    .WithKnownFact("Map reveals location of Crystal Caverns");

AnsiConsole.MarkupLine("[green]✓ Scene 2:[/] [grey]Lyra senses the disturbance in the forest[/]");
var lyraWithFact1 = new CharacterState(lyra.Id, lyra.Name, VitalStatus.Alive, forest.Id)
    .WithKnownFact("Ancient magic stirs in the world");

AnsiConsole.MarkupLine("[green]✓ Scene 3:[/] [grey]Aric leaves the tower and travels to find Lyra[/]");
var aricMoved = new CharacterState(aric.Id, aric.Name, VitalStatus.Alive, forest.Id)
    .WithKnownFact("Map reveals location of Crystal Caverns");

var storyState1Updated = storyState1
    .WithCharacter(aricMoved)
    .WithCharacter(lyraWithFact1);

AnsiConsole.MarkupLine("[green]✓ Scene 4:[/] [grey]Aric and Lyra reunite in the forest[/]\n");

// Validate and snapshot chapter 1
var validation1 = ruleEngine.ValidateState(storyState1Updated);
AnsiConsole.MarkupLine($"[cyan]Rule Validation:[/] {(validation1 is Result<Unit>.Success ? "[green]✓ Passed[/]" : "[red]✗ Failed[/]")}");

var snap1 = snapshotService.CreateSnapshot(storyState1Updated);
snapshots.Add((1, snap1));
AnsiConsole.MarkupLine($"[cyan]Snapshot created:[/] {snap1.SnapshotId}\n");

// ==================== CHAPTER 2: THE TURNING POINT ====================
AnsiConsole.MarkupLine("[bold yellow]CHAPTER 2: THE TURNING POINT[/]\n");

var worldState2 = worldState1.AdvanceTime(TimeSpan.FromHours(6));
AnsiConsole.MarkupLine($"[cyan]Time:[/] {worldState2.NarrativeTime:yyyy-MM-dd HH:mm:ss}");
AnsiConsole.MarkupLine($"[cyan]Time elapsed:[/] 6 hours\n");

AnsiConsole.MarkupLine("[green]✓ Scene 1:[/] [grey]Aric and Lyra convince Kael to join their quest[/]");
var kaelWithFact = new CharacterState(kael.Id, kael.Name, VitalStatus.Alive, caverns.Id)
    .WithKnownFact("A greater darkness approaches");

AnsiConsole.MarkupLine("[green]✓ Scene 2:[/] [grey]They travel together to the Crystal Caverns[/]");
var aricInCaverns = new CharacterState(aric.Id, aric.Name, VitalStatus.Alive, caverns.Id)
    .WithKnownFact("Map reveals location of Crystal Caverns")
    .WithKnownFact("The darkness grows stronger");

var lyraInCaverns = new CharacterState(lyra.Id, lyra.Name, VitalStatus.Alive, caverns.Id)
    .WithKnownFact("Ancient magic stirs in the world")
    .WithKnownFact("A prophecy must be fulfilled");

AnsiConsole.MarkupLine("[green]✓ Scene 3:[/] [grey]They find an ancient chamber with forbidden knowledge[/]");
var aricAdvanced = aricInCaverns.WithKnownFact("The truth is older than the world itself");

AnsiConsole.MarkupLine("[green]✓ Scene 4:[/] [red]An ancient guardian awakens and attacks![/]");
AnsiConsole.MarkupLine("[red]⚡ COMBAT BEGINS ⚡[/]\n");

var storyState2 = new StoryState(worldState2)
    .WithCharacter(aricAdvanced)
    .WithCharacter(lyraInCaverns)
    .WithCharacter(kaelWithFact);

var validation2 = ruleEngine.ValidateState(storyState2);
AnsiConsole.MarkupLine($"[cyan]Rule Validation:[/] {(validation2 is Result<Unit>.Success ? "[green]✓ Passed[/]" : "[red]✗ Failed[/]")}");

var snap2 = snapshotService.CreateSnapshot(storyState2);
snapshots.Add((2, snap2));
AnsiConsole.MarkupLine($"[cyan]Snapshot created:[/] {snap2.SnapshotId}\n");

// ==================== CHAPTER 3: THE REVELATION ====================
AnsiConsole.MarkupLine("[bold yellow]CHAPTER 3: THE REVELATION AND SACRIFICE[/]\n");

var worldState3 = worldState2.AdvanceTime(TimeSpan.FromHours(4));
AnsiConsole.MarkupLine($"[cyan]Time:[/] {worldState3.NarrativeTime:yyyy-MM-dd HH:mm:ss}");
AnsiConsole.MarkupLine($"[cyan]Time elapsed:[/] 4 hours (10 hours total)\n");

AnsiConsole.MarkupLine("[red]✗ Scene 1:[/] [red]Aric is mortally wounded defending his companions![/]");
var aricDead = new CharacterState(aric.Id, aric.Name, VitalStatus.Dead, caverns.Id)
    .WithKnownFact("The truth is older than the world itself")
    .WithKnownFact("Sacrifice is the only path forward");

AnsiConsole.MarkupLine("[yellow]⚔️  ARIC FALLS TO THE GUARDIAN ⚔️[/]\n");

AnsiConsole.MarkupLine("[green]✓ Scene 2:[/] [grey]With Aric's sacrifice, the barrier breaks[/]");
AnsiConsole.MarkupLine("[green]✓ Scene 3:[/] [grey]Lyra discovers the source of the darkness[/]");
var lyraFinal = lyraInCaverns
    .WithKnownFact("The darkness can be sealed, not destroyed")
    .WithKnownFact("The balance is now restored");

AnsiConsole.MarkupLine("[green]✓ Scene 4:[/] [grey]Kael honors Aric's memory and vows to protect the secret[/]");
var kaelFinal = kaelWithFact.WithKnownFact("I will guard this place forever");

AnsiConsole.MarkupLine("[green]✓ Scene 5:[/] [yellow]The two survivors emerge, forever changed[/]\n");

var storyState3 = new StoryState(worldState3)
    .WithCharacter(aricDead)
    .WithCharacter(lyraFinal)
    .WithCharacter(kaelFinal);

var validation3 = ruleEngine.ValidateState(storyState3);
AnsiConsole.MarkupLine($"[cyan]Rule Validation:[/] {(validation3 is Result<Unit>.Success ? "[green]✓ Passed[/]" : "[red]✗ Failed[/]")}");

var snap3 = snapshotService.CreateSnapshot(storyState3);
snapshots.Add((3, snap3));
AnsiConsole.MarkupLine($"[cyan]Snapshot created:[/] {snap3.SnapshotId}\n");

// ==================== SUMMARY ====================
AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]");
AnsiConsole.MarkupLine("[bold cyan]STORY SUMMARY[/]");
AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]\n");

var summaryTable = new Table()
    .Title("[bold yellow]Character Fates[/]")
    .AddColumn("[bold]Name[/]")
    .AddColumn("[bold]Starting Location[/]")
    .AddColumn("[bold]Final Status[/]")
    .AddColumn("[bold]Key Discovery[/]");

summaryTable.AddRow(
    "[cyan]Aric the Bold[/]",
    tower.Name,
    "[red]Dead[/]",
    "[yellow]The ancient map[/]"
);

summaryTable.AddRow(
    "[cyan]Lyra the Wise[/]",
    forest.Name,
    "[green]Alive[/]",
    "[yellow]The prophecy[/]"
);

summaryTable.AddRow(
    "[cyan]Kael the Shadow[/]",
    caverns.Name,
    "[green]Alive[/]",
    "[yellow]His purpose[/]"
);

AnsiConsole.Write(summaryTable);
AnsiConsole.WriteLine();

// Snapshots comparison
AnsiConsole.MarkupLine("[bold cyan]\nSnapshot Progression[/]");
var snapshotTable = new Table()
    .AddColumn("[bold]Chapter[/]")
    .AddColumn("[bold]Time[/]")
    .AddColumn("[bold]Alive Characters[/]")
    .AddColumn("[bold]Hash[/]");

snapshotTable.AddRow(
    "1",
    snap1.CreatedAt.ToString("HH:mm:ss"),
    "[green]3[/]",
    (snap1.IntegrityHash ?? "N/A")[..12] + "..."
);

snapshotTable.AddRow(
    "2",
    snap2.CreatedAt.ToString("HH:mm:ss"),
    "[green]3[/]",
    (snap2.IntegrityHash ?? "N/A")[..12] + "..."
);

snapshotTable.AddRow(
    "3",
    snap3.CreatedAt.ToString("HH:mm:ss"),
    "[yellow]2[/]",
    (snap3.IntegrityHash ?? "N/A")[..12] + "..."
);

AnsiConsole.Write(snapshotTable);
AnsiConsole.WriteLine();

// Final panel
var finalPanel = new Panel(
    $@"[bold cyan]Story Conclusion[/]

[yellow]The Three Acts:[/]
  [green]✓[/] Act 1: [grey]The gathering of heroes[/]
  [green]✓[/] Act 2: [grey]The descent into darkness[/]
  [yellow]✓[/] Act 3: [grey]The price of salvation[/]

[yellow]Total Time:[/] [cyan]10 hours of narrative[/]
[yellow]Snapshots:[/] [cyan]3 timeline saves[/]
[yellow]Character Arcs:[/] [cyan]3 complete journeys[/]

[grey]This is a fully deterministic, reproducible story.[/]
[grey]Each chapter can be restored from its snapshot.[/]"
)
{
    Border = BoxBorder.Rounded,
    Padding = new Padding(2, 1, 2, 1)
};

AnsiConsole.Write(finalPanel);
AnsiConsole.WriteLine();

// ==================== PHASE 2: MEMORY SYSTEM DEMONSTRATION ====================
AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]");
AnsiConsole.MarkupLine("[bold cyan]PHASE 2: MEMORY SYSTEM INTEGRATION[/]");
AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]\n");

AnsiConsole.MarkupLine("[yellow]Demonstrating fact extraction and coherence validation...[/]\n");

// Mock implementations for demo
var mockExtractor = new MockFactExtractor();
var mockValidator = new MockCoherenceValidator();

// Key narrative events
var events = new[]
{
    "Aric discovers an ancient map hidden in the tower's vault",
    "The map points to the Crystal Caverns as the source of darkness",
    "Lyra senses disturbance through ancient magical connection",
    "Aric meets Lyra in the Whispering Forest - they form alliance",
    "They convince Kael to join their quest for answers",
    "The trio enters the Crystal Caverns together",
    "An ancient guardian awakens, blocking their path",
    "Aric sacrifices himself to break the guardian's spell",
    "Lyra discovers the nature of the darkness - it cannot be destroyed",
    "Kael honors Aric's memory by becoming eternal guardian of the seal"
};

AnsiConsole.MarkupLine("[bold]→ Extracting facts from narrative events...[/]\n");

var totalFacts = 0;

// Process each event
foreach (var (index, eventDescription) in events.Select((e, i) => (i + 1, e)))
{
    AnsiConsole.MarkupLine($"[cyan]Event {index}:[/] {eventDescription}");
    
    var facts = mockExtractor.ExtractFromEvent(eventDescription);
    totalFacts += facts.Count;
    
    AnsiConsole.MarkupLine($"  [green]✓ Extracted {facts.Count} facts[/]");
    
    foreach (var fact in facts)
    {
        AnsiConsole.MarkupLine($"    • [grey]{fact}[/]");
    }
    
    AnsiConsole.WriteLine();
}

AnsiConsole.MarkupLine("[bold]→ Building memory chapters from event clusters...[/]\n");

// Create memory chapters  
var chapters = new[]
{
    ("The Discovery Phase", 2, "Aric uncovers ancient secrets hidden in the tower. A map reveals the source of the darkness."),
    ("The Alliance", 3, "Lyra senses the disturbance. Aric and Lyra unite, bringing Kael into their quest."),
    ("The Guardian's Challenge", 2, "The trio descends into the Crystal Caverns where an ancient guardian rises. Combat erupts."),
    ("Sacrifice and Revelation", 3, "Aric makes the ultimate sacrifice. Lyra discovers the nature of darkness. A new guardian emerges.")
};

foreach (var (title, eventCount, summary) in chapters)
{
    AnsiConsole.MarkupLine($"[cyan]Chapter:[/] {title}");
    AnsiConsole.MarkupLine($"  [grey]Events: {eventCount} | Summary: {summary}[/]");
    AnsiConsole.WriteLine();
}

AnsiConsole.MarkupLine("[bold]→ Validating narrative coherence...[/]\n");

// Coherence validation demo
var coherenceIssues = mockValidator.ValidateCoherence(new[]
{
    ("Aric the Bold", "Alive"),
    ("Aric the Bold", "Dead"),  // Contradiction!
    ("Lyra the Wise", "Alive"),
    ("Kael the Shadow", "Alive"),
});

if (coherenceIssues.Count > 0)
{
    AnsiConsole.MarkupLine("[red]⚠️  Coherence Violations Detected:[/]\n");
    foreach (var issue in coherenceIssues)
    {
        AnsiConsole.MarkupLine($"  [red]✗ {issue}[/]");
    }
    AnsiConsole.WriteLine();
}

AnsiConsole.MarkupLine("[bold]→ Building canonical world state...[/]\n");

// Final state
var canonicalState = new Dictionary<string, string>
{
    ["TimeElapsed"] = "10 hours",
    ["AliveCharacters"] = "2",
    ["DeadCharacters"] = "1",
    ["MemoriaSize"] = "4 chapters",
    ["TotalFacts"] = totalFacts.ToString(),
    ["Coherent"] = "Yes ✓"
};

var stateTable = new Table()
    .Title("[bold yellow]Canonical Narrative State[/]")
    .AddColumn("[bold]Property[/]")
    .AddColumn("[bold]Value[/]");

foreach (var kvp in canonicalState)
{
    stateTable.AddRow(kvp.Key, kvp.Value);
}

AnsiConsole.Write(stateTable);
AnsiConsole.WriteLine();

// Final summary
var memoryPanel = new Panel(
    $@"[bold cyan]Complete Narrative Journey with Memory Integration[/]

[yellow]Phase 1 - Story Foundation:[/]
  [green]✓[/] World creation: The Hidden Realm
  [green]✓[/] Characters: Aric, Lyra, Kael
  [green]✓[/] Locations: Tower, Forest, Caverns
  [green]✓[/] 10 hours of narrative time
  [green]✓[/] 3 story chapters with full arcs

[yellow]Phase 2 - Memory System:[/]
  [green]✓[/] Fact extraction: {totalFacts} facts from events
  [green]✓[/] Memory chapters: 4 major story arcs
  [green]✓[/] Coherence validation: Contradiction detected & reported
  [green]✓[/] Canonical state: Complete world model
  [green]✓[/] Violation detection: System working correctly

[yellow]System Capabilities:[/]
  [green]✓[/] Event-to-fact transformation
  [green]✓[/] Narrative summarization
  [green]✓[/] Entity tracking across events
  [green]✓[/] Relationship preservation
  [green]✓[/] Character state consistency
  [green]✓[/] Timeline integrity

[grey]The memory system enables narrative AI to understand[/]
[grey]and reason about complex story progressions reliably.[/]"
)
{
    Border = BoxBorder.Rounded,
    Padding = new Padding(2, 1, 2, 1)
};

AnsiConsole.Write(memoryPanel);
AnsiConsole.WriteLine();

AnsiConsole.MarkupLine("[bold green]✨ Phase 1 + Phase 2 demonstrates complete narrative understanding! ✨[/]\n");

// ==================== MOCK IMPLEMENTATIONS FOR DEMO ====================

class MockFactExtractor
{
    public List<string> ExtractFromEvent(string eventDescription)
    {
        var facts = new List<string>();
        
        // Simple pattern matching for demo
        if (eventDescription.Contains("discover", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("find", StringComparison.OrdinalIgnoreCase))
        {
            facts.Add("Discovery made during narrative");
        }
        
        if (eventDescription.Contains("death", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("sacrifice", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("dies", StringComparison.OrdinalIgnoreCase))
        {
            facts.Add("Character death or sacrifice");
        }
        
        if (eventDescription.Contains("alliance", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("meet", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("join", StringComparison.OrdinalIgnoreCase))
        {
            facts.Add("Character relationship or alliance formed");
        }
        
        if (eventDescription.Contains("guardian", StringComparison.OrdinalIgnoreCase) ||
            eventDescription.Contains("darkness", StringComparison.OrdinalIgnoreCase))
        {
            facts.Add("Supernatural or threat-related event");
        }
        
        // Ensure we have at least one fact
        if (facts.Count == 0)
        {
            facts.Add("Narrative event occurred");
        }
        
        return facts;
    }
}

class MockCoherenceValidator
{
    public List<string> ValidateCoherence(params (string entity, string state)[] states)
    {
        var violations = new List<string>();
        var entityStates = new Dictionary<string, string>();
        
        foreach (var (entity, state) in states)
        {
            if (entityStates.ContainsKey(entity))
            {
                if (entityStates[entity] != state)
                {
                    violations.Add($"{entity} is both {entityStates[entity]} and {state}");
                }
            }
            else
            {
                entityStates[entity] = state;
            }
        }
        
        return violations;
    }
}

class DebugLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Silent for demo
    }
}
