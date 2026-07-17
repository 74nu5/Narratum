using FluentAssertions;
using Moq;
using Narratum.Core;
using Narratum.Llm.Clients;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;
using Xunit;

namespace Narratum.Llm.Tests.Clients;

public class LazyLlmClientTests : IDisposable
{
    private readonly Mock<ILlmClientFactory> _mockFactory;
    private readonly Mock<ILlmClient> _mockClient;
    private LazyLlmClient? _lazyClient;

    public LazyLlmClientTests()
    {
        _mockFactory = new Mock<ILlmClientFactory>();
        _mockClient = new Mock<ILlmClient>();

        _mockClient.Setup(c => c.ClientName).Returns("MockClient");
        _mockFactory
            .Setup(f => f.CreateClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockClient.Object);
    }

    [Fact]
    public async Task GenerateAsync_FirstCall_InitializesClient()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = new LlmRequest("Test prompt");

        _mockClient
            .Setup(c => c.GenerateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmResponse>.Ok(new LlmResponse("Response", 10, 5)));

        // Act
        var result = await _lazyClient.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockClient.Verify(c => c.GenerateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_MultipleCalls_InitializesOnlyOnce()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request1 = new LlmRequest("Prompt 1");
        var request2 = new LlmRequest("Prompt 2");

        _mockClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmResponse>.Ok(new LlmResponse("Response", 10, 5)));

        // Act
        await _lazyClient.GenerateAsync(request1);
        await _lazyClient.GenerateAsync(request2);

        // Assert
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once,
            "Factory should be called only once for initialization");
        _mockClient.Verify(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2),
            "Client should be called for each request");
    }

    [Fact]
    public async Task GenerateAsync_ConcurrentCalls_InitializesOnlyOnce()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = new LlmRequest("Concurrent prompt");

        var initializationDelay = TimeSpan.FromMilliseconds(100);
        _mockFactory
            .Setup(f => f.CreateClientAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(initializationDelay, ct);
                return _mockClient.Object;
            });

        _mockClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmResponse>.Ok(new LlmResponse("Response", 10, 5)));

        // Act - Fire 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _lazyClient.GenerateAsync(request)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once,
            "Even with concurrent calls, initialization should happen only once (race condition test)");
    }

    [Fact]
    public async Task IsHealthyAsync_WhenNotInitialized_InitializesFirst()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);

        _mockClient
            .Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var isHealthy = await _lazyClient.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClientName_BeforeInitialization_ReturnsLazyName()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);

        // Act
        var name = _lazyClient.ClientName;

        // Assert
        name.Should().Be("Lazy(Not Initialized)");
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Never,
            "Getting name should not trigger initialization");
    }

    [Fact]
    public async Task ClientName_AfterInitialization_ReturnsRealName()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = new LlmRequest("Init prompt");

        _mockClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmResponse>.Ok(new LlmResponse("Response", 10, 5)));

        // Act
        await _lazyClient.GenerateAsync(request);
        var name = _lazyClient.ClientName;

        // Assert
        name.Should().Be("MockClient");
    }

    [Fact]
    public void Dispose_DisposesClient()
    {
        // Arrange
        var disposableMock = _mockClient.As<IDisposable>();
        _lazyClient = new LazyLlmClient(_mockFactory.Object);

        // Act
        _lazyClient.Dispose();

        // Assert - Dispose should be safe even if not initialized
        _lazyClient.Dispose(); // Second dispose should be safe
    }

    [Fact]
    public async Task Dispose_AfterInitialization_DisposesUnderlyingClient()
    {
        // Arrange
        var disposableClient = new Mock<ILlmClient>();
        disposableClient.Setup(c => c.ClientName).Returns("DisposableClient");
        disposableClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmResponse>.Ok(new LlmResponse("Response", 10, 5)));

        var disposableMock = disposableClient.As<IDisposable>();

        _mockFactory
            .Setup(f => f.CreateClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(disposableClient.Object);

        _lazyClient = new LazyLlmClient(_mockFactory.Object);

        var request = new LlmRequest("Init prompt");
        await _lazyClient.GenerateAsync(request);

        // Act
        _lazyClient.Dispose();

        // Assert
        disposableMock.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        _lazyClient.Dispose();

        var request = new LlmRequest("Test");

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _lazyClient.GenerateAsync(request));
    }

    [Fact]
    public async Task IsHealthyAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        _lazyClient.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _lazyClient.IsHealthyAsync());
    }

    [Fact]
    public async Task GenerateAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        _lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = new LlmRequest("Test");
        var cts = new CancellationToken(canceled: true);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _lazyClient.GenerateAsync(request, cts));
    }

    public void Dispose()
    {
        _lazyClient?.Dispose();
    }
}
