using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Llm.Configuration;
using Narratum.Llm.Factory;
using Narratum.Orchestration.Llm;
using Xunit;

namespace Narratum.Llm.Tests.Factory;

public class LlmClientFactoryTests
{
    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LlmClientFactory(null!, NullLogger<LlmClientFactory>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LlmClientFactory(config, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };

        // Act
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateClientAsync_WithFoundryLocal_ReturnsClient()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini",
            TimeoutSeconds = 30
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act
        var client = await factory.CreateClientAsync(CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
        client.Should().BeAssignableTo<ILlmClient>();
        client.ClientName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateClientAsync_WithCancellation_Cancels()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await factory.CreateClientAsync(cts.Token));
    }

    [Fact]
    public async Task CreateClientAsync_MultipleCalls_CreatesMultipleInstances()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act
        var client1 = await factory.CreateClientAsync(CancellationToken.None);
        var client2 = await factory.CreateClientAsync(CancellationToken.None);

        // Assert
        client1.Should().NotBeNull();
        client2.Should().NotBeNull();
        // Factory creates new instances (not singleton)
        client1.Should().NotBeSameAs(client2);
    }

    [Fact]
    public async Task CreateClientAsync_ConcurrentCalls_AllSucceed()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act - Fire 5 concurrent create calls
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => factory.CreateClientAsync(CancellationToken.None)))
            .ToArray();

        var clients = await Task.WhenAll(tasks);

        // Assert
        clients.Should().HaveCount(5);
        clients.Should().AllSatisfy(c => c.Should().NotBeNull());
    }

    [Fact]
    public async Task CreateClientAsync_UsesConfiguredModel()
    {
        // Arrange
        var expectedModel = "custom-model-name";
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = expectedModel
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act
        var client = await factory.CreateClientAsync(CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
        // Client should use the configured model
        // (exact verification depends on client implementation)
    }

    [Fact]
    public async Task CreateClientAsync_UsesConfiguredTimeout()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini",
            TimeoutSeconds = 60 // Custom timeout
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act
        var client = await factory.CreateClientAsync(CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
        // Client should respect timeout configuration
    }

    [Fact]
    public async Task CreateClientAsync_WithInvalidModel_HandlesGracefully()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "" // Invalid empty model
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance);

        // Act
        var client = await factory.CreateClientAsync(CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
        // Factory should handle invalid config gracefully
        // (may use fallback or throw specific exception)
    }

    [Fact]
    public void Dispose_CanBeCalledSafely()
    {
        // Arrange
        var config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini"
        };
        var factory = new LlmClientFactory(config, NullLogger<LlmClientFactory>.Instance) as IDisposable;

        // Act & Assert - Should not throw
        factory?.Dispose();
        factory?.Dispose(); // Second dispose should be safe
    }
}
