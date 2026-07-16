# Phase 4 — Intégration LLM Locale

**Status**: ✅ COMPLÈTE  
**Phase**: Phase 4 — LLM Local Integration  
**Dependencies**: Phase 1-3 (✅ COMPLETE)  
**Date de finalisation**: Janvier 2026

---

## 📋 Vue d'ensemble

Phase 4 implémente l'**abstraction LLM** et l'intégration de **Foundry Local** pour permettre la génération de texte 100% locale, sans dépendance cloud. Le système garantit que l'architecture reste propre et que le LLM peut être facilement remplacé ou mocké.

### Objectifs Atteints

✅ **Abstraction ILlmClient** - Interface découplée du provider  
✅ **Foundry Local** - Intégration LLM local haute performance  
✅ **Lazy Initialization** - Chargement asynchrone pour startup rapide  
✅ **Mode Mock** - Tests sans dépendance LLM réel  
✅ **Error Handling** - Gestion robuste des erreurs  
✅ **Configuration Flexible** - Paramètres ajustables  

---

## 🏗️ Architecture

### Modules

```
Narratum.Llm/
├── Abstractions/
│   ├── ILlmClient.cs               # Interface principale
│   ├── ILlmClientFactory.cs        # Factory pattern
│   └── LlmOptions.cs               # Options de configuration
│
├── Models/
│   ├── LlmRequest.cs               # Requête standardisée
│   ├── LlmResponse.cs              # Réponse standardisée
│   ├── LlmMessage.cs               # Message conversation
│   └── LlmError.cs                 # Erreurs typées
│
├── Providers/
│   ├── FoundryLocalClient.cs       # Implémentation Foundry
│   ├── MockLlmClient.cs            # Mock pour tests
│   └── LlmClientBase.cs            # Classe de base
│
├── Services/
│   ├── LlmService.cs               # Service orchestration
│   ├── LazyLlmWrapper.cs           # Lazy initialization
│   └── LlmRetryPolicy.cs           # Retry logic
│
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## 🔌 Interface ILlmClient

### Définition

```csharp
public interface ILlmClient : IDisposable
{
    /// <summary>
    /// Nom du provider (ex: "FoundryLocal", "Mock")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indique si le client est initialisé
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initialise le client de manière asynchrone
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère une completion simple
    /// </summary>
    Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère une completion avec streaming
    /// </summary>
    IAsyncEnumerable<LlmResponse> StreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Teste la santé du client
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
```

---

## 📦 Modèles de Données

### LlmRequest

```csharp
public record LlmRequest
{
    /// <summary>
    /// Prompt système (contexte, instructions)
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Prompt utilisateur (requête principale)
    /// </summary>
    public string UserPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Historique de conversation (optionnel)
    /// </summary>
    public List<LlmMessage> Messages { get; init; } = new();

    /// <summary>
    /// Température (0.0 = déterministe, 1.0 = créatif)
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Nombre maximum de tokens
    /// </summary>
    public int MaxTokens { get; init; } = 500;

    /// <summary>
    /// Top-p (nucleus sampling)
    /// </summary>
    public double TopP { get; init; } = 0.9;

    /// <summary>
    /// Métadonnées additionnelles
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Timeout pour la requête
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
```

### LlmResponse

```csharp
public record LlmResponse
{
    /// <summary>
    /// Texte généré
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Nom du modèle utilisé
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Nombre de tokens dans le prompt
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Nombre de tokens dans la completion
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Timestamp de génération
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Raison d'arrêt (stop, length, etc.)
    /// </summary>
    public string FinishReason { get; init; } = "stop";

    /// <summary>
    /// Indique si c'est une erreur
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Message d'erreur si IsError = true
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Métadonnées de la réponse
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

### LlmMessage

```csharp
public record LlmMessage
{
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum MessageRole
{
    System,
    User,
    Assistant
}
```

---

## 🚀 Foundry Local Client

### Implémentation

```csharp
public class FoundryLocalClient : LlmClientBase
{
    private readonly FoundryLocalOptions _options;
    private readonly HttpClient _httpClient;
    private bool _isInitialized;

    public override string ProviderName => "FoundryLocal";
    public override bool IsInitialized => _isInitialized;

    public FoundryLocalClient(
        FoundryLocalOptions options,
        HttpClient httpClient,
        ILogger<FoundryLocalClient> logger)
        : base(logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public override async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing Foundry Local client...");

            // Tester connexion
            var healthOk = await HealthCheckAsync(cancellationToken);
            if (!healthOk)
            {
                throw new LlmException("Foundry Local health check failed");
            }

            _isInitialized = true;
            _logger.LogInformation("Foundry Local client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Foundry Local client");
            throw;
        }
    }

    public override async Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "Client not initialized. Call InitializeAsync first.");
        }

        try
        {
            // Construire payload Foundry
            var payload = new
            {
                model = _options.ModelName,
                messages = BuildMessages(request),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                top_p = request.TopP,
                stream = false
            };

            // Appel API
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/chat/completions",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<FoundryCompletionResponse>(
                    cancellationToken: cancellationToken);

            return MapToLlmResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate completion");
            return new LlmResponse
            {
                IsError = true,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async IAsyncEnumerable<LlmResponse> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "Client not initialized. Call InitializeAsync first.");
        }

        // Construire payload avec stream = true
        var payload = new
        {
            model = _options.ModelName,
            messages = BuildMessages(request),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var json = line["data: ".Length..];
            if (json == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<FoundryStreamChunk>(json);
            if (chunk?.Choices?.Length > 0)
            {
                yield return new LlmResponse
                {
                    Content = chunk.Choices[0].Delta?.Content ?? string.Empty,
                    ModelName = _options.ModelName,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
    }

    public override async Task<bool> HealthCheckAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "/v1/models",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private List<object> BuildMessages(LlmRequest request)
    {
        var messages = new List<object>();

        // System prompt
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new
            {
                role = "system",
                content = request.SystemPrompt
            });
        }

        // Historique
        foreach (var msg in request.Messages)
        {
            messages.Add(new
            {
                role = msg.Role.ToString().ToLower(),
                content = msg.Content
            });
        }

        // User prompt
        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            messages.Add(new
            {
                role = "user",
                content = request.UserPrompt
            });
        }

        return messages;
    }
}
```

---

## ⚡ Lazy Initialization

### Problème

Au démarrage de l'application Web, initialiser le LLM de manière synchrone bloque le startup et augmente le temps de démarrage.

### Solution: LazyLlmWrapper

```csharp
public class LazyLlmWrapper : ILlmClient
{
    private readonly Lazy<Task<ILlmClient>> _lazyClient;
    private readonly ILlmClientFactory _factory;
    private readonly ILogger<LazyLlmWrapper> _logger;

    public string ProviderName => "LazyWrapper";
    public bool IsInitialized { get; private set; }

    public LazyLlmWrapper(
        ILlmClientFactory factory,
        ILogger<LazyLlmWrapper> logger)
    {
        _factory = factory;
        _logger = logger;
        
        // Lazy avec initialisation thread-safe
        _lazyClient = new Lazy<Task<ILlmClient>>(
            InitializeClientAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private async Task<ILlmClient> InitializeClientAsync()
    {
        _logger.LogInformation("Lazy initialization of LLM client starting...");
        
        var client = _factory.CreateClient();
        await client.InitializeAsync();
        
        IsInitialized = true;
        _logger.LogInformation("LLM client initialized successfully");
        
        return client;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Force initialization
        await _lazyClient.Value;
    }

    public async Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient.Value;
        return await client.GenerateAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<LlmResponse> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient.Value;
        await foreach (var response in client.StreamAsync(request, cancellationToken))
        {
            yield return response;
        }
    }

    public async Task<bool> HealthCheckAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_lazyClient.IsValueCreated)
            return false;

        var client = await _lazyClient.Value;
        return await client.HealthCheckAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_lazyClient.IsValueCreated)
        {
            _lazyClient.Value.Result?.Dispose();
        }
    }
}
```

### Avantages

1. ✅ **Startup rapide** - Application Web démarre immédiatement
2. ✅ **Init on demand** - LLM initialisé à la première utilisation
3. ✅ **Thread-safe** - Garantie une seule initialization
4. ✅ **Transparent** - Même interface ILlmClient

---

## 🔁 Retry Policy

### Implémentation

```csharp
public class LlmRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly ILogger _logger;

    public LlmRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<LlmResponse> ExecuteWithRetryAsync(
        Func<CancellationToken, Task<LlmResponse>> action,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var delay = _initialDelay;

        while (true)
        {
            attempt++;

            try
            {
                var response = await action(cancellationToken);
                
                // Si pas d'erreur, retourner
                if (!response.IsError)
                    return response;

                // Si erreur et pas de retries restants, retourner erreur
                if (attempt >= _maxRetries)
                {
                    _logger.LogError(
                        "All {MaxRetries} retry attempts failed. Last error: {Error}",
                        _maxRetries,
                        response.ErrorMessage);
                    return response;
                }

                // Log et retry
                _logger.LogWarning(
                    "Attempt {Attempt}/{MaxRetries} failed: {Error}. Retrying in {Delay}ms...",
                    attempt,
                    _maxRetries,
                    response.ErrorMessage,
                    delay.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                if (attempt >= _maxRetries)
                {
                    _logger.LogError(ex,
                        "All {MaxRetries} retry attempts failed with exception",
                        _maxRetries);
                    throw;
                }

                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxRetries} threw exception. Retrying in {Delay}ms...",
                    attempt,
                    _maxRetries,
                    delay.TotalMilliseconds);
            }

            // Attendre avant retry (exponential backoff)
            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
        }
    }
}
```

### Utilisation

```csharp
public class GenerationService
{
    private readonly ILlmClient _llmClient;
    private readonly LlmRetryPolicy _retryPolicy;

    public async Task<string> GenerateNarrativeAsync(string prompt)
    {
        var request = new LlmRequest
        {
            UserPrompt = prompt,
            Temperature = 0.7,
            MaxTokens = 500
        };

        var response = await _retryPolicy.ExecuteWithRetryAsync(
            async ct => await _llmClient.GenerateAsync(request, ct));

        if (response.IsError)
        {
            throw new GenerationException(response.ErrorMessage);
        }

        return response.Content;
    }
}
```

---

## 🎭 Mock LLM Client (Tests)

### Implémentation

```csharp
public class MockLlmClient : ILlmClient
{
    private readonly Dictionary<string, string> _responses = new();
    
    public string ProviderName => "Mock";
    public bool IsInitialized { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public void RegisterResponse(string promptPattern, string response)
    {
        _responses[promptPattern] = response;
    }

    public Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Chercher réponse mockée
        var response = _responses
            .FirstOrDefault(kvp => request.UserPrompt.Contains(kvp.Key, 
                StringComparison.OrdinalIgnoreCase))
            .Value ?? "Mock response";

        return Task.FromResult(new LlmResponse
        {
            Content = response,
            ModelName = "mock-model",
            PromptTokens = 10,
            CompletionTokens = 20
        });
    }

    public async IAsyncEnumerable<LlmResponse> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GenerateAsync(request, cancellationToken);
        var words = response.Content.Split(' ');

        foreach (var word in words)
        {
            yield return new LlmResponse
            {
                Content = word + " ",
                ModelName = "mock-model"
            };

            await Task.Delay(10, cancellationToken); // Simuler latence
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsInitialized);
    }

    public void Dispose() { }
}
```

### Utilisation dans Tests

```csharp
[Fact]
public async Task GenerationService_UsesLlm_Successfully()
{
    // Arrange
    var mockLlm = new MockLlmClient();
    mockLlm.RegisterResponse(
        "generate narrative",
        "The hero ventured into the dark forest...");

    var service = new GenerationService(mockLlm);

    // Act
    var result = await service.GenerateNarrativeAsync("generate narrative");

    // Assert
    result.Should().Contain("hero");
    result.Should().Contain("forest");
}
```

---

## ⚙️ Configuration

### appsettings.json

```json
{
  "Llm": {
    "Provider": "FoundryLocal",
    "FoundryLocal": {
      "BaseUrl": "http://localhost:11434",
      "ModelName": "mistral:latest",
      "Timeout": "00:02:00",
      "ApiKey": null
    },
    "DefaultOptions": {
      "Temperature": 0.7,
      "MaxTokens": 500,
      "TopP": 0.9
    },
    "Retry": {
      "MaxAttempts": 3,
      "InitialDelayMs": 1000,
      "EnableExponentialBackoff": true
    },
    "UseLazyInitialization": true
  }
}
```

### Enregistrement Services

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLlmClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var llmConfig = configuration.GetSection("Llm");
        
        services.Configure<LlmOptions>(llmConfig);
        services.Configure<FoundryLocalOptions>(llmConfig.GetSection("FoundryLocal"));
        
        // HttpClient pour Foundry
        services.AddHttpClient<FoundryLocalClient>(client =>
        {
            var baseUrl = llmConfig.GetValue<string>("FoundryLocal:BaseUrl");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.Parse(
                llmConfig.GetValue<string>("FoundryLocal:Timeout"));
        });

        // Factory
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        // Client (avec ou sans lazy)
        var useLazy = llmConfig.GetValue<bool>("UseLazyInitialization");
        
        if (useLazy)
        {
            services.AddSingleton<ILlmClient, LazyLlmWrapper>();
        }
        else
        {
            services.AddSingleton<ILlmClient>(sp =>
            {
                var factory = sp.GetRequiredService<ILlmClientFactory>();
                var client = factory.CreateClient();
                client.InitializeAsync().Wait();
                return client;
            });
        }

        // Retry policy
        services.AddSingleton<LlmRetryPolicy>();

        return services;
    }
}
```

---

## 🧪 Tests

### Structure

```
Llm.Tests/
├── Abstractions/
│   └── ILlmClientTests.cs
├── Providers/
│   ├── FoundryLocalClientTests.cs
│   └── MockLlmClientTests.cs
├── Services/
│   ├── LazyLlmWrapperTests.cs
│   └── LlmRetryPolicyTests.cs
└── Integration/
    └── EndToEndLlmTests.cs
```

### Exemples

```csharp
[Fact]
public async Task FoundryLocalClient_GeneratesCompletion()
{
    // Arrange
    var client = CreateFoundryClient();
    await client.InitializeAsync();

    var request = new LlmRequest
    {
        SystemPrompt = "You are a helpful assistant.",
        UserPrompt = "Say hello in French.",
        Temperature = 0.5,
        MaxTokens = 50
    };

    // Act
    var response = await client.GenerateAsync(request);

    // Assert
    response.Should().NotBeNull();
    response.IsError.Should().BeFalse();
    response.Content.Should().Contain("Bonjour");
    response.TotalTokens.Should().BeGreaterThan(0);
}

[Fact]
public async Task LazyLlmWrapper_InitializesOnFirstUse()
{
    // Arrange
    var wrapper = CreateLazyWrapper();

    // Assert - not initialized yet
    wrapper.IsInitialized.Should().BeFalse();

    // Act
    await wrapper.GenerateAsync(new LlmRequest { UserPrompt = "test" });

    // Assert - now initialized
    wrapper.IsInitialized.Should().BeTrue();
}

[Fact]
public async Task RetryPolicy_RetriesOnError()
{
    // Arrange
    var attempts = 0;
    var policy = new LlmRetryPolicy(maxRetries: 3);

    Func<CancellationToken, Task<LlmResponse>> failTwiceThenSucceed = ct =>
    {
        attempts++;
        if (attempts < 3)
        {
            return Task.FromResult(new LlmResponse
            {
                IsError = true,
                ErrorMessage = "Temporary error"
            });
        }
        return Task.FromResult(new LlmResponse { Content = "Success!" });
    };

    // Act
    var response = await policy.ExecuteWithRetryAsync(failTwiceThenSucceed);

    // Assert
    attempts.Should().Be(3);
    response.IsError.Should().BeFalse();
    response.Content.Should().Be("Success!");
}
```

---

## 📊 Métriques

| Métrique | Valeur |
|----------|--------|
| **Fichiers créés** | ~20 fichiers |
| **Lignes de code** | ~2,000 lignes |
| **Tests** | ~30 tests |
| **Providers** | 2 (Foundry, Mock) |
| **Coverage** | ~80% |

---

## 🎯 Points Clés

### Succès

1. ✅ **Abstraction propre** - ILlmClient découple l'implémentation
2. ✅ **100% local** - Aucune dépendance cloud
3. ✅ **Performant** - Lazy init pour startup rapide
4. ✅ **Testable** - MockLlmClient pour tests sans LLM réel
5. ✅ **Résilient** - Retry policy automatique

### Défis Résolus

1. ✅ **Startup lent** - LazyLlmWrapper résout le problème
2. ✅ **Erreurs réseau** - Retry policy avec exponential backoff
3. ✅ **Tests lents** - Mock client pour tests rapides
4. ✅ **Configuration** - Options flexibles par environnement

---

## 🚀 Utilisation

### Exemple Simple

```csharp
public class NarrativeGenerator
{
    private readonly ILlmClient _llmClient;

    public NarrativeGenerator(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<string> GenerateAsync(string prompt)
    {
        var request = new LlmRequest
        {
            SystemPrompt = "Tu es un narrateur expert en fantasy.",
            UserPrompt = prompt,
            Temperature = 0.7,
            MaxTokens = 500
        };

        var response = await _llmClient.GenerateAsync(request);

        if (response.IsError)
        {
            throw new Exception($"LLM error: {response.ErrorMessage}");
        }

        return response.Content;
    }
}
```

### Exemple avec Streaming

```csharp
public async Task StreamNarrativeAsync(string prompt)
{
    var request = new LlmRequest
    {
        UserPrompt = prompt,
        Temperature = 0.7
    };

    await foreach (var chunk in _llmClient.StreamAsync(request))
    {
        Console.Write(chunk.Content);
        await Task.Delay(50); // Effet typewriter
    }
}
```

---

## 📝 Prochaines Améliorations

### Court Terme
- [ ] Support autres providers (Ollama, llama.cpp direct)
- [ ] Metrics et telemetry
- [ ] Cache de réponses

### Moyen Terme
- [ ] Fine-tuning local
- [ ] Prompt engineering tools
- [ ] A/B testing prompts

---

**Phase 4 apporte l'IA locale à Narratum tout en préservant l'architecture propre et testable.** 🤖

---

**Dernière mise à jour** : 16 Juillet 2026  
**Statut** : ✅ COMPLÈTE
