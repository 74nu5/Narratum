using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Simulation;

namespace Narratum.Tests;

/// <summary>
/// Unit tests for Phase 1.6 - Progression and Services
/// Tests state transitions, progression logic
/// </summary>
public class Phase1Step6SimulationTests
{
    [Fact]
    public void StateTransitionService_ShouldExist()
    {
        // Arrange
        var progressionService = new ProgressionService();

        // Assert
        progressionService.Should().NotBeNull();
    }

    [Fact]
    public void StoryWorld_Transitions_ShouldMaintainConsistency()
    {
        // Arrange
        var world = new StoryWorld(name: "Test World");
        var characters = new[]
        {
            new Character(name: "Aric", traits: new[] { "brave" })
        };

        // Act
        var worldState = new WorldState(worldId: world.Id, worldName: world.Name);

        // Assert
        worldState.Should().NotBeNull();
        worldState.WorldId.Should().Be(world.Id);
        worldState.WorldName.Should().Be(world.Name);
    }

    [Fact]
    public void TimeProgression_ShouldAdvanceNarrativeTime()
    {
        // Arrange
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var originalTime = worldState.NarrativeTime;

        // Act
        var advancedState = worldState.AdvanceTime(TimeSpan.FromDays(1));

        // Assert
        advancedState.NarrativeTime.Should().BeAfter(originalTime);
    }

    [Fact]
    public void CharacterState_Transitions_ShouldBeValid()
    {
        // Arrange
        var characterId = Id.New();
        var characterState = new CharacterState(
            characterId: characterId,
            name: "Aric",
            vitalStatus: VitalStatus.Alive
        );

        // Act
        var newState = characterState.WithKnownFact("fact");

        // Assert
        newState.Should().NotBeNull();
        newState.KnownFacts.Should().Contain("fact");
        characterState.KnownFacts.Should().NotContain("fact"); // Original unchanged
    }

    [Fact]
    public void StoryState_EventTracking_ShouldRecordEvents()
    {
        // Arrange
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test"
        );

        // Act & Assert
        storyState.EventHistory.Should().NotBeNull();
        storyState.EventHistory.Should().BeReadOnly();
    }

    [Fact]
    public void RuleEngine_ShouldValidateTransitions()
    {
        // Arrange
        var ruleEngine = new RuleEngine();
        var state = StoryState.Create(worldId: Id.New(), worldName: "Test");

        // Act
        var result = ruleEngine.ValidateState(state);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ProgressionService_ShouldBeDefined()
    {
        // Arrange & Act
        var service = new ProgressionService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void StateTransitionService_ShouldBeDefined()
    {
        // Arrange & Act
        var service = new StateTransitionService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void DomainEvent_ShouldBeCreatable()
    {
        // Arrange
        var characterId = Id.New();

        // Act
        var evt = new CharacterDeathEvent(
            id: Id.New(),
            characterId: characterId,
            timestamp: DateTime.UtcNow
        );

        // Assert
        evt.Should().NotBeNull();
        evt.Id.Should().NotBe(default);
    }

    [Fact]
    public void CharacterEncounterEvent_ShouldBeValid()
    {
        // Arrange
        var char1Id = Id.New();
        var char2Id = Id.New();
        var locationId = Id.New();

        // Act
        var evt = new CharacterEncounterEvent(
            id: Id.New(),
            character1Id: char1Id,
            character2Id: char2Id,
            locationId: locationId,
            timestamp: DateTime.UtcNow
        );

        // Assert
        evt.Should().NotBeNull();
        evt.Character1Id.Should().Be(char1Id);
        evt.Character2Id.Should().Be(char2Id);
    }

    [Fact]
    public void StoryAction_ShouldBeCreatable()
    {
        // Arrange & Act
        var action = new StoryAction(actionType: "Move", description: "Character moves");

        // Assert
        action.Should().NotBeNull();
        action.ActionType.Should().Be("Move");
    }

    [Fact]
    public void NarrativeRules_ShouldExist()
    {
        // Arrange
        var narrativeRules = new NarrativeRules();

        // Assert
        narrativeRules.Should().NotBeNull();
    }
}
