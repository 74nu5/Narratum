using FluentAssertions;
using NSubstitute;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour AgentExecutor.
/// </summary>
public class AgentExecutorTests
{
    private readonly StoryState _testState;
    private readonly NarrativeContext _testContext;

    public AgentExecutorTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testContext = new NarrativeContext(_testState);
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new AgentExecutor(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithMockClient_ShouldSucceed()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Single(
            AgentPrompt.Create(AgentType.Narrator, "You are a narrator.", "Tell a story."));

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
        var output = ((Result<RawOutput>.Success)result).Value;
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPrompts_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var executor = new AgentExecutor(client);

        // Act
        var action = async () => await executor.ExecuteAsync(null!, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Single(AgentPrompt.Create(AgentType.Narrator, "S", "U"));

        // Act
        var action = async () => await executor.ExecuteAsync(prompts, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_Sequential_ShouldExecuteInOrder()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Sequential(
            AgentPrompt.Create(AgentType.Summary, "Summary system", "Summarize."),
            AgentPrompt.Create(AgentType.Narrator, "Narrator system", "Narrate."));

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
        var output = ((Result<RawOutput>.Success)result).Value;
        output.Responses.Should().HaveCount(2);
        output.HasSuccessfulResponse(AgentType.Summary).Should().BeTrue();
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Parallel_ShouldExecuteAllAgents()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Parallel(
            AgentPrompt.Create(AgentType.Narrator, "Narrator", "Narrate."),
            AgentPrompt.Create(AgentType.Character, "Character", "Speak."));

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
        var output = ((Result<RawOutput>.Success)result).Value;
        output.Responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordDuration()
    {
        // Arrange
        var config = new MockLlmConfig { SimulatedDelay = TimeSpan.FromMilliseconds(50) };
        var client = new MockLlmClient(config);
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Single(AgentPrompt.Create(AgentType.Narrator, "S", "U"));

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        var output = ((Result<RawOutput>.Success)result).Value;
        output.TotalDuration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingClient_ShouldReturnFailedResponse()
    {
        // Arrange
        var config = new MockLlmConfig { FailureRate = 1.0 }; // Always fail
        var client = new MockLlmClient(config);
        var executor = new AgentExecutor(client);
        var prompts = PromptSet.Single(AgentPrompt.Create(AgentType.Narrator, "S", "U"));

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
        var output = ((Result<RawOutput>.Success)result).Value;
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_Sequential_RequiredFails_ShouldStopExecution()
    {
        // Arrange
        var config = new MockLlmConfig { FailureRate = 1.0 };
        var client = new MockLlmClient(config);
        var executor = new AgentExecutor(client);
        var prompts = new PromptSet(
            new[]
            {
                new AgentPrompt(AgentType.Summary, "S1", "U1", new Dictionary<string, string>(), PromptPriority.Required),
                new AgentPrompt(AgentType.Narrator, "S2", "U2", new Dictionary<string, string>(), PromptPriority.Required)
            },
            ExecutionOrder.Sequential);

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        var output = ((Result<RawOutput>.Success)result).Value;
        // Only the first agent should have been attempted
        output.Responses.Should().HaveCount(1);
    }

    [Fact]
    public async Task RewriteAsync_WithValidInput_ShouldRewriteContent()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var executor = new AgentExecutor(client);

        var previousOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Original content", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        var validationResult = ValidationResult.Invalid("Content too short");

        // Act
        var result = await executor.RewriteAsync(previousOutput, validationResult, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
        var output = ((Result<RawOutput>.Success)result).Value;
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task RewriteAsync_WithNullPreviousOutput_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var executor = new AgentExecutor(client);
        var validationResult = ValidationResult.Invalid("Error");

        // Act
        var action = async () => await executor.RewriteAsync(null!, validationResult, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RewriteAsync_WithNullValidationResult_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var executor = new AgentExecutor(client);
        var previousOutput = RawOutput.Create(
            new[] { AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero) },
            TimeSpan.Zero);

        // Act
        var action = async () => await executor.RewriteAsync(previousOutput, null!, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RewriteAsync_ShouldSkipAlreadyFailedResponses()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var executor = new AgentExecutor(client);

        var previousOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero),
                AgentResponse.CreateFailure(AgentType.Character, "Previous error", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        var validationResult = ValidationResult.Invalid("Some error");

        // Act
        var result = await executor.RewriteAsync(previousOutput, validationResult, _testContext);

        // Assert
        var output = ((Result<RawOutput>.Success)result).Value;
        // Character should still be failed (skipped)
        output.HasSuccessfulResponse(AgentType.Character).Should().BeFalse();
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Conditional_ShouldSkipOptionalWhenAllFail()
    {
        // Arrange
        var config = new MockLlmConfig { FailureRate = 1.0 };
        var client = new MockLlmClient(config);
        var executor = new AgentExecutor(client);

        var prompts = new PromptSet(
            new[]
            {
                new AgentPrompt(AgentType.Summary, "S1", "U1", new Dictionary<string, string>(), PromptPriority.Required),
                new AgentPrompt(AgentType.Narrator, "S2", "U2", new Dictionary<string, string>(), PromptPriority.Optional),
                new AgentPrompt(AgentType.Consistency, "S3", "U3", new Dictionary<string, string>(), PromptPriority.Fallback)
            },
            ExecutionOrder.Conditional);

        // Act
        var result = await executor.ExecuteAsync(prompts, _testContext);

        // Assert
        result.Should().BeOfType<Result<RawOutput>.Success>();
    }
}
