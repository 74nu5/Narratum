using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Llm.Configuration;

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
    /// Initialise le SDK Foundry Local et démarre le service web.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Foundry Local...");

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

        _logger.LogInformation("Foundry Local initialized at {BaseUrl}", _baseUrl);
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

    public async Task EnsureModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);

        var mgr = FoundryLocalManager.Instance;
        var catalog = await mgr.GetCatalogAsync();

        var model = await catalog.GetModelAsync(modelName)
            ?? throw new InvalidOperationException($"Model '{modelName}' not found in Foundry Local catalog");

        if (_config.AutoDownload)
        {
            _logger.LogInformation("Downloading model {Model}...", modelName);
            await model.DownloadAsync(progress =>
            {
                if (progress % 25 < 1)
                    _logger.LogInformation("Download progress: {Progress:F0}%", progress);
            });
        }

        _logger.LogInformation("Loading model {Model}...", modelName);
        await model.LoadAsync();
        _logger.LogInformation("Model {Model} loaded successfully", modelName);
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
