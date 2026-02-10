using FluentAssertions;
using Narratum.Core;
using Narratum.Orchestration.Llm;
using Xunit;

namespace Narratum.Orchestration.Tests.Llm;

/// <summary>
/// Tests unitaires pour MockLlmClient.
/// </summary>
public class MockLlmClientTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateClient()
    {
        // Act
        var client = new MockLlmClient();

        // Assert
        client.ClientName.Should().Be("MockLlmClient");
        client.IsMock.Should().BeTrue();
        client.RequestCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithConfig_ShouldUseConfig()
    {
        // Arrange
        var config = new MockLlmConfig
        {
            DefaultResponse = "Custom response"
        };

        // Act
        var client = new MockLlmClient(config);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnSuccess()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var request = new LlmRequest(
            systemPrompt: "You are a narrator.",
            userPrompt: "Describe the scene.");

        // Act
        var result = await client.GenerateAsync(request);

        // Assert
        result.Should().BeOfType<Result<LlmResponse>.Success>();
        var response = ((Result<LlmResponse>.Success)result).Value;
        response.Content.Should().NotBeEmpty();
        response.RequestId.Should().Be(request.RequestId);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncrementRequestCount()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var request = LlmRequest.Simple("Test");

        // Act
        await client.GenerateAsync(request);
        await client.GenerateAsync(request);

        // Assert
        client.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var request = LlmRequest.Simple("Test");

        // Act
        var result = await client.GenerateAsync(request);

        // Assert
        var response = ((Result<LlmResponse>.Success)result).Value;
        response.Metadata.Should().ContainKey("mock");
        response.Metadata["mock"].Should().Be(true);
    }

    [Fact]
    public async Task GenerateAsync_ShouldEstimateTokens()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var request = new LlmRequest(
            "System prompt here",
            "User prompt here");

        // Act
        var result = await client.GenerateAsync(request);

        // Assert
        var response = ((Result<LlmResponse>.Success)result).Value;
        response.PromptTokens.Should().BeGreaterThan(0);
        response.TotalTokens.Should().Be(response.PromptTokens + response.CompletionTokens);
    }

    [Fact]
    public async Task GenerateAsync_WithCustomResponse_ShouldMatchPattern()
    {
        // Arrange
        var config = new MockLlmConfig
        {
            CustomResponses = new Dictionary<string, string>
            {
                ["dialogue"] = "Custom dialogue response"
            }
        };
        var client = new MockLlmClient(config);
        var request = new LlmRequest(
            "System",
            "Generate a dialogue between characters");

        // Act
        var result = await client.GenerateAsync(request);

        // Assert
        var response = ((Result<LlmResponse>.Success)result).Value;
        response.Content.Should().Be("Custom dialogue response");
    }

    [Fact]
    public async Task GenerateAsync_WithSimulatedDelay_ShouldTakeTime()
    {
        // Arrange
        var config = new MockLlmConfig
        {
            SimulatedDelay = TimeSpan.FromMilliseconds(50)
        };
        var client = new MockLlmClient(config);
        var request = LlmRequest.Simple("Test");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await client.GenerateAsync(request);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(40); // Allow some tolerance
    }

    [Fact]
    public async Task GenerateAsync_WithFailureRate_ShouldEventuallyFail()
    {
        // Arrange
        var config = new MockLlmConfig
        {
            SimulatedDelay = TimeSpan.Zero,
            FailureRate = 1.0 // Always fail
        };
        var client = new MockLlmClient(config);
        var request = LlmRequest.Simple("Test");

        // Act
        var result = await client.GenerateAsync(request);

        // Assert
        result.Should().BeOfType<Result<LlmResponse>.Failure>();
    }

    [Fact]
    public async Task GenerateAsync_WithNullRequest_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();

        // Act
        var action = async () => await client.GenerateAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnTrue()
    {
        // Arrange
        var client = new MockLlmClient();

        // Act
        var isHealthy = await client.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void MockLlmConfig_Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = MockLlmConfig.Default;

        // Assert
        config.SimulatedDelay.Should().Be(TimeSpan.FromMilliseconds(50));
        config.FailureRate.Should().Be(0.0);
        config.DefaultResponse.Should().NotBeEmpty();
    }

    [Fact]
    public void MockLlmConfig_ForTesting_ShouldHaveNoDelay()
    {
        // Act
        var config = MockLlmConfig.ForTesting;

        // Assert
        config.SimulatedDelay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void MockLlmConfig_Stupid_ShouldHaveStupidResponse()
    {
        // Act
        var config = MockLlmConfig.Stupid;

        // Assert
        config.DefaultResponse.Should().Be("TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE");
    }
}

/// <summary>
/// Tests unitaires pour StupidLlmClient.
/// </summary>
public class StupidLlmClientTests
{
    [Fact]
    public async Task GenerateAsync_ShouldAlwaysReturnSameText()
    {
        // Arrange
        var client = new StupidLlmClient();
        var request1 = new LlmRequest("System", "First prompt");
        var request2 = new LlmRequest("Different system", "Completely different prompt");

        // Act
        var result1 = await client.GenerateAsync(request1);
        var result2 = await client.GenerateAsync(request2);

        // Assert
        var response1 = ((Result<LlmResponse>.Success)result1).Value;
        var response2 = ((Result<LlmResponse>.Success)result2).Value;

        response1.Content.Should().Be("TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE");
        response2.Content.Should().Be(response1.Content);
    }

    [Fact]
    public void StupidLlmClient_Properties_ShouldBeCorrect()
    {
        // Act
        var client = new StupidLlmClient();

        // Assert
        client.ClientName.Should().Be("StupidLlmClient");
        client.IsMock.Should().BeTrue();
    }

    [Fact]
    public async Task StupidLlmClient_ShouldIncludeStupidMetadata()
    {
        // Arrange
        var client = new StupidLlmClient();

        // Act
        var result = await client.GenerateAsync(LlmRequest.Simple("Test"));

        // Assert
        var response = ((Result<LlmResponse>.Success)result).Value;
        response.Metadata.Should().ContainKey("stupid");
        response.Metadata["stupid"].Should().Be(true);
    }

    [Fact]
    public async Task StupidLlmClient_IsHealthy_ShouldReturnTrue()
    {
        // Arrange
        var client = new StupidLlmClient();

        // Act
        var isHealthy = await client.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }
}
