using FluentAssertions;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Validation;
using Xunit;

namespace Narratum.Orchestration.Tests.Validation;

/// <summary>
/// Tests unitaires pour StructureValidator.
/// </summary>
public class StructureValidatorTests
{
    [Fact]
    public void Validate_WithValidOutput_ShouldReturnValid()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This is a valid narrative text with sufficient length.",
                    TimeSpan.FromMilliseconds(100))
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithNullOutput_ShouldThrow()
    {
        // Arrange
        var validator = new StructureValidator();

        // Act
        var action = () => validator.Validate(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_WithNoResponses_ShouldReturnError()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(Array.Empty<AgentResponse>(), TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorType.Should().Be(StructureErrorType.NoResponses);
    }

    [Fact]
    public void Validate_WithEmptyContent_ShouldReturnError()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.EmptyContent);
    }

    [Fact]
    public void Validate_WithWhitespaceContent_ShouldReturnError()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "   \t\n  ", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.EmptyContent);
    }

    [Fact]
    public void Validate_WithFailedResponse_ShouldReturnError()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateFailure(AgentType.Narrator, "LLM timeout", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.AgentFailed);
    }

    [Fact]
    public void Validate_WithTooShortContent_ShouldReturnError()
    {
        // Arrange
        var config = new StructureValidatorConfig { DefaultMinLength = 50 };
        var validator = new StructureValidator(config);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Short.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.ContentTooShort);
    }

    [Fact]
    public void Validate_WithTooLongContent_AsWarning_ShouldReturnValidWithWarning()
    {
        // Arrange
        var config = new StructureValidatorConfig
        {
            DefaultMaxLength = 100,
            TreatMaxLengthAsError = false
        };
        var validator = new StructureValidator(config);
        var longContent = new string('x', 150);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, longContent, TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WithTooLongContent_AsError_ShouldReturnInvalid()
    {
        // Arrange
        var config = new StructureValidatorConfig
        {
            DefaultMaxLength = 100,
            TreatMaxLengthAsError = true
        };
        var validator = new StructureValidator(config);
        var longContent = new string('x', 150);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, longContent, TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.ContentTooLong);
    }

    [Fact]
    public void Validate_WithForbiddenPattern_AsWarning_ShouldReturnValidWithWarning()
    {
        // Arrange
        var config = new StructureValidatorConfig
        {
            ForbiddenPatterns = new[] { "[TODO]", "PLACEHOLDER" },
            TreatForbiddenPatternsAsError = false
        };
        var validator = new StructureValidator(config);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This is valid content with [TODO] marker.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WithForbiddenPattern_AsError_ShouldReturnInvalid()
    {
        // Arrange
        var config = new StructureValidatorConfig
        {
            ForbiddenPatterns = new[] { "[TODO]", "PLACEHOLDER" },
            TreatForbiddenPatternsAsError = true
        };
        var validator = new StructureValidator(config);
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "This is content with PLACEHOLDER text.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorType == StructureErrorType.InvalidFormat);
    }

    [Fact]
    public void Validate_WithAgentSpecificMinLength_ShouldUseAgentConfig()
    {
        // Arrange
        var config = new StructureValidatorConfig
        {
            DefaultMinLength = 10,
            MinLengthPerAgent = new Dictionary<AgentType, int>
            {
                [AgentType.Summary] = 50,
                [AgentType.Narrator] = 100
            }
        };
        var validator = new StructureValidator(config);
        var output = RawOutput.Create(
            new[]
            {
                // Narrator with 50 chars - should fail (needs 100)
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    new string('x', 50),
                    TimeSpan.Zero),
                // Summary with 30 chars - should fail (needs 50)
                AgentResponse.CreateSuccess(
                    AgentType.Summary,
                    new string('y', 30),
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Agent == AgentType.Narrator);
        result.Errors.Should().Contain(e => e.Agent == AgentType.Summary);
    }

    [Fact]
    public void Validate_WithMultipleResponses_ShouldValidateAll()
    {
        // Arrange
        var validator = new StructureValidator();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Valid narrative content here.",
                    TimeSpan.Zero),
                AgentResponse.CreateSuccess(
                    AgentType.Summary,
                    "Valid summary content here.",
                    TimeSpan.Zero),
                AgentResponse.CreateSuccess(
                    AgentType.Character,
                    "", // Empty - should fail
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = validator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Agent == AgentType.Character && e.ErrorType == StructureErrorType.EmptyContent);
    }

    [Fact]
    public void ValidateResponse_WithValidResponse_ShouldReturnValid()
    {
        // Arrange
        var validator = new StructureValidator();
        var response = AgentResponse.CreateSuccess(
            AgentType.Narrator,
            "Valid content here.",
            TimeSpan.Zero);

        // Act
        var result = validator.ValidateResponse(response);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_WithInvalidResponse_ShouldReturnInvalid()
    {
        // Arrange
        var validator = new StructureValidator();
        var response = AgentResponse.CreateFailure(
            AgentType.Narrator,
            "Error message",
            TimeSpan.Zero);

        // Act
        var result = validator.ValidateResponse(response);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}

/// <summary>
/// Tests pour StructureValidatorConfig.
/// </summary>
public class StructureValidatorConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = StructureValidatorConfig.Default;

        // Assert
        config.DefaultMinLength.Should().Be(10);
        config.DefaultMaxLength.Should().Be(10000);
        config.ForbiddenPatterns.Should().BeEmpty();
        config.TreatMaxLengthAsError.Should().BeFalse();
        config.TreatForbiddenPatternsAsError.Should().BeFalse();
    }

    [Fact]
    public void Strict_ShouldBeMoreRestrictive()
    {
        // Act
        var config = StructureValidatorConfig.Strict;

        // Assert
        config.DefaultMinLength.Should().Be(50);
        config.DefaultMaxLength.Should().Be(5000);
        config.ForbiddenPatterns.Should().Contain("[ERROR]");
        config.ForbiddenPatterns.Should().Contain("PLACEHOLDER");
        config.TreatMaxLengthAsError.Should().BeTrue();
        config.TreatForbiddenPatternsAsError.Should().BeTrue();
    }

    [Fact]
    public void Narrative_ShouldHaveAgentSpecificLengths()
    {
        // Act
        var config = StructureValidatorConfig.Narrative;

        // Assert
        config.MinLengthPerAgent.Should().ContainKey(AgentType.Narrator);
        config.MinLengthPerAgent.Should().ContainKey(AgentType.Summary);
        config.MinLengthPerAgent[AgentType.Narrator].Should().Be(150);
        config.MinLengthPerAgent[AgentType.Summary].Should().Be(50);
    }
}

/// <summary>
/// Tests pour StructureValidationResult.
/// </summary>
public class StructureValidationResultTests
{
    [Fact]
    public void Valid_ShouldCreateValidResult()
    {
        // Act
        var result = StructureValidationResult.Valid();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_ShouldCreateInvalidResult()
    {
        // Act
        var result = StructureValidationResult.Invalid(
            StructureValidationError.Empty(AgentType.Narrator));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Merge_ShouldCombineResults()
    {
        // Arrange
        var result1 = StructureValidationResult.Invalid(
            StructureValidationError.Empty(AgentType.Narrator));
        var result2 = StructureValidationResult.WithWarnings(
            new StructureValidationWarning(AgentType.Summary, "Warning message"));

        // Act
        var merged = result1.Merge(result2);

        // Assert
        merged.IsValid.Should().BeFalse();
        merged.Errors.Should().HaveCount(1);
        merged.Warnings.Should().HaveCount(1);
    }
}

/// <summary>
/// Tests pour StructureValidationError.
/// </summary>
public class StructureValidationErrorTests
{
    [Fact]
    public void Empty_ShouldCreateCorrectError()
    {
        // Act
        var error = StructureValidationError.Empty(AgentType.Narrator);

        // Assert
        error.Agent.Should().Be(AgentType.Narrator);
        error.ErrorType.Should().Be(StructureErrorType.EmptyContent);
        error.Message.Should().Contain("Narrator");
        error.Message.Should().Contain("empty");
    }

    [Fact]
    public void TooShort_ShouldIncludeLengthInfo()
    {
        // Act
        var error = StructureValidationError.TooShort(AgentType.Summary, 30, 50);

        // Assert
        error.Agent.Should().Be(AgentType.Summary);
        error.ErrorType.Should().Be(StructureErrorType.ContentTooShort);
        error.Message.Should().Contain("30");
        error.Message.Should().Contain("50");
    }

    [Fact]
    public void NoResponses_ShouldHaveNullAgent()
    {
        // Act
        var error = StructureValidationError.NoResponses();

        // Assert
        error.Agent.Should().BeNull();
        error.ErrorType.Should().Be(StructureErrorType.NoResponses);
    }
}
