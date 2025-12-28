using Narratum.Core;
using Narratum.Domain;
using Narratum.Simulation;
using Narratum.State;
using Xunit;
using FluentAssertions;

namespace Narratum.Tests;

/// <summary>
/// Integration tests for Step 1.4: Rules Engine
/// Tests rule validation and enforcement.
/// </summary>
public class Phase1Step4RulesEngineTests
{
    [Fact]
    public void RuleEngine_ShouldInitializeWithDefaultRules()
    {
        // Arrange & Act
        var ruleEngine = new RuleEngine();

        // Assert
        ruleEngine.Rules.Should().HaveCount(9);
    }

    [Fact]
    public void CharacterMustBeAliveRule_ShouldPreventDeadCharacterMove()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Dead));

        var ruleEngine = new RuleEngine();
        var action = new MoveCharacterAction(characterId, Id.New());

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void CharacterMustExistRule_ShouldRejectNonExistentCharacter()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var ruleEngine = new RuleEngine();
        var action = new MoveCharacterAction(Id.New(), Id.New()); // Non-existent character

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void TimeMonotonicityRule_ShouldRejectNegativeTimeAdvance()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var ruleEngine = new RuleEngine();
        var action = new AdvanceTimeAction(TimeSpan.FromHours(-1));

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void NoSelfRelationshipRule_ShouldRejectSelfRelationship()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric"));

        var ruleEngine = new RuleEngine();
        var action = new UpdateRelationshipAction(characterId, characterId, new Relationship("friend", 0, 0));

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void CannotDieTwiceRule_ShouldRejectDeathOfDeadCharacter()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Dead));

        var ruleEngine = new RuleEngine();
        var action = new RecordCharacterDeathAction(characterId, Id.New(), "Already dead");

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void CannotStayInSameLocationRule_ShouldRejectSameLocation()
    {
        // Arrange
        var characterId = Id.New();
        var locationId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive, locationId));

        var ruleEngine = new RuleEngine();
        var action = new MoveCharacterAction(characterId, locationId);

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void RuleEngine_ShouldAllowValidAction()
    {
        // Arrange
        var characterId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive, location1));

        var ruleEngine = new RuleEngine();
        var action = new MoveCharacterAction(characterId, location2);

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void RuleEngine_ShouldCollectMultipleViolations()
    {
        // Arrange
        var char1Id = Id.New();
        var char2Id = Id.New();
        var state = StoryState.Create(Id.New(), "World");

        var ruleEngine = new RuleEngine();
        var action = new TriggerEncounterAction(char1Id, char2Id, Id.New());

        // Act
        var violations = ruleEngine.GetActionViolations(state, action);

        // Assert
        violations.Should().HaveCountGreaterThanOrEqualTo(1);
        violations.Any(v => v.RuleId == "CHAR_EXISTS").Should().BeTrue();
    }

    [Fact]
    public void RuleEngine_ShouldValidateStateConsistency()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var ruleEngine = new RuleEngine();

        // Act
        var result = ruleEngine.ValidateState(state);

        // Assert
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void RuleEngine_ShouldHandleNullAction()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var ruleEngine = new RuleEngine();

        // Act
        var result = ruleEngine.ValidateAction(state, null);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void RuleEngine_ShouldPreventDeadCharacterEncounter()
    {
        // Arrange
        var char1Id = Id.New();
        var char2Id = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(char1Id, "Aric", VitalStatus.Dead))
            .WithCharacter(new CharacterState(char2Id, "Malachar", VitalStatus.Alive));

        var ruleEngine = new RuleEngine();
        var action = new TriggerEncounterAction(char1Id, char2Id, Id.New());

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void RuleEngine_IntegratedWithStateTransitionService()
    {
        // Arrange
        var characterId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive, location1));

        var ruleEngine = new RuleEngine();
        var transitionService = new StateTransitionService(null, ruleEngine);

        // Act - valid action should work
        var action = new MoveCharacterAction(characterId, location2);
        var result = transitionService.TransitionState(state, action);

        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
    }

    [Fact]
    public void RuleEngine_ShouldBlockInvalidActionInTransitionService()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Dead));

        var ruleEngine = new RuleEngine();
        var transitionService = new StateTransitionService(null, ruleEngine);

        // Act - dead character cannot move
        var action = new MoveCharacterAction(characterId, Id.New());
        var result = transitionService.TransitionState(state, action);

        // Assert
        result.Should().BeOfType<Result<StoryState>.Failure>();
    }

    [Fact]
    public void RulesEngine_Deterministic()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Dead));

        var ruleEngine = new RuleEngine();
        var action = new MoveCharacterAction(characterId, Id.New());

        // Act - run validation multiple times
        var results = Enumerable.Range(0, 5)
            .Select(_ => ruleEngine.ValidateAction(state, action))
            .ToList();

        // Assert - all results should be the same
        results.Should().AllSatisfy(r => r.Should().Be(results[0]));
    }

    [Fact]
    public void RuleViolation_ShouldContainErrorDetails()
    {
        // Arrange
        var violation = RuleViolation.Error("TEST_RULE", "Test Rule", "Test message");

        // Assert
        violation.RuleId.Should().Be("TEST_RULE");
        violation.RuleName.Should().Be("Test Rule");
        violation.Message.Should().Be("Test message");
        violation.Severity.Should().Be(RuleSeverity.Error);
    }

    [Fact]
    public void RuleViolation_ShouldSupportWarningAndInfo()
    {
        // Arrange
        var warning = RuleViolation.Warning("WARN_RULE", "Warning Rule", "Warning message");
        var info = RuleViolation.Info("INFO_RULE", "Info Rule", "Info message");

        // Assert
        warning.Severity.Should().Be(RuleSeverity.Warning);
        info.Severity.Should().Be(RuleSeverity.Info);
    }

    [Fact]
    public void RuleEngine_ShouldRejectDeadCharacterRevelation()
    {
        // Arrange
        var characterId = Id.New();
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Dead));

        var ruleEngine = new RuleEngine();
        var action = new RecordRevelationAction(characterId, "Secret knowledge");

        // Act
        var result = ruleEngine.ValidateAction(state, action);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void CompleteScenarioWithRules_AllConditionsEnforced()
    {
        // Arrange
        var heroId = Id.New();
        var villainId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();

        var state = StoryState.Create(Id.New(), "Aethermoor")
            .WithCharacter(new CharacterState(heroId, "Aric", VitalStatus.Alive, location1))
            .WithCharacter(new CharacterState(villainId, "Malachar", VitalStatus.Alive, location2));

        var ruleEngine = new RuleEngine();
        var transitionService = new StateTransitionService(null, ruleEngine);
        var progressionService = new ProgressionService(transitionService);

        // Act - execute valid sequence
        var actions = new StoryAction[]
        {
            new MoveCharacterAction(heroId, location2),
            new TriggerEncounterAction(heroId, villainId, location2),
            new RecordCharacterDeathAction(villainId, location2, "Defeated"),
            new RecordRevelationAction(heroId, "Victory achieved")
        };

        var currentState = state;
        foreach (var action in actions)
        {
            var result = progressionService.Progress(currentState, action);
            currentState = ((Result<StoryState>.Success)result).Value;
        }

        // Assert
        currentState.GetCharacter(villainId)?.VitalStatus.Should().Be(VitalStatus.Dead);
        currentState.GetCharacter(heroId)?.KnownFacts.Should().Contain("Victory achieved");
    }
}
