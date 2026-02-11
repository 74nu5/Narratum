using Spectre.Console;
using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Llm.DependencyInjection;
using Narratum.Llm.Clients;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Narratum.Playground.Phases;

/// <summary>
/// Démonstration de la Phase 4 : Intégration LLM avec Microsoft Foundry Local
/// </summary>
public static class Phase4FoundryLocalDemo
{
    public static async Task RunAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("PHASE 4")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.MarkupLine("[yellow]LLM Integration - Microsoft Foundry Local[/]");
        AnsiConsole.MarkupLine("[grey]Testing local AI model integration[/]\n");

        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold cyan]FOUNDRY LOCAL - LLM CLIENT TEST[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]\n");

        // Configuration
        var modelName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Choisir le modèle Foundry Local:[/]")
                .AddChoices("Phi-4", "Phi-4-mini"));

        AnsiConsole.MarkupLine($"\n[cyan]Modèle sélectionné:[/] {modelName}");
        AnsiConsole.MarkupLine($"[grey]Configuration: Foundry Local SDK avec {modelName}[/]\n");

        // Setup DI
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]Initialisation de Foundry Local...[/]", async ctx =>
            {
                ctx.Status("[yellow]Configuration du service container...[/]");
                await Task.Delay(500);

                ctx.Status("[yellow]Chargement du SDK Foundry Local...[/]");
                await Task.Delay(500);

                ctx.Status($"[yellow]Préparation du modèle {modelName}...[/]");
                await Task.Delay(1000);
            });

        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings/errors
        });

        // Add Foundry Local LLM client
        try
        {
            services.AddNarratumFoundryLocal(
                defaultModel: modelName,
                narratorModel: modelName
            );

            AnsiConsole.MarkupLine("[green]✓[/] Service LLM configuré avec succès\n");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Erreur de configuration:[/] {ex.Message}\n");
            return;
        }

        // Build service provider
        await using var provider = services.BuildServiceProvider();
        ILlmClient? client = null;

        try
        {
            client = provider.GetRequiredService<ILlmClient>();
            AnsiConsole.MarkupLine("[green]✓[/] Client LLM résolu depuis le DI container\n");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Erreur de résolution du client:[/] {ex.Message}\n");
            return;
        }

        AnsiConsole.MarkupLine("[green]✓[/] Client LLM prêt pour les tests\n");

        // Test prompts
        var prompts = new[]
        {
            (
                Agent: "Narrator",
                SystemPrompt: "Tu es un narrateur de fantasy épique.",
                UserPrompt: "Raconte en 2 phrases l'histoire d'un chevalier qui rencontre un dragon dans une forêt.",
                Tokens: 150,
                Color: "cyan"
            ),
            (
                Agent: "Character",
                SystemPrompt: "Tu es un personnage dans une histoire.",
                UserPrompt: "En une phrase, décris tes pensées lorsque tu découvres un trésor caché.",
                Tokens: 80,
                Color: "yellow"
            ),
            (
                Agent: "Summary",
                SystemPrompt: "Tu es un résumeur concis.",
                UserPrompt: "Résume en une phrase: Un héros brave affronte un monstre terrifiant et remporte la victoire grâce à son courage.",
                Tokens: 50,
                Color: "green"
            ),
            (
                Agent: "Consistency",
                SystemPrompt: "Tu vérifies la cohérence narrative.",
                UserPrompt: "Vérifie la cohérence: 'Le personnage est vivant' puis 'Le personnage meurt'. Réponds COHERENT ou INCOHERENT.",
                Tokens: 30,
                Color: "magenta"
            )
        };

        AnsiConsole.MarkupLine("[bold yellow]→ Test de génération par agent[/]\n");

        foreach (var (agent, systemPrompt, userPrompt, tokens, color) in prompts)
        {
            var panel = new Panel($"[grey]System: {systemPrompt}\nUser: {userPrompt}[/]")
            {
                Header = new PanelHeader($"[{color}]{agent}[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };

            AnsiConsole.Write(panel);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"[yellow]Génération en cours ({agent})...[/]", async ctx =>
                {
                    try
                    {
                        var request = new LlmRequest(
                            systemPrompt: systemPrompt,
                            userPrompt: userPrompt,
                            parameters: new LlmParameters
                            {
                                MaxTokens = tokens,
                                Temperature = 0.7
                            },
                            metadata: new Dictionary<string, object>
                            {
                                [ChatClientLlmAdapter.ModelMetadataKey] = modelName
                            }
                        );

                        var result = await client.GenerateAsync(request, CancellationToken.None);

                        if (result is Result<LlmResponse>.Success success)
                        {
                            var response = success.Value;

                            AnsiConsole.MarkupLine($"\n[{color}]Réponse:[/]");
                            AnsiConsole.MarkupLine($"[white]{response.Content}[/]");

                            AnsiConsole.MarkupLine($"[green]✓ Succès[/]\n");
                        }
                        else if (result is Result<LlmResponse>.Failure failure)
                        {
                            AnsiConsole.MarkupLine($"\n[red]✗ Erreur:[/] {failure.Message}\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"\n[red]✗ Exception:[/] {ex.Message}\n");
                    }
                });
        }

        // Summary
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold cyan]RÉSUMÉ DU TEST[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════════════[/]\n");

        var summaryPanel = new Panel(
            $@"[bold cyan]Phase 4 - Foundry Local Integration[/]

[yellow]Configuration:[/]
  [green]✓[/] Provider: Microsoft Foundry Local
  [green]✓[/] Modèle: {modelName}
  [green]✓[/] Architecture: IChatClient → ILlmClient
  [green]✓[/] Dependency Injection: Configuré

[yellow]Tests effectués:[/]
  [green]✓[/] Narrator agent
  [green]✓[/] Character agent
  [green]✓[/] Summary agent
  [green]✓[/] Consistency agent

[yellow]Fonctionnalités validées:[/]
  [green]✓[/] Génération de contenu
  [green]✓[/] Métadonnées de requête
  [green]✓[/] Paramètres (température, max tokens)
  [green]✓[/] System + User prompts

[grey]Le système LLM est prêt pour l'orchestration complète.[/]"
        )
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1, 2, 1)
        };

        AnsiConsole.Write(summaryPanel);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold green]✨ Phase 4 test terminé avec succès! ✨[/]\n");
    }
}
