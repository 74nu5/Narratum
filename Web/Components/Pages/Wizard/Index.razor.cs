namespace Narratum.Web.Components.Pages.Wizard;

using Narratum.Llm.Azure;
using Narratum.Web.Models;
using Narratum.Web.Services;

using static Shared.CharactersEditor;
using static Shared.LocationsEditor;

public partial class Index
{
    private readonly string[] stepTitles = ["Monde", "Genre", "Personnages", "Lieux", "Résumé"];
    private int currentStep;

    private string worldName = string.Empty;
    private string worldDescription = string.Empty;
    private string genre = "Fantasy";
    private string narrativeStyle = string.Empty;
    private string model = ModelSelectionService.AvailableModels[0].Id;
    private IReadOnlyList<ModelOption> models = ModelSelectionService.AvailableModels;
    private IReadOnlyList<ModelOption> localModels = [];
    private IReadOnlyList<AzureSubscriptionInfo> subscriptions = [];
    private string? currentSubscription;
    private List<CharacterInput> characters = [];
    private List<LocationInput> locations = [];

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

    private async Task OnSubscriptionChanged(string? subscriptionId)
    {
        if (string.IsNullOrEmpty(subscriptionId))
            return;

        this.currentSubscription = subscriptionId;
        this.AzureState.SetCurrentSubscription(subscriptionId);
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

        this.Navigation.NavigateTo($"/generation/{slotName}");
    }

    private void Refresh()
    {
        this.StateHasChanged();
    }
}
