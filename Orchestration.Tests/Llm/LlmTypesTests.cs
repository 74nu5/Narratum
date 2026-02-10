using FluentAssertions;
using Narratum.Core;
using Narratum.Orchestration.Llm;
using Xunit;

namespace Narratum.Orchestration.Tests.Llm;

/// <summary>
/// Tests unitaires pour LlmRequest, LlmResponse, et LlmParameters.
/// </summary>
public class LlmTypesTests
{
    [Fact]
    public void LlmParameters_Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var parameters = LlmParameters.Default;

        // Assert
        parameters.Temperature.Should().Be(0.7);
        parameters.MaxTokens.Should().Be(1024);
        parameters.TopP.Should().Be(0.9);
        parameters.StopTokens.Should().BeEmpty();
    }

    [Fact]
    public void LlmParameters_Deterministic_ShouldHaveZeroTemperature()
    {
        // Act
        var parameters = LlmParameters.Deterministic;

        // Assert
        parameters.Temperature.Should().Be(0.0);
    }

    [Fact]
    public void LlmParameters_Creative_ShouldHaveHighTemperature()
    {
        // Act
        var parameters = LlmParameters.Creative;

        // Assert
        parameters.Temperature.Should().Be(0.9);
        parameters.TopP.Should().Be(0.95);
    }

    [Fact]
    public void LlmParameters_Init_ShouldAllowCustomValues()
    {
        // Act
        var parameters = new LlmParameters
        {
            Temperature = 0.5,
            MaxTokens = 2048,
            TopP = 0.8,
            StopTokens = new[] { "\n", "END" }
        };

        // Assert
        parameters.Temperature.Should().Be(0.5);
        parameters.MaxTokens.Should().Be(2048);
        parameters.TopP.Should().Be(0.8);
        parameters.StopTokens.Should().HaveCount(2);
    }

    [Fact]
    public void LlmRequest_Constructor_ShouldCreateValidRequest()
    {
        // Act
        var request = new LlmRequest(
            systemPrompt: "You are a narrator.",
            userPrompt: "Describe the scene.");

        // Assert
        request.RequestId.Should().NotBe(default(Id));
        request.SystemPrompt.Should().Be("You are a narrator.");
        request.UserPrompt.Should().Be("Describe the scene.");
        request.Parameters.Should().Be(LlmParameters.Default);
        request.Metadata.Should().BeEmpty();
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LlmRequest_Constructor_WithNullSystemPrompt_ShouldThrow()
    {
        // Act
        var action = () => new LlmRequest(null!, "User prompt");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LlmRequest_Constructor_WithNullUserPrompt_ShouldThrow()
    {
        // Act
        var action = () => new LlmRequest("System prompt", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LlmRequest_Constructor_WithParameters_ShouldUseParameters()
    {
        // Arrange
        var parameters = LlmParameters.Deterministic;

        // Act
        var request = new LlmRequest("System", "User", parameters);

        // Assert
        request.Parameters.Should().Be(parameters);
    }

    [Fact]
    public void LlmRequest_Constructor_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var request = new LlmRequest("System", "User", metadata: metadata);

        // Assert
        request.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void LlmRequest_Simple_ShouldCreateSimpleRequest()
    {
        // Act
        var request = LlmRequest.Simple("Tell me a story");

        // Assert
        request.SystemPrompt.Should().Contain("helpful");
        request.UserPrompt.Should().Be("Tell me a story");
    }

    [Fact]
    public void LlmResponse_Constructor_ShouldCreateValidResponse()
    {
        // Arrange
        var requestId = Id.New();

        // Act
        var response = new LlmResponse(
            requestId: requestId,
            content: "Generated content here.");

        // Assert
        response.ResponseId.Should().NotBe(default(Id));
        response.RequestId.Should().Be(requestId);
        response.Content.Should().Be("Generated content here.");
        response.PromptTokens.Should().Be(0);
        response.CompletionTokens.Should().Be(0);
        response.GenerationDuration.Should().Be(TimeSpan.Zero);
        response.Metadata.Should().BeEmpty();
        response.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LlmResponse_Constructor_WithNullContent_ShouldThrow()
    {
        // Act
        var action = () => new LlmResponse(Id.New(), null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LlmResponse_Constructor_WithAllParameters_ShouldSetAll()
    {
        // Arrange
        var requestId = Id.New();
        var duration = TimeSpan.FromMilliseconds(150);
        var metadata = new Dictionary<string, object> { ["model"] = "test-model" };

        // Act
        var response = new LlmResponse(
            requestId: requestId,
            content: "Content",
            promptTokens: 100,
            completionTokens: 50,
            generationDuration: duration,
            metadata: metadata);

        // Assert
        response.PromptTokens.Should().Be(100);
        response.CompletionTokens.Should().Be(50);
        response.GenerationDuration.Should().Be(duration);
        response.Metadata.Should().ContainKey("model");
    }

    [Fact]
    public void LlmResponse_TotalTokens_ShouldSumTokens()
    {
        // Arrange
        var response = new LlmResponse(
            requestId: Id.New(),
            content: "Content",
            promptTokens: 100,
            completionTokens: 50);

        // Act & Assert
        response.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void LlmRequest_And_LlmResponse_ShouldLinkViaRequestId()
    {
        // Arrange
        var request = new LlmRequest("System", "User");

        // Act
        var response = new LlmResponse(
            requestId: request.RequestId,
            content: "Response");

        // Assert
        response.RequestId.Should().Be(request.RequestId);
    }
}
