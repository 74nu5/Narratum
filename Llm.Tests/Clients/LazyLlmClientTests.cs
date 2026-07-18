using FluentAssertions;
using Moq;
using Narratum.Core;
using Narratum.Llm.Clients;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;
using Xunit;

namespace Narratum.Llm.Tests.Clients;

public sealed class LazyLlmClientTests
{
    private readonly Mock<ILlmClientFactory> _mockFactory;
    private readonly Mock<ILlmClient> _mockClient;

    public LazyLlmClientTests()
    {
        _mockFactory = new Mock<ILlmClientFactory>();
        _mockClient = new Mock<ILlmClient>();

        _mockClient.Setup(c => c.ClientName).Returns("MockClient");
        _mockFactory
            .Setup(f => f.CreateClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockClient.Object);
    }

    private static LlmRequest CreateRequest(string userPrompt = "Test prompt")
        => new("System prompt", userPrompt);

    private static Result<LlmResponse> OkResponse(string content = "Response")
        => Result<LlmResponse>.Ok(new LlmResponse(Id.New(), content, promptTokens: 10, completionTokens: 5));

    [Fact]
    public async Task GenerateAsync_FirstCall_InitializesClient()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = CreateRequest();

        _mockClient
            .Setup(c => c.GenerateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResponse());

        // Act
        var result = await lazyClient.GenerateAsync(request);

        // Assert
        result.Should().BeOfType<Result<LlmResponse>.Success>();
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockClient.Verify(c => c.GenerateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_MultipleCalls_InitializesOnlyOnce()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request1 = CreateRequest("Prompt 1");
        var request2 = CreateRequest("Prompt 2");

        _mockClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResponse());

        // Act
        await lazyClient.GenerateAsync(request1);
        await lazyClient.GenerateAsync(request2);

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
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = CreateRequest("Concurrent prompt");

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
            .ReturnsAsync(OkResponse());

        // Act - Fire 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => lazyClient.GenerateAsync(request)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeOfType<Result<LlmResponse>.Success>());
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once,
            "Even with concurrent calls, initialization should happen only once (race condition test)");
    }

    [Fact]
    public async Task IsHealthyAsync_WhenNotInitialized_InitializesFirst()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);

        _mockClient
            .Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var isHealthy = await lazyClient.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClientName_BeforeInitialization_ReturnsLazyName()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);

        // Act
        var name = lazyClient.ClientName;

        // Assert
        name.Should().Be("Lazy(Not Initialized)");
        _mockFactory.Verify(f => f.CreateClientAsync(It.IsAny<CancellationToken>()), Times.Never,
            "Getting name should not trigger initialization");
    }

    [Fact]
    public async Task ClientName_AfterInitialization_ReturnsRealName()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = CreateRequest("Init prompt");

        _mockClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResponse());

        // Act
        await lazyClient.GenerateAsync(request);
        var name = lazyClient.ClientName;

        // Assert
        name.Should().Be("MockClient");
    }

    [Fact]
    public void Dispose_WhenNotInitialized_IsSafe()
    {
        // Arrange
        var lazyClient = new LazyLlmClient(_mockFactory.Object);

        // Act & Assert - Dispose should be safe even if not initialized
        lazyClient.Dispose();
        lazyClient.Dispose(); // Second dispose should be safe
    }

    [Fact]
    public async Task Dispose_AfterInitialization_DisposesUnderlyingClient()
    {
        // Arrange
        var disposableClient = new Mock<ILlmClient>();
        disposableClient.Setup(c => c.ClientName).Returns("DisposableClient");
        disposableClient
            .Setup(c => c.GenerateAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResponse());

        var disposableMock = disposableClient.As<IDisposable>();

        var factory = new Mock<ILlmClientFactory>();
        factory
            .Setup(f => f.CreateClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(disposableClient.Object);

        var lazyClient = new LazyLlmClient(factory.Object);

        var request = CreateRequest("Init prompt");
        await lazyClient.GenerateAsync(request);

        // Act
        lazyClient.Dispose();

        // Assert
        disposableMock.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var lazyClient = new LazyLlmClient(_mockFactory.Object);
        lazyClient.Dispose();

        var request = CreateRequest();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await lazyClient.GenerateAsync(request));
    }

    [Fact]
    public async Task IsHealthyAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var lazyClient = new LazyLlmClient(_mockFactory.Object);
        lazyClient.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await lazyClient.IsHealthyAsync());
    }

    [Fact]
    public async Task GenerateAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        using var lazyClient = new LazyLlmClient(_mockFactory.Object);
        var request = CreateRequest();
        var cts = new CancellationToken(canceled: true);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await lazyClient.GenerateAsync(request, cts));
    }
}
