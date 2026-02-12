using Spectre.Console;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Services;
using Narratum.Orchestration.Models;
using Narratum.Persistence;
using Narratum.Llm.Configuration;
using Narratum.Llm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Narratum.Playground.Phases;

/// <summary>
/// Phase 5 Demo - Test complet de génération narrative End-to-End.
/// Crée une histoire, génère plusieurs pages avec le LLM, affiche les résultats.
/// </summary>
public static class Phase5NarrativeGenerationDemo
{
    public static async Task RunAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]Phase 5 - Full Narrative Generation[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Setup DI
        var services = new ServiceCollection();
        
        AnsiConsole.MarkupLine("[grey]Setting up services...[/]");
        
        services.AddDbContext<NarrativumDbContext>(options =>
            options.UseSqlite("Data Source=playground_narrative.db"));
        
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddScoped<PersistenceService>();
        
        var llmConfig = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "Phi-4-mini"
        };
        services.AddNarratumLlm(llmConfig);
        
        services.AddScoped<FullOrchestrationService>();
        
        await using var provider = services.BuildServiceProvider();
        
        // Ensure clean database
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NarrativumDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            AnsiConsole.MarkupLine("[green]✓ Database ready[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Creating Initial Story State[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Create initial state
        StoryState initialState;
        using (var scope = provider.CreateScope())
        {
            var worldId = Id.New();
            var aricId = Id.New();
            var elaraId = Id.New();
            
            var aric = new CharacterState(aricId, "Aric le Brave");
            var elara = new CharacterState(elaraId, "Elara la Sage");
            
            initialState = StoryState.Create(worldId, "Elendor")
                .WithCharacters(aric, elara);
            
            AnsiConsole.MarkupLine($"[cyan]World:[/] Elendor");
            AnsiConsole.MarkupLine($"[cyan]Characters:[/] {aric.Name}, {elara.Name}");
            
            // Save initial state
            var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();
            var snapshot = snapshotService.CreateSnapshot(initialState);
            
            var db = scope.ServiceProvider.GetRequiredService<NarrativumDbContext>();
            db.PageSnapshots.Add(new PageSnapshotEntity
            {
                Id = Guid.NewGuid(),
                SlotName = "playground-test",
                PageIndex = 0,
                GeneratedAt = DateTime.UtcNow,
                NarrativeText = "Histoire initialisée - Monde: Elendor, Personnages: Aric le Brave, Elara la Sage",
                SerializedState = JsonSerializer.Serialize(snapshot),
                IntentDescription = "Initialization",
                ModelUsed = "N/A",
                GenreStyle = "Fantasy épique"
            });
            await db.SaveChangesAsync();
            
            AnsiConsole.MarkupLine("[green]✓ Initial state saved (Page 0)[/]");
        }

        AnsiConsole.WriteLine();
        var continueGen = AnsiConsole.Confirm("[yellow]Generate narrative pages?[/]", true);
        
        if (!continueGen)
        {
            AnsiConsole.MarkupLine("[grey]Demo skipped.[/]");
            return;
        }

        // Generate 3 narrative pages
        var intents = new[]
        {
            "Aric et Elara se rencontrent dans une taverne sombre. Ils partagent une mission commune.",
            "Les deux héros découvrent une carte ancienne menant à un trésor légendaire.",
            "En route vers le trésor, ils sont attaqués par des bandits. Aric doit les affronter."
        };

        for (int i = 0; i < intents.Length; i++)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[yellow]Generating Page {i + 1}[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine($"[cyan]Intent:[/] {intents[i]}");
            AnsiConsole.WriteLine();

            await AnsiConsole.Status()
                .StartAsync("Generating narrative...", async ctx =>
                {
                    using var scope = provider.CreateScope();
                    
                    // Load latest state
                    var db = scope.ServiceProvider.GetRequiredService<NarrativumDbContext>();
                    var latestSnapshot = await db.PageSnapshots
                        .Where(p => p.SlotName == "playground-test")
                        .OrderByDescending(p => p.PageIndex)
                        .FirstAsync();
                    
                    var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();
                    var snapshot = JsonSerializer.Deserialize<Narratum.Persistence.StateSnapshot>(latestSnapshot.SerializedState)!;
                    var stateResult = snapshotService.RestoreFromSnapshot(snapshot);
                    
                    if (stateResult is not Result<StoryState>.Success success)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Failed to restore state: {(stateResult as Result<StoryState>.Failure)?.Message}[/]");
                        return;
                    }
                    
                    var currentState = success.Value;
                    
                    // Create intent
                    var intent = NarrativeIntent.Continue(intents[i]);
                    
                    // Execute pipeline
                    var orchestrator = scope.ServiceProvider.GetRequiredService<FullOrchestrationService>();
                    var result = await orchestrator.ExecuteCycleAsync(currentState, intent, CancellationToken.None);
                    
                    if (result is Result<FullPipelineResult>.Success pipelineSuccess)
                    {
                        var output = pipelineSuccess.Value;
                        
                        if (output.IsSuccess && output.Output != null)
                        {
                            // Save new page
                            var newSnapshot = snapshotService.CreateSnapshot(currentState);
                            db.PageSnapshots.Add(new PageSnapshotEntity
                            {
                                Id = Guid.NewGuid(),
                                SlotName = "playground-test",
                                PageIndex = latestSnapshot.PageIndex + 1,
                                GeneratedAt = DateTime.UtcNow,
                                NarrativeText = output.Output.NarrativeText,
                                SerializedState = JsonSerializer.Serialize(newSnapshot),
                                IntentDescription = intents[i],
                                ModelUsed = "Phi-4-mini",
                                GenreStyle = "Fantasy épique"
                            });
                            await db.SaveChangesAsync();
                            
                            AnsiConsole.MarkupLine($"[green]✓ Page {i + 1} generated successfully![/]");
                            AnsiConsole.WriteLine();
                            
                            var panel = new Panel(output.Output.NarrativeText)
                                .Header($"[yellow]📖 Page {i + 1}[/]")
                                .Border(BoxBorder.Rounded)
                                .BorderColor(Color.Cyan1);
                            AnsiConsole.Write(panel);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]✗ Generation failed: {output.ErrorMessage}[/]");
                        }
                    }
                    else
                    {
                        var failure = result as Result<FullPipelineResult>.Failure;
                        AnsiConsole.MarkupLine($"[red]✗ Pipeline failed: {failure?.Message}[/]");
                    }
                });
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Demo Complete![/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"\n[green]✓ Generated {intents.Length} narrative pages successfully![/]");
    }
}
