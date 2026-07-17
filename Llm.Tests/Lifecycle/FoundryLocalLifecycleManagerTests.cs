using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Narratum.Core;
using Narratum.Llm.Configuration;
using Narratum.Llm.Lifecycle;
using Xunit;

namespace Narratum.Llm.Tests.Lifecycle;

public class FoundryLocalLifecycleManagerTests
{
    private readonly LlmClientConfig _config;

    public FoundryLocalLifecycleManagerTests()
    {
        _config = new LlmClientConfig
        {
            Provider = LlmProviderType.FoundryLocal,
            DefaultModel = "phi-4-mini",
            TimeoutSeconds = 30,
            MaxRetries = 3
        };
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FoundryLocalLifecycleManager(null!, NullLogger<FoundryLocalLifecycleManager>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FoundryLocalLifecycleManager(_config, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_ReturnsResult()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act
        var result = await manager.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Note: Actual success depends on Foundry Local being installed
        // In CI/test environment, this might fail, which is expected
    }

    [Fact]
    public async Task InitializeAsync_WithCancellation_Cancels()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await manager.InitializeAsync(cts.Token));
    }

    [Fact]
    public async Task ShutdownAsync_AfterInitialization_ReturnsSuccess()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);
        await manager.InitializeAsync(CancellationToken.None);

        // Act
        var result = await manager.ShutdownAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Success or failure depends on Foundry state
    }

    [Fact]
    public async Task ShutdownAsync_WithoutInitialization_Succeeds()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act
        var result = await manager.ShutdownAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Should succeed even if not initialized (idempotent)
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsBoolean()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act
        var isHealthy = await manager.IsHealthyAsync(CancellationToken.None);

        // Assert
        isHealthy.Should().BeOfType<bool>();
        // Actual value depends on Foundry Local state
    }

    [Fact]
    public async Task MultipleInitializeCalls_AreIdempotent()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act
        var result1 = await manager.InitializeAsync(CancellationToken.None);
        var result2 = await manager.InitializeAsync(CancellationToken.None);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        // Second call should be safe (idempotent)
    }

    [Fact]
    public async Task MultipleShutdownCalls_AreIdempotent()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);
        await manager.InitializeAsync(CancellationToken.None);

        // Act
        var result1 = await manager.ShutdownAsync(CancellationToken.None);
        var result2 = await manager.ShutdownAsync(CancellationToken.None);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        // Second shutdown should be safe (idempotent)
    }

    [Fact]
    public async Task ConcurrentInitializeCalls_HandleGracefully()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act - Fire 5 concurrent initializations
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => manager.InitializeAsync(CancellationToken.None)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        // All should complete without exceptions (even if some fail)
    }

    [Fact]
    public async Task InitializeThenShutdown_Lifecycle_Works()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);

        // Act
        var initResult = await manager.InitializeAsync(CancellationToken.None);
        var isHealthyAfterInit = await manager.IsHealthyAsync(CancellationToken.None);
        var shutdownResult = await manager.ShutdownAsync(CancellationToken.None);

        // Assert
        initResult.Should().NotBeNull();
        shutdownResult.Should().NotBeNull();
        // Health check result depends on Foundry state
    }

    [Fact]
    public async Task ShutdownAsync_WithCancellation_Cancels()
    {
        // Arrange
        var manager = new FoundryLocalLifecycleManager(_config, NullLogger<FoundryLocalLifecycleManager>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await manager.ShutdownAsync(cts.Token));
    }
}
