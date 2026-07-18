using Narratum.Llm.Azure;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// État applicatif du provider cloud Azure AI Foundry : souscription courante, liste des
/// souscriptions accessibles, et modèles (déploiements de chat) proposés dans le sélecteur.
/// Tout est chargé paresseusement via <see cref="IAzureFoundryDirectory"/> (Entra) et mis en cache ;
/// si Azure est indisponible (non connecté, RBAC), les listes sont vides et l'app reste 100% locale.
/// </summary>
public sealed class AzureFoundryState
{
    private readonly IAzureFoundryDirectory _directory;
    private readonly ILogger<AzureFoundryState> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IReadOnlyList<AzureSubscriptionInfo>? _subscriptions;
    private string? _currentSubscriptionId;
    private readonly Dictionary<string, IReadOnlyList<AzureFoundryDeployment>> _deploymentsBySubscription = new();

    public AzureFoundryState(IAzureFoundryDirectory directory, ILogger<AzureFoundryState> logger)
    {
        this._directory = directory;
        this._logger = logger;
    }

    /// <summary>Souscription active (celle dont les modèles sont proposés).</summary>
    public string? CurrentSubscriptionId => this._currentSubscriptionId;

    /// <summary>Liste (mise en cache) des souscriptions accessibles. Vide si Azure est indisponible.</summary>
    public async Task<IReadOnlyList<AzureSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        if (this._subscriptions is not null)
            return this._subscriptions;

        await this._lock.WaitAsync(ct);
        try
        {
            if (this._subscriptions is null)
            {
                try
                {
                    this._subscriptions = await this._directory.ListSubscriptionsAsync(ct);
                }
                catch (Exception ex)
                {
                    this._logger.LogWarning(ex, "Azure subscriptions unavailable; cloud provider disabled");
                    this._subscriptions = [];
                }

                this._currentSubscriptionId ??=
                    this._subscriptions.FirstOrDefault(s => s.IsDefault)?.SubscriptionId
                    ?? this._subscriptions.FirstOrDefault()?.SubscriptionId;
            }

            return this._subscriptions;
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <summary>Change la souscription active ; ses modèles seront (re)chargés à la demande.</summary>
    public void SetCurrentSubscription(string subscriptionId)
        => this._currentSubscriptionId = subscriptionId;

    /// <summary>
    /// Modèles de chat Azure de la souscription courante, en options de sélecteur (id composite
    /// <c>azure:endpoint::deployment</c>). Les modèles non-chat (embeddings, image, audio) sont exclus.
    /// </summary>
    public async Task<IReadOnlyList<ModelOption>> GetAzureModelsAsync(CancellationToken ct = default)
        => ToOptions(await this.GetDeploymentsAsync(ct), IsChatModel);

    /// <summary>
    /// Modèles d'IMAGE Azure de la souscription courante, en options de sélecteur (id composite).
    /// </summary>
    public async Task<IReadOnlyList<ModelOption>> GetAzureImageModelsAsync(CancellationToken ct = default)
        => ToOptions(await this.GetDeploymentsAsync(ct), IsImageModel);

    private static IReadOnlyList<ModelOption> ToOptions(
        IReadOnlyList<AzureFoundryDeployment> deployments, Func<AzureFoundryDeployment, bool> keep)
        =>
        [
            .. deployments
                .Where(keep)
                .OrderBy(d => d.ModelName, StringComparer.OrdinalIgnoreCase)
                .Select(d => new ModelOption(
                    AzureModelRef.Compose(d.Endpoint, d.DeploymentName),
                    $"☁️ {d.DeploymentName} · {d.ModelName} ({d.Location})")),
        ];

    /// <summary>Charge (et met en cache) les déploiements de la souscription courante.</summary>
    private async Task<IReadOnlyList<AzureFoundryDeployment>> GetDeploymentsAsync(CancellationToken ct)
    {
        await this.GetSubscriptionsAsync(ct);

        var subscription = this._currentSubscriptionId;
        if (string.IsNullOrEmpty(subscription))
            return [];

        if (this._deploymentsBySubscription.TryGetValue(subscription, out var cached))
            return cached;

        await this._lock.WaitAsync(ct);
        try
        {
            if (!this._deploymentsBySubscription.TryGetValue(subscription, out cached))
            {
                try
                {
                    cached = await this._directory.ListDeploymentsAsync(subscription, ct);
                }
                catch (Exception ex)
                {
                    this._logger.LogWarning(ex, "Azure deployments unavailable for subscription {Sub}", subscription);
                    cached = [];
                }

                this._deploymentsBySubscription[subscription] = cached;
            }

            return cached;
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <summary>Garde les modèles de chat ; écarte embeddings, image et audio.</summary>
    private static bool IsChatModel(AzureFoundryDeployment d)
    {
        var probe = (d.ModelName + " " + (d.ModelFormat ?? string.Empty)).ToLowerInvariant();
        string[] excluded = ["embedding", "dall-e", "flux", "sora", "mai-image", "whisper", "tts", "image", "imagen"];
        return !excluded.Any(probe.Contains);
    }

    /// <summary>Garde les modèles de génération d'IMAGE ; écarte la vidéo (sora) et le reste.</summary>
    private static bool IsImageModel(AzureFoundryDeployment d)
    {
        var probe = (d.ModelName + " " + (d.ModelFormat ?? string.Empty)).ToLowerInvariant();
        string[] included = ["dall-e", "flux", "mai-image", "imagen", "gpt-image", "stable-diffusion"];
        return included.Any(probe.Contains);
    }
}
