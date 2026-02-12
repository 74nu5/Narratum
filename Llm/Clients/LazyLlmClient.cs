using Narratum.Core;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Clients;

/// <summary>
/// Lazy wrapper for ILlmClient that defers async initialization until first use.
/// Prevents blocking application startup with Foundry Local initialization.
/// </summary>
internal sealed class LazyLlmClient : ILlmClient
{
    private readonly ILlmClientFactory _factory;
    private ILlmClient? _client;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LazyLlmClient(ILlmClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public string ClientName => _client?.ClientName ?? "Lazy(Not Initialized)";
    public bool IsMock => _client?.IsMock ?? false;

    public async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _client!.GenerateAsync(request, cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _client!.IsHealthyAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return; // Double-check after acquiring lock

            _client = await _factory.CreateClientAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
