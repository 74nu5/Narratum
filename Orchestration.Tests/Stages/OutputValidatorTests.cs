using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour OutputValidator.
/// </summary>
public class OutputValidatorTests
{
    private readonly StoryState _testState;
    private readonly NarrativeContext _testContext;

    public OutputValidatorTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testContext = new NarrativeContext(_testState);
    }

    [Fact]
    public async Task ValidateAsync_WithValidOutput_ShouldReturnValid()
    {
        // Arrange
        var validator = new OutputValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This is a valid narrative text that meets the minimum length requirements.",
                    TimeSpan.FromMilliseconds(100))
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithNullOutput_ShouldThrow()
    {
        // Arrange
        var validator = new OutputValidator();

        // Act
        var action = async () => await validator.ValidateAsync(null!, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var validator = new OutputValidator();
        var output = RawOutput.Create(
            new[] { AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero) },
            TimeSpan.Zero);

        // Act
        var action = async () => await validator.ValidateAsync(output, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_WithNoResponses_ShouldReturnCriticalError()
    {
        // Arrange
        var validator = new OutputValidator();
        var output = RawOutput.Create(Array.Empty<AgentResponse>(), TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasCriticalErrors.Should().BeTrue();
        result.ErrorMessages.Should().Contain(m => m.Contains("No agent responses"));
    }

    [Fact]
    public async Task ValidateAsync_WithFailedResponse_ShouldReturnError()
    {
        // Arrange
        var validator = new OutputValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateFailure(AgentType.Narrator, "LLM timeout", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        // Major errors don't make IsValid false (only Critical errors do)
        // But there should be an error message about the failure
        result.Errors.Should().NotBeEmpty();
        result.ErrorMessages.Should().Contain(m => m.Contains("Narrator") && m.Contains("failed"));
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyContent_ShouldReturnCriticalError()
    {
        // Arrange
        var validator = new OutputValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasCriticalErrors.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithTooShortContent_ShouldReturnError()
    {
        // Arrange
        var config = new OutputValidatorConfig { MinContentLength = 50 };
        var validator = new OutputValidator(config: config);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Short.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        // Too short is a Major error, not Critical, so IsValid remains true
        // But there should be an error about the length
        result.Errors.Should().NotBeEmpty();
        result.ErrorMessages.Should().Contain(m => m.Contains("too short"));
    }

    [Fact]
    public async Task ValidateAsync_WithTooLongContent_ShouldReturnWarning()
    {
        // Arrange
        var config = new OutputValidatorConfig { MaxContentLength = 100 };
        var validator = new OutputValidator(config: config);
        var longContent = new string('x', 150);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, longContent, TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.IsValid.Should().BeTrue(); // Warnings don't make it invalid
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Message.Contains("exceeds recommended length"));
    }

    [Fact]
    public async Task ValidateAsync_WithForbiddenPattern_ShouldReturnWarning()
    {
        // Arrange
        var config = new OutputValidatorConfig
        {
            ForbiddenPatterns = new[] { "[TODO]", "PLACEHOLDER" }
        };
        var validator = new OutputValidator(config: config);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This is valid content but has a [TODO] marker.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Message.Contains("[TODO]"));
    }

    [Fact]
    public async Task ValidateAsync_DeadCharacterActing_ShouldReturnCriticalError()
    {
        // Arrange
        var validator = new OutputValidator();
        var deadCharacter = new CharacterContext(
            Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>());
        var context = new NarrativeContext(_testState, activeCharacters: new[] { deadCharacter });

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Bob walked into the room and said hello.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasCriticalErrors.Should().BeTrue();
        result.ErrorMessages.Should().Contain(m => m.Contains("Bob") && m.Contains("Dead character"));
    }

    [Fact]
    public async Task ValidateAsync_DeadCharacterMentioned_ShouldAllowNonActionMentions()
    {
        // Arrange
        var validator = new OutputValidator();
        var deadCharacter = new CharacterContext(
            Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>());
        var context = new NarrativeContext(_testState, activeCharacters: new[] { deadCharacter });

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Alice remembered Bob fondly. His memory lived on.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, context);

        // Assert
        result.IsValid.Should().BeTrue(); // Mentioning dead characters is fine, actions are not
    }

    [Fact]
    public async Task ValidateAsync_LocationNotMentioned_ShouldReturnWarning()
    {
        // Arrange
        var validator = new OutputValidator();
        var location = LocationContext.Create(Id.New(), "Dark Forest", "A mysterious forest.");
        var character = new CharacterContext(
            Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>());
        var context = new NarrativeContext(
            _testState,
            activeCharacters: new[] { character },
            currentLocation: location);

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Alice sat down and contemplated life. The weather was nice.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, context);

        // Assert
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Message.Contains("Dark Forest"));
    }

    [Fact]
    public async Task ValidateAsync_LocationMentioned_ShouldNotWarn()
    {
        // Arrange
        var validator = new OutputValidator();
        var location = LocationContext.Create(Id.New(), "Dark Forest", "A mysterious forest.");
        var character = new CharacterContext(
            Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>());
        var context = new NarrativeContext(
            _testState,
            activeCharacters: new[] { character },
            currentLocation: location);

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Alice wandered deeper into the Dark Forest, leaves crunching underfoot.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, context);

        // Assert
        result.Warnings.Should().NotContain(w => w.Message.Contains("Dark Forest"));
    }

    [Fact]
    public async Task ValidateAsync_WithDefaultConfig_ShouldUseDefaults()
    {
        // Arrange
        var validator = new OutputValidator();
        var shortContent = "Short"; // Less than default 10 chars but not empty
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, shortContent, TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        // Default MinContentLength is 10, "Short" is 5 chars
        // Too short is a Major error, not Critical, so IsValid remains true
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Message.Contains("too short"));
    }

    [Fact]
    public async Task ValidateAsync_WithStrictConfig_ShouldBeMoreRestrictive()
    {
        // Arrange
        var validator = new OutputValidator(config: OutputValidatorConfig.Strict);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This has [ERROR] in it.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await validator.ValidateAsync(output, _testContext);

        // Assert
        result.HasWarnings.Should().BeTrue();
    }
}

/// <summary>
/// Tests pour OutputValidatorConfig.
/// </summary>
public class OutputValidatorConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = OutputValidatorConfig.Default;

        // Assert
        config.MinContentLength.Should().Be(10);
        config.MaxContentLength.Should().Be(10000);
        config.ForbiddenPatterns.Should().BeEmpty();
    }

    [Fact]
    public void Strict_ShouldBeMoreRestrictive()
    {
        // Act
        var config = OutputValidatorConfig.Strict;

        // Assert
        config.MinContentLength.Should().Be(50);
        config.MaxContentLength.Should().Be(5000);
        config.ForbiddenPatterns.Should().Contain("[ERROR]");
        config.ForbiddenPatterns.Should().Contain("[TODO]");
        config.ForbiddenPatterns.Should().Contain("PLACEHOLDER");
    }
}
