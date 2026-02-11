using Spectre.Console;
using Narratum.Playground.Phases;

var shouldContinue = true;

while (shouldContinue)
{
    AnsiConsole.Clear();
    AnsiConsole.Write(
        new FigletText("NARRATUM")
            .Centered()
            .Color(Color.Cyan1));

    AnsiConsole.MarkupLine("[yellow]Narrative Engine Playground[/]");
    AnsiConsole.MarkupLine("[grey]Choisissez une phase à tester[/]\n");

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<MenuChoice>()
            .Title("[yellow]Quelle phase voulez-vous tester ?[/]")
            .PageSize(10)
            .MoreChoicesText("[grey](Utilisez les flèches pour naviguer)[/]")
            .AddChoices(MenuChoice.Phase1And2, MenuChoice.Phase4, MenuChoice.Quit)
            .UseConverter(choice => choice switch
            {
                MenuChoice.Phase1And2 => "Phase 1 & 2 - Story Walkthrough + Memory System",
                MenuChoice.Phase4 => "Phase 4 - LLM Integration (Foundry Local)",
                MenuChoice.Quit => "Quitter",
                _ => choice.ToString()
            }));

    if (choice == MenuChoice.Quit)
    {
        AnsiConsole.MarkupLine("\n[cyan]Au revoir ![/]");
        break;
    }

    try
    {
        switch (choice)
        {
            case MenuChoice.Phase1And2:
                Phase1And2Demo.Run();
                break;
            case MenuChoice.Phase4:
                await Phase4FoundryLocalDemo.RunAsync();
                break;
        }

        if (!AnsiConsole.Confirm("\n[yellow]Revenir au menu principal ?[/]", defaultValue: true))
        {
            break;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        
        if (!AnsiConsole.Confirm("\n[yellow]Revenir au menu principal ?[/]", defaultValue: true))
        {
            break;
        }
    }
}

enum MenuChoice
{
    Phase1And2,
    Phase4,
    Quit
}
