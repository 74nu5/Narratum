using FluentAssertions;
using NSubstitute;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Validation;
using Narratum.State;
using Xunit;

namespace Narratum.Orchestration.Tests.Validation;

/// <summary>
/// Tests unitaires pour CoherenceValidatorAdapter.
/// </summary>
public class CoherenceValidatorAdapterTests
{
    private readonly StoryState _testState;
    private readonly NarrativeContext _testContext;

    public CoherenceValidatorAdapterTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testContext = new NarrativeContext(_testState);
    }

    [Fact]
    public async Task ValidateAsync_WithValidOutput_ShouldReturnCoherent()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Alice walked through the forest, enjoying the scenery.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, _testContext);

        // Assert
        result.IsCoherent.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithNullOutput_ShouldThrow()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();

        // Act
        var action = async () => await adapter.ValidateAsync(null!, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var output = RawOutput.Create(
            new[] { AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero) },
            TimeSpan.Zero);

        // Act
        var action = async () => await adapter.ValidateAsync(output, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_DeadCharacterActing_ShouldReturnIssue()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var deadCharacter = new CharacterContext(
            Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>());
        var context = new NarrativeContext(_testState, activeCharacters: new[] { deadCharacter });

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Bob walked into the room and smiled at everyone.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, context);

        // Assert
        result.IsCoherent.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i =>
            i.IssueType == CoherenceIssueType.DeadCharacterAction &&
            i.Description.Contains("Bob"));
    }

    [Fact]
    public async Task ValidateAsync_DeadCharacterSpeaking_ShouldReturnIssue()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var deadCharacter = new CharacterContext(
            Id.New(), "Alice", VitalStatus.Dead, new HashSet<string>());
        var context = new NarrativeContext(_testState, activeCharacters: new[] { deadCharacter });

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Character,
                    "\"I have something to say,\" Alice said firmly.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, context);

        // Assert
        result.IsCoherent.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Description.Contains("Alice"));
    }

    [Fact]
    public async Task ValidateAsync_DeadCharacterMentionedWithoutAction_ShouldBeCoherent()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var deadCharacter = new CharacterContext(
            Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>());
        var context = new NarrativeContext(_testState, activeCharacters: new[] { deadCharacter });

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Everyone remembered Bob. His legacy lived on in their hearts.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, context);

        // Assert
        result.IsCoherent.Should().BeTrue();
        result.Issues.Should().NotContain(i =>
            i.IssueType == CoherenceIssueType.DeadCharacterAction);
    }

    [Fact]
    public async Task ValidateAsync_WithCoherenceValidator_ShouldIntegrate()
    {
        // Arrange
        var mockValidator = Substitute.For<ICoherenceValidator>();
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Character is both alive and dead",
            new[] { Guid.NewGuid() },
            "Fix the contradiction");

        mockValidator
            .ValidateState(Arg.Any<CanonicalState>())
            .Returns(new[] { violation });

        var canonicalState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);

        var context = new NarrativeContext(
            _testState,
            canonicalState: canonicalState);

        var adapter = new CoherenceValidatorAdapter(mockValidator);
        var output = RawOutput.Create(
            new[] { AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero) },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, context);

        // Assert
        result.IsCoherent.Should().BeFalse();
        result.Issues.Should().Contain(i =>
            i.IssueType == CoherenceIssueType.Contradiction);
    }

    [Fact]
    public void ValidateState_WithNullState_ShouldThrow()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();

        // Act
        var action = () => adapter.ValidateState(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateState_WithNoValidator_ShouldReturnCoherent()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var canonicalState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);

        // Act
        var result = adapter.ValidateState(canonicalState);

        // Assert
        result.IsCoherent.Should().BeTrue();
    }

    [Fact]
    public void ValidateTransition_WithNoViolations_ShouldReturnCoherent()
    {
        // Arrange
        var mockValidator = Substitute.For<ICoherenceValidator>();
        mockValidator
            .ValidateTransition(Arg.Any<CanonicalState>(), Arg.Any<CanonicalState>())
            .Returns(Array.Empty<CoherenceViolation>());

        var adapter = new CoherenceValidatorAdapter(mockValidator);
        var state1 = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);
        var state2 = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);

        // Act
        var result = adapter.ValidateTransition(state1, state2);

        // Assert
        result.IsCoherent.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleDeadCharacters_ShouldDetectAll()
    {
        // Arrange
        var adapter = new CoherenceValidatorAdapter();
        var deadCharacters = new[]
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Dead, new HashSet<string>()),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>())
        };
        var context = new NarrativeContext(_testState, activeCharacters: deadCharacters);

        var output = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "Alice walked in. Bob smiled at her. They looked at each other.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await adapter.ValidateAsync(output, context);

        // Assert
        result.IsCoherent.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Description.Contains("Alice"));
        result.Issues.Should().Contain(i => i.Description.Contains("Bob"));
    }
}

/// <summary>
/// Tests pour CoherenceValidationResult.
/// </summary>
public class CoherenceValidationResultTests
{
    [Fact]
    public void Coherent_ShouldCreateCoherentResult()
    {
        // Act
        var result = CoherenceValidationResult.Coherent();

        // Assert
        result.IsCoherent.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Incoherent_ShouldCreateIncoherentResult()
    {
        // Act
        var result = CoherenceValidationResult.Incoherent(
            CoherenceIssue.Contradiction("Test contradiction"));

        // Assert
        result.IsCoherent.Should().BeFalse();
        result.Issues.Should().ContainSingle();
    }

    [Fact]
    public void FromViolations_ShouldConvertCorrectly()
    {
        // Arrange
        var violations = new[]
        {
            CoherenceViolation.Create(
                CoherenceViolationType.StatementContradiction,
                CoherenceSeverity.Error,
                "Error description",
                new[] { Guid.NewGuid() }),
            CoherenceViolation.Create(
                CoherenceViolationType.SequenceViolation,
                CoherenceSeverity.Warning,
                "Warning description",
                new[] { Guid.NewGuid() })
        };

        // Act
        var result = CoherenceValidationResult.FromViolations(violations);

        // Assert
        result.IsCoherent.Should().BeFalse(); // Has error
        result.Issues.Should().HaveCount(2);
        result.HasErrors.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void Merge_ShouldCombineResults()
    {
        // Arrange
        var result1 = CoherenceValidationResult.Incoherent(
            CoherenceIssue.Contradiction("Contradiction 1"));
        var result2 = CoherenceValidationResult.Incoherent(
            CoherenceIssue.TimelineViolation("Timeline issue"));

        // Act
        var merged = result1.Merge(result2);

        // Assert
        merged.IsCoherent.Should().BeFalse();
        merged.Issues.Should().HaveCount(2);
    }
}

/// <summary>
/// Tests pour CoherenceIssue.
/// </summary>
public class CoherenceIssueTests
{
    [Fact]
    public void FromViolation_ShouldConvertCorrectly()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Test description",
            new[] { Guid.NewGuid() },
            "Resolution suggestion");

        // Act
        var issue = CoherenceIssue.FromViolation(violation);

        // Assert
        issue.IssueType.Should().Be(CoherenceIssueType.Contradiction);
        issue.Severity.Should().Be(CoherenceIssueSeverity.Error);
        issue.Description.Should().Be("Test description");
        issue.Resolution.Should().Be("Resolution suggestion");
    }

    [Fact]
    public void DeadCharacterAction_ShouldCreateCorrectIssue()
    {
        // Act
        var issue = CoherenceIssue.DeadCharacterAction("Bob", "walked");

        // Assert
        issue.IssueType.Should().Be(CoherenceIssueType.DeadCharacterAction);
        issue.Severity.Should().Be(CoherenceIssueSeverity.Error);
        issue.Description.Should().Contain("Bob");
        issue.Description.Should().Contain("walked");
    }
}

/// <summary>
/// Tests pour CoherenceValidatorAdapterConfig.
/// </summary>
public class CoherenceValidatorAdapterConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = CoherenceValidatorAdapterConfig.Default;

        // Assert
        config.DeadCharacterActionPatterns.Should().NotBeEmpty();
        config.ValidateCharacterKnowledge.Should().BeFalse();
        config.ValidateLocationCoherence.Should().BeTrue();
    }

    [Fact]
    public void Strict_ShouldEnableAllValidation()
    {
        // Act
        var config = CoherenceValidatorAdapterConfig.Strict;

        // Assert
        config.ValidateCharacterKnowledge.Should().BeTrue();
        config.ValidateLocationCoherence.Should().BeTrue();
    }
}
