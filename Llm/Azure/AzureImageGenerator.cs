using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;

using Azure.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Narratum.Core;
using Narratum.Orchestration.Llm;

using OpenAI;
using OpenAI.Images;

namespace Narratum.Llm.Azure;

/// <summary>
/// Génère des images via Azure AI Foundry : l'API images OpenAI-compatible de l'endpoint
/// <c>/openai/v1</c>, authentifiée par Entra ID (aucune clé). Le modèle est encodé comme pour le
/// texte : <c>azure:{endpoint}::{deployment}</c>.
/// </summary>
public sealed class AzureImageGenerator : IImageGenerator
{
    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly int _timeoutSeconds;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ImageClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public AzureImageGenerator(
        TokenCredential credential,
        string scope = "https://ai.azure.com/.default",
        int timeoutSeconds = 300,
        ILogger<AzureImageGenerator>? logger = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _scope = scope;
        _timeoutSeconds = timeoutSeconds;
        _logger = logger ?? NullLogger<AzureImageGenerator>.Instance;
    }

    public bool CanHandle(string? modelId) => AzureModelRef.IsAzureModel(modelId);

    public async Task<Result<ImageResult>> GenerateAsync(
        string prompt, string modelId, CancellationToken cancellationToken = default)
    {
        if (!AzureModelRef.IsAzureModel(modelId))
            return Result<ImageResult>.Fail("Image generation is only available for Azure (cloud) models.");

        var (endpoint, deployment) = AzureModelRef.Parse(modelId);
        var client = GetImageClient(endpoint, deployment);

        try
        {
            var value = await GenerateWithOptionFallbackAsync(client, prompt, deployment, cancellationToken);
            var bytes = value.ImageBytes.ToArray();
            return Result<ImageResult>.Ok(new ImageResult(bytes, DetectExtension(bytes)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<ImageResult>.Fail("Image generation cancelled");
        }
        catch (ClientResultException ex)
        {
            _logger.LogWarning(ex, "Image generation failed for {Model}: {Message}", deployment, ex.Message);
            return Result<ImageResult>.Fail($"Image generation failed ({ex.Status}): {ex.Message}");
        }
    }

    // Some image models reject Size/ResponseFormat; on a 400/422 retry with just the prompt.
    private static async Task<GeneratedImage> GenerateWithOptionFallbackAsync(
        ImageClient client, string prompt, string deployment, CancellationToken ct)
    {
        var options = new ImageGenerationOptions
        {
            Size = GeneratedImageSize.W1024xH1024,
            ResponseFormat = GeneratedImageFormat.Bytes,
        };

        try
        {
            return await client.GenerateImageAsync(prompt, options, ct);
        }
        catch (ClientResultException ex) when (ex.Status is 400 or 422)
        {
            // Retry with defaults, but still ask for bytes so we can persist the result.
            var minimal = new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes };
            return await client.GenerateImageAsync(prompt, minimal, ct);
        }
    }

    private ImageClient GetImageClient(string endpoint, string deployment)
    {
        return _clients.GetOrAdd($"{endpoint}|{deployment}", _ =>
        {
            var v1Endpoint = new Uri($"{endpoint.TrimEnd('/')}/openai/v1/");
#pragma warning disable OPENAI001 // BearerTokenPolicy auth on OpenAIClient is an evaluation API.
            var tokenPolicy = new BearerTokenPolicy(_credential, _scope);
            var openAiClient = new OpenAIClient(
                authenticationPolicy: tokenPolicy,
                options: new OpenAIClientOptions
                {
                    Endpoint = v1Endpoint,
                    NetworkTimeout = TimeSpan.FromSeconds(_timeoutSeconds),
                });
#pragma warning restore OPENAI001
            return openAiClient.GetImageClient(deployment);
        });
    }

    /// <summary>Détecte le format d'image d'après les octets magiques (les modèles varient : PNG, JPEG…).</summary>
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
}
