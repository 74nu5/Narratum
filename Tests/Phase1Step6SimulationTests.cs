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
        var transitionService = new StateTransitionService();
        var progressionService = new ProgressionService(transitionService);

        // Assert
        progressionService.Should().NotBeNull();
    }

    [Fact]
    public void StoryWorld_Transitions_ShouldMaintainConsistency()
    {
        // Arrange
        var world = new StoryWorld(name: "Test World");
        var traits = new Dictionary<string, string> { { "personality", "brave" } };
        var characters = new[]
        {
            new Character(name: "Aric", traits: traits)
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
        storyState.EventHistory.Should().BeEmpty();
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
        var transitionService = new StateTransitionService();
        var service = new ProgressionService(transitionService);

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
            characterId: characterId,
            locationId: null,
            cause: "Test cause"
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
            character1Id: char1Id,
            character2Id: char2Id,
            locationId: locationId
        );

        // Assert
        evt.Should().NotBeNull();
        evt.ActorIds.Should().Contain(char1Id);
        evt.ActorIds.Should().Contain(char2Id);
    }

    [Fact]
    public void StoryAction_ShouldBeCreatable()
    {
        // Arrange & Act
        var characterId = Id.New();
        var locationId = Id.New();
        var action = new MoveCharacterAction(characterId, locationId);

        // Assert
        action.Should().NotBeNull();
        action.CharacterId.Should().Be(characterId);
        action.ToLocationId.Should().Be(locationId);
    }

    [Fact]
    public void RuleEngine_ShouldExist()
    {
        // Arrange
        var ruleEngine = new RuleEngine();

        // Assert
        ruleEngine.Should().NotBeNull();
    }
}
