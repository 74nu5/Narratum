using System.Runtime.CompilerServices;
using Narratum.Core;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;

namespace Narratum.Llm.Clients;

/// <summary>
/// Lazy wrapper for ILlmClient that defers async initialization until first use.
/// Prevents blocking application startup with Foundry Local initialization.
/// </summary>
internal sealed class LazyLlmClient : ILlmClient, IStreamingLlmClient, IModelCatalogProvider, IDisposable
{
    private readonly ILlmClientFactory _factory;
    private ILlmClient? _client;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;

    public LazyLlmClient(ILlmClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public string ClientName => _client?.ClientName ?? "Lazy(Not Initialized)";

    public async Task<Result<LlmResponse>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);
        return await _client!.GenerateAsync(request, cancellationToken);
    }

    public async Task<Result<T>> GenerateStructuredAsync<T>(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);
        // Forward to the concrete client so its native strict-schema path is used,
        // not the interface default.
        return await _client!.GenerateStructuredAsync<T>(request, cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);
        return await _client!.IsHealthyAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        if (_client is IStreamingLlmClient streamingClient)
        {
            await foreach (var chunk in streamingClient
                .GenerateStreamingAsync(request, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            // Underlying client does not support streaming: emit the full response once.
            var result = await _client!.GenerateAsync(request, cancellationToken);
            if (result is Result<LlmResponse>.Success success)
                yield return success.Value.Content;
        }
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

    public async Task<IReadOnlyList<LlmModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        return _client is IModelCatalogProvider provider
            ? await provider.GetModelsAsync(cancellationToken)
            : Array.Empty<LlmModelInfo>();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LazyLlmClient));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _initLock?.Dispose();

        if (_client is IDisposable disposableClient)
            disposableClient.Dispose();

        _disposed = true;
    }
}
