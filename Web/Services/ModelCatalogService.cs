using Narratum.Orchestration.Llm;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Builds the model dropdown from the local provider's REAL catalogue (exact ids and
/// variants), so the UI never guesses model names or picks the wrong variant. Cached
/// app-wide after the first successful load; falls back to the static curated list when
/// the provider can't be queried.
/// </summary>
public class ModelCatalogService
{
    private IReadOnlyList<ModelOption>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<ModelOption>> GetModelsAsync(ILlmClient client, CancellationToken ct = default)
    {
        if (_cache is not null)
            return _cache;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache is not null)
                return _cache;

            _cache = await LoadAsync(client, ct);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<IReadOnlyList<ModelOption>> LoadAsync(ILlmClient client, CancellationToken ct)
    {
        if (client is IModelCatalogProvider provider)
        {
            try
            {
                var models = await provider.GetModelsAsync(ct);
                // Offer EVERY device variant so the user can pick a specific one. Variants of a
                // model stay adjacent, ordered CPU → NPU → GPU: CPU is the reliable baseline and
                // becomes the default selection, while WebGPU builds (which can emit degenerate
                // output on some machines) sit last — available, but never the default.
                var options = models
                    .Where(IsNarrativeModel)
                    .OrderBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(m => DeviceRank(m.Device))
                    .Select(m => new ModelOption(m.Id, BuildLabel(m)))
                    .ToList();

                if (options.Count > 0)
                    return options;
            }
            catch
            {
                // Provider unavailable — fall back to the static list.
            }
        }

        return ModelSelectionService.AvailableModels;
    }

    /// <summary>Keep chat/text models; drop speech, embedding and vision models.</summary>
    private static bool IsNarrativeModel(LlmModelInfo m)
    {
        var probe = (m.Alias + " " + m.Id + " " + (m.Task ?? "")).ToLowerInvariant();
        string[] excluded = { "whisper", "embedding", "asr", "speech", "-vl-", "vision" };
        return !excluded.Any(x => probe.Contains(x));
    }

    /// <summary>Ordering weight so variants list CPU, then NPU, then GPU (least reliable last).</summary>
    private static int DeviceRank(string? device) => device?.ToUpperInvariant() switch
    {
        "CPU" => 0,
        "NPU" => 1,
        "GPU" => 2,
        _ => 3
    };

    private static string BuildLabel(LlmModelInfo m)
    {
        var device = string.IsNullOrEmpty(m.Device) ? "" : $" · {m.Device}";
        var state = m.Cached ? " · en cache" : " · à télécharger";
        return $"{m.Alias}{device}{state}";
    }
}
