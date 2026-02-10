using System.Diagnostics;
using Narratum.Core;

namespace Narratum.Orchestration.Llm;

/// <summary>
/// Configuration pour le MockLlmClient.
/// </summary>
public sealed record MockLlmConfig
{
    /// <summary>
    /// Délai simulé pour les réponses.
    /// </summary>
    public TimeSpan SimulatedDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Texte par défaut retourné.
    /// </summary>
    public string DefaultResponse { get; init; } = "[MOCK] Generated narrative content.";

    /// <summary>
    /// Taux d'échec simulé (0.0 = jamais, 1.0 = toujours).
    /// </summary>
    public double FailureRate { get; init; } = 0.0;

    /// <summary>
    /// Nombre simulé de tokens par caractère.
    /// </summary>
    public double TokensPerCharacter { get; init; } = 0.25;

    /// <summary>
    /// Réponses personnalisées par pattern de prompt.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomResponses { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MockLlmConfig Default => new();

    /// <summary>
    /// Configuration pour les tests (sans délai).
    /// </summary>
    public static MockLlmConfig ForTesting => new()
    {
        SimulatedDelay = TimeSpan.Zero,
        DefaultResponse = "[TEST] Mock response."
    };

    /// <summary>
    /// Configuration "stupide" - retourne toujours le même texte.
    /// </summary>
    public static MockLlmConfig Stupid => new()
    {
        SimulatedDelay = TimeSpan.Zero,
        DefaultResponse = "TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE"
    };
}

/// <summary>
/// Client LLM mock pour le développement et les tests.
///
/// Ce client simule le comportement d'un LLM réel sans appeler de modèle.
/// Il permet de valider que l'architecture fonctionne indépendamment
/// de la qualité ou de la disponibilité d'un LLM réel.
///
/// Principe Phase 3 : "Le système doit fonctionner même si le LLM est stupide."
/// </summary>
public sealed class MockLlmClient : ILlmClient
{
    private readonly MockLlmConfig _config;
    private readonly Random _random;
    private int _requestCount;

    public string ClientName => "MockLlmClient";
    public bool IsMock => true;

    /// <summary>
    /// Nombre de requêtes traitées.
    /// </summary>
    public int RequestCount => _requestCount;

    public MockLlmClient(MockLlmConfig? config = null)
    {
        _config = config ?? MockLlmConfig.Default;
        _random = new Random();
        _requestCount = 0;
    }

    public async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Interlocked.Increment(ref _requestCount);
        var stopwatch = Stopwatch.StartNew();

        // Simuler le délai
        if (_config.SimulatedDelay > TimeSpan.Zero)
        {
            await Task.Delay(_config.SimulatedDelay, cancellationToken);
        }

        // Simuler un échec aléatoire
        if (_config.FailureRate > 0 && _random.NextDouble() < _config.FailureRate)
        {
            return Result<LlmResponse>.Fail("Mock LLM simulated failure");
        }

        // Générer le contenu
        var content = GenerateContent(request);

        stopwatch.Stop();

        // Calculer les tokens simulés
        var promptTokens = EstimateTokens(request.SystemPrompt + request.UserPrompt);
        var completionTokens = EstimateTokens(content);

        var response = new LlmResponse(
            requestId: request.RequestId,
            content: content,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            generationDuration: stopwatch.Elapsed,
            metadata: new Dictionary<string, object>
            {
                ["mock"] = true,
                ["config"] = _config.GetType().Name,
                ["requestNumber"] = _requestCount
            });

        return Result<LlmResponse>.Ok(response);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Génère le contenu de la réponse.
    /// </summary>
    private string GenerateContent(LlmRequest request)
    {
        // Chercher une réponse personnalisée
        foreach (var (pattern, response) in _config.CustomResponses)
        {
            if (request.UserPrompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }
        }

        // Retourner la réponse par défaut
        return _config.DefaultResponse;
    }

    /// <summary>
    /// Estime le nombre de tokens dans un texte.
    /// </summary>
    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return (int)(text.Length * _config.TokensPerCharacter);
    }
}

/// <summary>
/// Client LLM "stupide" pour valider la robustesse du système.
/// Retourne toujours le même texte, peu importe le prompt.
/// </summary>
public sealed class StupidLlmClient : ILlmClient
{
    private const string StupidResponse = "TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE";

    public string ClientName => "StupidLlmClient";
    public bool IsMock => true;

    public Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new LlmResponse(
            requestId: request.RequestId,
            content: StupidResponse,
            promptTokens: 10,
            completionTokens: 5,
            generationDuration: TimeSpan.FromMilliseconds(1),
            metadata: new Dictionary<string, object> { ["stupid"] = true });

        return Task.FromResult(Result<LlmResponse>.Ok(response));
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
