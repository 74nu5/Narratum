using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Azure.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Narratum.Core;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Azure;

/// <summary>
/// Génère des images sur Azure AI Foundry via HTTP brut, avec un routage PAR MODÈLE — les modèles
/// image d'une ressource Foundry sont servis sur l'host <c>services.ai.azure.com</c> (pas
/// <c>cognitiveservices.azure.com</c> que renvoie la découverte), chacun sur sa propre route :
///  - MAI-Image → <c>/mai/v1/images/generations</c> (width/height)
///  - FLUX.2 (pro/flex) → <c>/providers/blackforestlabs/v1/{flux-2-pro|flux-2-flex}?api-version=preview</c>
///  - reste (dall-e, gpt-image, FLUX.1) → <c>/openai/v1/images/generations?api-version=preview</c>
/// Le nom de déploiement est envoyé comme <c>model</c>. Auth : Entra ID (bearer, scope
/// cognitiveservices). Toutes renvoient l'image en <c>data[0].b64_json</c> ou <c>data[0].url</c>.
/// Mécanisme repris du projet GenerateMultiImage (vérifié contre des déploiements réels).
/// </summary>
public sealed class AzureImageGenerator : IImageGenerator, IDisposable
{
    private const string TokenScope = "https://cognitiveservices.azure.com/.default";

    private readonly TokenCredential _credential;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private bool _disposed;

    public AzureImageGenerator(
        TokenCredential credential,
        int timeoutSeconds = 300,
        ILogger<AzureImageGenerator>? logger = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = logger ?? NullLogger<AzureImageGenerator>.Instance;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    }

    public bool CanHandle(string? modelId) => AzureModelRef.IsAzureModel(modelId);

    public async Task<Result<ImageResult>> GenerateAsync(
        string prompt, string modelId, CancellationToken cancellationToken = default)
    {
        if (!AzureModelRef.IsAzureModel(modelId))
            return Result<ImageResult>.Fail("Image generation is only available for Azure (cloud) models.");

        var (endpoint, deployment) = AzureModelRef.Parse(modelId);
        var baseUrl = BuildFoundryBaseUrl(endpoint);

        try
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext([TokenScope]), cancellationToken);

            var (url, body) = BuildRequest(deployment, prompt, baseUrl);
            _logger.LogDebug("Azure image POST {Url} (model {Model})", url, deployment);

            var bytes = await SendAndReadImageAsync(url, body, token.Token, cancellationToken);
            return bytes is null
                ? Result<ImageResult>.Fail($"Image generation returned no image for {deployment}")
                : Result<ImageResult>.Ok(new ImageResult(bytes, DetectExtension(bytes)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<ImageResult>.Fail("Image generation cancelled");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Azure image network error for {Model}: {Message}", deployment, ex.Message);
            return Result<ImageResult>.Fail($"Image network error: {ex.Message}");
        }
    }

    /// <summary>Chooses the route and body for a model family (matched on the deployment name).</summary>
    private static (string Url, Dictionary<string, object> Body) BuildRequest(string model, string prompt, string baseUrl)
    {
        var norm = Normalize(model);

        if (norm.StartsWith("maiimage", StringComparison.Ordinal))
        {
            return (
                $"{baseUrl}/mai/v1/images/generations",
                new() { ["model"] = model, ["prompt"] = prompt, ["width"] = 1024, ["height"] = 1024 });
        }

        if (TryGetBflModelPath(norm, out var bflPath))
        {
            return (
                $"{baseUrl}/providers/blackforestlabs/v1/{bflPath}?api-version=preview",
                new()
                {
                    ["model"] = model,
                    ["prompt"] = prompt,
                    ["width"] = 1024,
                    ["height"] = 1024,
                    ["output_format"] = "png",
                    ["num_images"] = 1,
                });
        }

        // dall-e, gpt-image, FLUX.1 — the OpenAI-compatible v1 image API.
        return (
            $"{baseUrl}/openai/v1/images/generations?api-version=preview",
            new() { ["model"] = model, ["prompt"] = prompt, ["n"] = 1 });
    }

    private async Task<byte[]?> SendAndReadImageAsync(
        string url, Dictionary<string, object> body, string entraToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", entraToken);
        // Pre-serialise to StringContent so the request carries a Content-Length header; JsonContent
        // streams chunked (no Content-Length), which the Foundry endpoints reject with 400.
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var snippet = json.Length > 400 ? json[..400] : json;
            _logger.LogWarning("Azure image HTTP {Status}: {Body}", (int)response.StatusCode, snippet);
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {snippet}");
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            return null;

        var first = data[0];

        foreach (var key in (string[])["b64_json", "image", "image_base64"])
            if (first.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 } b64)
                return Convert.FromBase64String(b64);

        foreach (var key in (string[])["url", "sample"])
            if (first.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 } imageUrl)
                return await _http.GetByteArrayAsync(imageUrl, ct);

        return null;
    }

    /// <summary>
    /// Image models live on the resource's <c>services.ai.azure.com</c> host. Discovery stores the
    /// <c>cognitiveservices.azure.com</c> endpoint, so rewrite that host; other hosts pass through.
    /// </summary>
    private static string BuildFoundryBaseUrl(string endpoint)
    {
        var host = new Uri(endpoint).Host;
        if (host.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            host = $"{host.Split('.')[0]}.services.ai.azure.com";
        return $"https://{host}";
    }

    private static string Normalize(string model)
        => new(model.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool TryGetBflModelPath(string normalizedModel, out string modelPath)
    {
        (modelPath, var isBfl) = normalizedModel switch
        {
            "flux2pro" => ("flux-2-pro", true),
            "flux2flex" => ("flux-2-flex", true),
            _ => (string.Empty, false),
        };
        return isBfl;
    }

    /// <summary>Détecte le format d'après les octets magiques (les modèles varient : PNG, JPEG, WebP).</summary>
    private static string DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "jpg";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[8] == 0x57 && bytes[9] == 0x45)
            return "webp";
        return "png";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _http.Dispose();
    }
}
