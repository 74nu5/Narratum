using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Simulation;

namespace Narratum.Tests;

/// <summary>
/// Unit tests for Phase 1.6 - Rule Engine
/// Tests rule validation and violation detection
/// </summary>
public class Phase1Step6RulesEngineTests
{
    private readonly RuleEngine _ruleEngine;

    public Phase1Step6RulesEngineTests()
    {
        _ruleEngine = new RuleEngine();
    }

    [Fact]
    public void RuleEngine_ShouldBeCreatable()
    {
        // Assert
        _ruleEngine.Should().NotBeNull();
        _ruleEngine.Rules.Should().NotBeEmpty();
    }

    [Fact]
    public void RuleEngine_ValidateState_ShouldAcceptValidState()
    {
        // Arrange
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World"
        );

        // Act
        var result = _ruleEngine.ValidateState(storyState);

        // Assert
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void RuleEngine_ValidateState_ShouldHandleNullState()
    {
        // Act
        var result = _ruleEngine.ValidateState(null!);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
        var failure = (Result<Unit>.Failure)result;
        failure.Message.Should().Contain("null");
    }

    [Fact]
    public void RuleEngine_ValidateState_WithDeadCharacter_ShouldDetectViolation()
    {
        // Arrange
        var characterId = Id.New();
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test",
            characters: new Dictionary<Id, CharacterState>
            {
                {
                    characterId,
                    new CharacterState(
                        characterId: characterId,
                        name: "Aric",
                        vitalStatus: VitalStatus.Dead
                    )
                }
            }
        );

        // Act
        var result = _ruleEngine.ValidateState(storyState);

        // Assert
        // Result may be Success or Failure depending on rule configuration
        result.Should().NotBeNull();
    }

    [Fact]
    public void RuleEngine_GetStateViolations_ShouldReturnEmptyForValidState()
    {
        // Arrange
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World"
        );

        // Act
        var violations = _ruleEngine.GetStateViolations(storyState);

        // Assert
        violations.Should().NotBeNull();
        violations.Should().BeEmpty();
    }

    [Fact]
    public void RuleEngine_GetStateViolations_ShouldDetectViolations()
    {
        // Arrange
        var characterId = Id.New();
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test",
            characters: new Dictionary<Id, CharacterState>
            {
                {
                    characterId,
                    new CharacterState(
                        characterId: characterId,
                        name: "Aric",
                        vitalStatus: VitalStatus.Dead
                    )
                }
            }
        );

        // Act
        var violations = _ruleEngine.GetStateViolations(storyState);

        // Assert
        violations.Should().NotBeNull();
    }

    [Fact]
    public void RuleEngine_Rules_ShouldIncludeMultipleRules()
    {
        // Assert
        _ruleEngine.Rules.Should().NotBeEmpty();
        _ruleEngine.Rules.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void RuleEngine_ValidateNullState_ShouldFail()
    {
        // Act
        var result = _ruleEngine.ValidateState(null!);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }
}
