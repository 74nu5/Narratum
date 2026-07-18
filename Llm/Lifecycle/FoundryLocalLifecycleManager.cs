using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Lifecycle;

/// <summary>
/// Gestionnaire de cycle de vie pour Microsoft Foundry Local.
/// Initialise le SDK, télécharge et charge les modèles, démarre le service web.
/// </summary>
public sealed class FoundryLocalLifecycleManager : ILlmLifecycleManager
{
    private readonly FoundryLocalConfig _config;
    private readonly ILogger _logger;
    private string? _baseUrl;
    private bool _initialized;
    private bool _disposed;

    public string ProviderName => "FoundryLocal";

    public FoundryLocalLifecycleManager(
        FoundryLocalConfig config,
        ILogger<FoundryLocalLifecycleManager>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<FoundryLocalLifecycleManager>.Instance;
    }

    /// <summary>
    /// Initialise le SDK Foundry Local et démarre le service web (LAZY).
    /// Cette méthode est appelée automatiquement lors de la première utilisation.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Foundry Local (on first use)...");

        var foundryConfig = new Microsoft.AI.Foundry.Local.Configuration
        {
            AppName = "Narratum",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            ModelCacheDir = string.IsNullOrEmpty(_config.CacheDirectory) ? null : _config.CacheDirectory,
            Web = new Microsoft.AI.Foundry.Local.Configuration.WebService
            {
                Urls = "http://127.0.0.1:5001"
            }
        };

        await FoundryLocalManager.CreateAsync(foundryConfig, _logger);
        var mgr = FoundryLocalManager.Instance;

        await mgr.StartWebServiceAsync();
        _baseUrl = foundryConfig.Web.Urls;
        _initialized = true;

        _logger.LogInformation("Foundry Local initialized successfully at {BaseUrl}", _baseUrl);
    }

    public async Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await ExpandVariantsAsync(catalog);

        return models
            .Select(m => new LlmModelInfo(
                Id: m.Id,
                Alias: m.Alias ?? m.Info?.Alias ?? m.Id,
                DisplayName: m.Info?.DisplayName ?? m.Alias ?? m.Id,
                Cached: m.Info?.Cached ?? false,
                Task: m.Info?.Task,
                Device: DeriveDevice(m)))
            .ToList();
    }

    /// <summary>
    /// Flattens the catalogue into every concrete, device-specific variant. A model exposes
    /// several variants (generic-gpu, openvino-npu, generic-cpu, …), each an IModel with its
    /// own id and Runtime. ListModelsAsync returns only the default (usually CPU) entry, so we
    /// expand .Variants to surface the GPU/NPU options, de-duplicated by concrete id.
    /// </summary>
    private static async Task<List<IModel>> ExpandVariantsAsync(ICatalog catalog)
    {
        var models = await catalog.ListModelsAsync();
        var byId = new Dictionary<string, IModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in models)
        {
            byId[m.Id] = m;
            foreach (var v in m.Variants ?? Array.Empty<IModel>())
                byId[v.Id] = v;
        }

        return byId.Values.ToList();
    }

    private static int DeviceRank(IModel m) => DeriveDevice(m) switch
    {
        "GPU" => 0,
        "NPU" => 1,
        "CPU" => 2,
        _ => 3
    };

    /// <summary>Real device from the variant's Runtime; falls back to parsing the id.</summary>
    private static string? DeriveDevice(IModel m)
    {
        switch (m.Info?.Runtime?.DeviceType)
        {
            case DeviceType.GPU: return "GPU";
            case DeviceType.NPU: return "NPU";
            case DeviceType.CPU: return "CPU";
        }

        var id = m.Id;
        if (id.Contains("gpu", StringComparison.OrdinalIgnoreCase)) return "GPU";
        if (id.Contains("npu", StringComparison.OrdinalIgnoreCase)) return "NPU";
        if (id.Contains("cpu", StringComparison.OrdinalIgnoreCase)) return "CPU";
        return null;
    }

    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized) return false;

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync($"{_baseUrl}/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> EnsureModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);

        var mgr = FoundryLocalManager.Instance;
        var catalog = await mgr.GetCatalogAsync();

        // Match the catalogue EXACTLY, across every device variant. catalog.GetModelAsync()
        // does fuzzy matching and can return the wrong model (e.g. "mistral-nemo-12b-instruct"
        // resolved to a "ministral-..." model), so we don't use it.
        // The dropdown sends a concrete variant id (…-generic-gpu:1) — match that first so the
        // requested device is honoured. A bare alias (legacy stories / agent config) falls back
        // to the best available variant (GPU > NPU > CPU).
        var models = await ExpandVariantsAsync(catalog);
        var model = models.FirstOrDefault(m => string.Equals(m.Id, modelName, StringComparison.OrdinalIgnoreCase))
                    ?? models.Where(m => string.Equals(m.Alias, modelName, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(DeviceRank)
                             .FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Model '{modelName}' not found in Foundry Local catalog (exact match on alias/id)");

        if (!await model.IsLoadedAsync())
        {
            if (_config.AutoDownload && !await model.IsCachedAsync())
            {
                _logger.LogInformation("Downloading model {Model}...", modelName);
                await model.DownloadAsync(progress =>
                {
                    if (progress % 25 < 1)
                        _logger.LogInformation("Download progress for {Model}: {Progress:F0}%", modelName, progress);
                });
            }

            _logger.LogInformation("Loading model {Model} (served id {Id})...", modelName, model.Id);
            await model.LoadAsync();
        }

        _logger.LogInformation("Model {Model} ready (served id {Id})", modelName, model.Id);

        // Foundry's OpenAI endpoint serves models by their concrete id, not the alias.
        return model.Id;
    }

    public async Task<string> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);

        return $"{_baseUrl}/v1";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized)
        {
            try
            {
                var mgr = FoundryLocalManager.Instance;
                await mgr.StopWebServiceAsync();
                _logger.LogInformation("Foundry Local service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping Foundry Local service");
            }
        }
    }
}
