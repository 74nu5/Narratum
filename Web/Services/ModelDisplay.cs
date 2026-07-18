using Narratum.Llm.Azure;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Formatage lisible d'un identifiant de modèle (local Foundry ou <c>azure:*</c>) pour l'UI,
/// afin qu'on sache toujours d'un coup d'œil quel provider et quel modèle sont utilisés.
/// </summary>
public static class ModelDisplay
{
    /// <summary>Icône du provider : ☁️ pour le cloud (Azure), 💻 pour le local, vide si inconnu.</summary>
    public static string Icon(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId == "N/A")
            return string.Empty;

        return AzureModelRef.IsAzureModel(modelId) ? "☁️" : "💻";
    }

    /// <summary>
    /// Libellé « provider · modèle » lisible. Pour Azure, décode
    /// <c>azure:{endpoint}::{deployment}</c> ; sinon utilise le libellé du catalogue local.
    /// </summary>
    public static string Badge(string? modelId, IReadOnlyList<ModelOption> catalogue)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId == "N/A")
            return "modèle inconnu";

        if (AzureModelRef.IsAzureModel(modelId))
        {
            var (_, deployment) = AzureModelRef.Parse(modelId);
            return $"☁️ {deployment} · Azure (cloud)";
        }

        var label = catalogue.FirstOrDefault(m => m.Id == modelId)?.Label ?? modelId;
        return $"💻 {label} · local";
    }
}
