namespace Narratum.Web.Components.Pages.Wizard;

using Narratum.Llm.Azure;
using Narratum.Web.Models;
using Narratum.Web.Services;

public partial class Index
{
    private readonly string[] stepTitles = ["Monde", "Genre", "Personnages", "Lieux", "Première page", "Résumé"];
    private int currentStep;

    // Genre catalogue (code → French label).
    private static readonly IReadOnlyList<KeyValuePair<string, string>> genreOptions =
    [
        new("Fantasy", "Fantasy"),
        new("SciFi", "Science-Fiction"),
        new("Mystery", "Mystère"),
        new("Horror", "Horreur"),
        new("Historical", "Historique"),
    ];

    private string worldName = string.Empty;
    private string worldDescription = string.Empty;
    private string genre = "Fantasy";
    private string narrativeStyle = string.Empty;

    // Optional opening action: when filled, the first page is generated right after creation.
    private string firstAction = string.Empty;
    private string model = ModelSelectionService.AvailableModels[0].Id;
    private IReadOnlyList<ModelOption> models = ModelSelectionService.AvailableModels;
    private IReadOnlyList<ModelOption> localModels = [];
    private IReadOnlyList<AzureSubscriptionInfo> subscriptions = [];
    private string? currentSubscription;
    private readonly List<CharacterInput> characters = [];
    private readonly List<LocationInput> locations = [];

    private IReadOnlyList<CharacterInput> NamedCharacters =>
        this.characters.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();

    private IReadOnlyList<LocationInput> NamedLocations =>
        this.locations.Where(l => !string.IsNullOrWhiteSpace(l.Name)).ToList();

    // Inline dot style for a wizard step marker (current / done / upcoming).
    private static string StepDotStyle(bool current, bool done) => current
        ? "background:var(--accent);color:var(--on-accent);box-shadow:0 0 0 4px var(--accent-glow)"
        : done ? "background:transparent;color:var(--accent);border:1px solid var(--accent)"
        : "background:transparent;color:var(--text-3);border:1px solid var(--border-strong)";

    protected override async Task OnInitializedAsync()
    {
        this.localModels = await this.ModelCatalog.GetModelsAsync(this.LlmClient);
        this.subscriptions = await this.AzureState.GetSubscriptionsAsync();
        this.currentSubscription = this.AzureState.CurrentSubscriptionId;
        await this.RebuildModelListAsync();
        this.model = this.models.FirstOrDefault()?.Id ?? this.model;
    }

    // Merge local Foundry models with the current Azure subscription's chat deployments.
    private async Task RebuildModelListAsync()
    {
        var azureModels = await this.AzureState.GetAzureModelsAsync();
        this.models = [.. this.localModels, .. azureModels];
    }

    private async Task OnSubscriptionChanged()
    {
        if (string.IsNullOrEmpty(this.currentSubscription))
            return;

        this.AzureState.SetCurrentSubscription(this.currentSubscription);
        await this.RebuildModelListAsync();
    }

    private bool CanProceed => this.currentStep switch
    {
        0 => !string.IsNullOrWhiteSpace(this.worldName),
        2 => this.characters.Count > 0,
        _ => true,
    };

    private bool CanCreate => !string.IsNullOrWhiteSpace(this.worldName) && this.characters.Count > 0;

    private void NextStep()
    {
        if (this.currentStep < this.stepTitles.Length - 1)
            this.currentStep++;
    }

    private void PreviousStep()
    {
        if (this.currentStep > 0)
            this.currentStep--;
    }

    private async Task CreateStory()
    {
        var slotName = $"story-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        var request = new StoryCreationRequest(
            this.worldName,
            this.genre,
            [.. this.characters.Select(c => (c.Name, (string?)c.Description))],
            string.IsNullOrWhiteSpace(this.worldDescription) ? null : this.worldDescription,
            string.IsNullOrWhiteSpace(this.narrativeStyle) ? null : this.narrativeStyle,
            [.. this.locations.Select(l => (l.Name, (string?)l.Description))],
            this.model);

        await this.GenService.CreateStoryAsync(slotName, request);

        // Hand the opening action over so the generation screen writes page 1 straight away.
        var target = string.IsNullOrWhiteSpace(this.firstAction)
            ? $"/generation/{slotName}"
            : $"/generation/{slotName}?intent={Uri.EscapeDataString(this.firstAction.Trim())}";

        this.Navigation.NavigateTo(target);
    }

    /// <summary>Character row captured by the wizard form.</summary>
    public sealed class CharacterInput
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>Location row captured by the wizard form.</summary>
    public sealed class LocationInput
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
