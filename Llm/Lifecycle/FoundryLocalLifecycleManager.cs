namespace Narratum.Llm.Lifecycle;

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Narratum.Llm.Configuration;
using Narratum.Orchestration.Llm;

using LogLevel = Microsoft.AI.Foundry.Local.LogLevel;

/// <summary>
///     Gestionnaire de cycle de vie pour Microsoft Foundry Local.
///     Initialise le SDK, télécharge et charge les modèles, démarre le service web.
/// </summary>
public sealed class FoundryLocalLifecycleManager : ILlmLifecycleManager
{
    private readonly FoundryLocalConfig _config;
    private readonly ILogger _logger;
    private string? _baseUrl;
    private bool _disposed;
    private bool _initialized;

    public FoundryLocalLifecycleManager(
        FoundryLocalConfig config,
        ILogger<FoundryLocalLifecycleManager>? logger = null)
    {
        this._config = config ?? throw new ArgumentNullException(nameof(config));
        this._logger = logger ?? NullLogger<FoundryLocalLifecycleManager>.Instance;
    }

    public string ProviderName
        => "FoundryLocal";

    public async Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        if (!this._initialized)
            await this.InitializeAsync(cancellationToken);

        var result = await FoundryLocalManager.Instance.DownloadAndRegisterEpsAsync(cancellationToken);
        if (!result.Success)
        {
            this._logger.LogError("Failed to download and register EPS.");
            return [];
        }

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await ExpandVariantsAsync(catalog);

        return
        [
            .. models
                .Select(static m => new LlmModelInfo(
                    m.Id,
                    m.Alias ?? m.Info?.Alias ?? m.Id,
                    m.Info?.DisplayName ?? m.Alias ?? m.Id,
                    m.Info?.Cached ?? false,
                    m.Info?.Task,
                    DeriveDevice(m))),
        ];
    }

    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!this._initialized) return false;

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync($"{this._baseUrl}/v1/models", cancellationToken);
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
        if (!this._initialized)
            await this.InitializeAsync(cancellationToken);

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
            if (this._config.AutoDownload && !await model.IsCachedAsync())
            {
                this._logger.LogInformation("Downloading model {Model}...", modelName);
                await model.DownloadAsync(progress =>
                {
                    if (progress % 25 < 1)
                        this._logger.LogInformation("Download progress for {Model}: {Progress:F0}%", modelName, progress);
                });
            }

            this._logger.LogInformation("Loading model {Model} (served id {Id})...", modelName, model.Id);
            await model.LoadAsync();
        }

        this._logger.LogInformation("Model {Model} ready (served id {Id})", modelName, model.Id);

        // Foundry's OpenAI endpoint serves models by their concrete id, not the alias.
        return model.Id;
    }

    public async Task<string> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!this._initialized)
            await this.InitializeAsync(cancellationToken);

        return $"{this._baseUrl}/v1";
    }

    public async ValueTask DisposeAsync()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        if (this._initialized)
        {
            try
            {
                var mgr = FoundryLocalManager.Instance;
                await mgr.StopWebServiceAsync();
                this._logger.LogInformation("Foundry Local service stopped");
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Error stopping Foundry Local service");
            }
        }
    }

    /// <summary>
    ///     Initialise le SDK Foundry Local et démarre le service web (LAZY).
    ///     Cette méthode est appelée automatiquement lors de la première utilisation.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (this._initialized) return;

        this._logger.LogInformation("Initializing Foundry Local (on first use)...");

        var foundryConfig = new Configuration
        {
            AppName = "Narratum",
            LogLevel = LogLevel.Information,
            ModelCacheDir = string.IsNullOrEmpty(this._config.CacheDirectory) ? null : this._config.CacheDirectory,
            Web = new()
            {
                Urls = "http://127.0.0.1:5001",
            },
        };

        // FoundryLocalManager is a process-global singleton, created at most once per process.
        // Another lifecycle-manager instance (e.g. a different Blazor circuit, or a stale one
        // left over after a hot reload) may already have created it. Reuse it in that case
        // instead of calling CreateAsync again, which throws "already been created".
        if (FoundryLocalManager.IsInitialized)
        {
            this._logger.LogInformation("Foundry Local already created; reusing the existing instance.");
        }
        else
        {
            var created = false;
            try
            {
                await FoundryLocalManager.CreateAsync(foundryConfig, this._logger);
                created = true;
            }
            catch (FoundryLocalException) when (FoundryLocalManager.IsInitialized)
            {
                // Lost a race with a concurrent initializer; the singleton now exists — reuse it.
                this._logger.LogInformation("Foundry Local was created concurrently; reusing the existing instance.");
            }

            // Only the instance that actually created the manager starts the web service.
            if (created)
                await FoundryLocalManager.Instance.StartWebServiceAsync();
        }

        this._baseUrl = foundryConfig.Web.Urls;
        this._initialized = true;

        this._logger.LogInformation("Foundry Local initialized successfully at {BaseUrl}", this._baseUrl);
    }

    /// <summary>
    ///     Flattens the catalogue into every concrete, device-specific variant. A model exposes
    ///     several variants (generic-gpu, openvino-npu, generic-cpu, …), each an IModel with its
    ///     own id and Runtime. ListModelsAsync returns only the default (usually CPU) entry, so we
    ///     expand .Variants to surface the GPU/NPU options, de-duplicated by concrete id.
    /// </summary>
    private static async Task<List<IModel>> ExpandVariantsAsync(ICatalog catalog)
    {
        var models = await catalog.ListModelsAsync();
        var byId = new Dictionary<string, IModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in models)
        {
            var modelDetails = await catalog.GetModelAsync(m.Alias);
            if (modelDetails is null)
                continue;

            byId[m.Id] = modelDetails;
            foreach (var v in modelDetails.Variants)
                byId[v.Id] = v;
        }

        return [.. byId.Values];
    }

    // A bare alias (legacy story / agent config) resolves to the most reliable variant:
    // CPU first, then NPU, and WebGPU last since it can emit degenerate output on some machines.
    private static int DeviceRank(IModel m)
        => DeriveDevice(m) switch
        {
            "CPU" => 0,
            "NPU" => 1,
            "GPU" => 2,
            _ => 3,
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
}