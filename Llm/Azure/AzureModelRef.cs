namespace Narratum.Llm.Azure;

/// <summary>
/// Encodage d'un déploiement Azure AI Foundry dans un identifiant de modèle unique
/// (<c>azure:{endpoint}::{deployment}</c>), qui voyage dans <c>llm.model</c> à travers toute
/// la plomberie existante. Le routeur le décode pour cibler le bon endpoint/déploiement.
/// </summary>
public static class AzureModelRef
{
    /// <summary>Préfixe marquant un modèle servi par Azure AI Foundry.</summary>
    public const string Prefix = "azure:";

    private const string EndpointDeploymentSeparator = "::";

    /// <summary>Vrai si l'identifiant désigne un déploiement Azure.</summary>
    public static bool IsAzureModel(string? model)
        => model is not null && model.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Construit l'identifiant composite d'un déploiement Azure.</summary>
    public static string Compose(string endpoint, string deployment)
        => $"{Prefix}{endpoint}{EndpointDeploymentSeparator}{deployment}";

    /// <summary>Décompose un identifiant Azure en (endpoint, déploiement).</summary>
    public static (string Endpoint, string Deployment) Parse(string model)
    {
        if (!IsAzureModel(model))
            throw new InvalidOperationException($"Not an Azure model id: '{model}'");

        var body = model[Prefix.Length..];
        var separatorIndex = body.IndexOf(EndpointDeploymentSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            throw new InvalidOperationException($"Malformed Azure model id: '{model}'");

        return (body[..separatorIndex], body[(separatorIndex + EndpointDeploymentSeparator.Length)..]);
    }
}
