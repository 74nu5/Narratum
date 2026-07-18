using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Narratum.Llm.Azure;

/// <summary>Une souscription Azure visible par l'identité connectée.</summary>
public sealed record AzureSubscriptionInfo(string SubscriptionId, string DisplayName, bool IsDefault);

/// <summary>
/// Un déploiement de modèle sur une ressource Azure AI Foundry / OpenAI, prêt à l'emploi.
/// </summary>
public sealed record AzureFoundryDeployment(
    string AccountName,
    string Endpoint,
    string DeploymentName,
    string ModelName,
    string? ModelFormat,
    string Location);

/// <summary>
/// Découverte Azure via Entra ID (ARM) : souscriptions accessibles et déploiements de modèles
/// des ressources Cognitive Services (Azure AI Foundry / OpenAI). Aucune clé — l'identité
/// provient de <see cref="TokenCredential"/> (DefaultAzureCredential en production).
/// </summary>
public interface IAzureFoundryDirectory
{
    /// <summary>Liste les souscriptions Azure accessibles par l'identité connectée.</summary>
    Task<IReadOnlyList<AzureSubscriptionInfo>> ListSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Liste les déploiements de modèles de chat des ressources Cognitive Services d'une souscription.
    /// </summary>
    Task<IReadOnlyList<AzureFoundryDeployment>> ListDeploymentsAsync(
        string subscriptionId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AzureFoundryDirectory : IAzureFoundryDirectory
{
    private readonly ArmClient _arm;
    private readonly ILogger _logger;

    // Kinds de compte Cognitive Services qui servent des modèles de chat OpenAI-compatibles.
    private static readonly HashSet<string> ChatAccountKinds =
        new(StringComparer.OrdinalIgnoreCase) { "OpenAI", "AIServices" };

    public AzureFoundryDirectory(TokenCredential credential, ILogger<AzureFoundryDirectory>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        this._arm = new ArmClient(credential);
        this._logger = logger ?? NullLogger<AzureFoundryDirectory>.Instance;
    }

    public async Task<IReadOnlyList<AzureSubscriptionInfo>> ListSubscriptionsAsync(CancellationToken ct = default)
    {
        var result = new List<AzureSubscriptionInfo>();
        var defaultSub = await this._arm.GetDefaultSubscriptionAsync(ct);
        var defaultId = defaultSub.Data.SubscriptionId;

        await foreach (var sub in this._arm.GetSubscriptions().GetAllAsync(ct))
        {
            result.Add(new AzureSubscriptionInfo(
                sub.Data.SubscriptionId,
                sub.Data.DisplayName,
                string.Equals(sub.Data.SubscriptionId, defaultId, StringComparison.OrdinalIgnoreCase)));
        }

        return result;
    }

    public async Task<IReadOnlyList<AzureFoundryDeployment>> ListDeploymentsAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        var subscription = this._arm.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));

        var deployments = new List<AzureFoundryDeployment>();

        await foreach (var account in subscription.GetCognitiveServicesAccountsAsync(ct))
        {
            if (!ChatAccountKinds.Contains(account.Data.Kind ?? string.Empty))
                continue;

            var endpoint = account.Data.Properties?.Endpoint;
            if (string.IsNullOrEmpty(endpoint))
                continue;

            try
            {
                await foreach (var deployment in account.GetCognitiveServicesAccountDeployments().GetAllAsync(ct))
                {
                    var model = deployment.Data.Properties?.Model;
                    deployments.Add(new AzureFoundryDeployment(
                        account.Data.Name,
                        endpoint,
                        deployment.Data.Name,
                        model?.Name ?? deployment.Data.Name,
                        model?.Format,
                        account.Data.Location.ToString() ?? string.Empty));
                }
            }
            catch (global::Azure.RequestFailedException ex)
            {
                // A resource we can enumerate but not read deployments for (RBAC) — skip it.
                this._logger.LogDebug(ex, "Skipping deployments for account {Account}: {Message}",
                    account.Data.Name, ex.Message);
            }
        }

        return deployments;
    }
}
