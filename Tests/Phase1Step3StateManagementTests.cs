using Narratum.Core;
using Narratum.Domain;
using Narratum.Simulation;
using Narratum.State;
using Xunit;
using FluentAssertions;

namespace Narratum.Tests;

/// <summary>
/// Integration tests for Step 1.3: State Management
/// Tests state transitions and progression services.
/// </summary>
public class Phase1Step3StateManagementTests
{
    [Fact]
    public void MoveCharacterAction_ShouldTransitionState()
    {
        // Arrange
        var characterId = Id.New();
        var location1Id = Id.New();
        var location2Id = Id.New();
        
        var characterState = new CharacterState(characterId, "Aric", VitalStatus.Alive, location1Id);
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(characterState);
        
        var transitionService = new StateTransitionService();
        var action = new MoveCharacterAction(characterId, location2Id);
        
        // Act
        var result = transitionService.TransitionState(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        newState.GetCharacter(characterId)?.CurrentLocationId.Should().Be(location2Id);
        newState.EventHistory.Should().HaveCount(1);
        newState.EventHistory[0].Should().BeOfType<CharacterMovedEvent>();
    }

    [Fact]
    public void DeadCharacterCannotMove_ShouldFail()
    {
        // Arrange
        var characterId = Id.New();
        var characterState = new CharacterState(characterId, "Aric", VitalStatus.Dead);
        var state = StoryState.Create(Id.New(), "World").WithCharacter(characterState);
        
        var transitionService = new StateTransitionService();
        var action = new MoveCharacterAction(characterId, Id.New());
        
        // Act
        var result = transitionService.ValidateAction(state, action);
        
        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void TriggerEncounterAction_ShouldGenerateEvent()
    {
        // Arrange
        var char1Id = Id.New();
        var char2Id = Id.New();
        var locationId = Id.New();
        
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(char1Id, "Aric", VitalStatus.Alive))
            .WithCharacter(new CharacterState(char2Id, "Malachar", VitalStatus.Alive));
        
        var transitionService = new StateTransitionService();
        var action = new TriggerEncounterAction(char1Id, char2Id, locationId);
        
        // Act
        var result = transitionService.TransitionState(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        newState.EventHistory.Should().HaveCount(1);
        newState.EventHistory[0].Should().BeOfType<CharacterEncounterEvent>();
    }

    [Fact]
    public void RecordCharacterDeathAction_ShouldUpdateStatusAndGenerateEvent()
    {
        // Arrange
        var characterId = Id.New();
        var locationId = Id.New();
        
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive));
        
        var transitionService = new StateTransitionService();
        var action = new RecordCharacterDeathAction(characterId, locationId, "Fell in combat");
        
        // Act
        var result = transitionService.TransitionState(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        newState.GetCharacter(characterId)?.VitalStatus.Should().Be(VitalStatus.Dead);
        newState.EventHistory.Should().HaveCount(1);
        newState.EventHistory[0].Should().BeOfType<CharacterDeathEvent>();
    }

    [Fact]
    public void AdvanceTimeAction_ShouldUpdateWorldTime()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var initialTime = state.WorldState.NarrativeTime;
        
        var transitionService = new StateTransitionService();
        var action = new AdvanceTimeAction(TimeSpan.FromHours(24));
        
        // Act
        var result = transitionService.TransitionState(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        newState.WorldState.NarrativeTime.Should().Be(initialTime.AddHours(24));
    }

    [Fact]
    public void AdvanceTimeWithNegativeDuration_ShouldFail()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var transitionService = new StateTransitionService();
        var action = new AdvanceTimeAction(TimeSpan.FromHours(-1));
        
        // Act
        var result = transitionService.ValidateAction(state, action);
        
        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void RecordRevelationAction_ShouldAddKnownFactAndGenerateEvent()
    {
        // Arrange
        var characterId = Id.New();
        var revelation = "The villain is actually your brother!";
        
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric"));
        
        var transitionService = new StateTransitionService();
        var action = new RecordRevelationAction(characterId, revelation);
        
        // Act
        var result = transitionService.TransitionState(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        newState.GetCharacter(characterId)?.KnownFacts.Should().Contain(revelation);
        newState.EventHistory.Should().HaveCount(1);
        newState.EventHistory[0].Should().BeOfType<RevelationEvent>();
    }

    [Fact]
    public void ProgressionService_ShouldOrchestrateTransitions()
    {
        // Arrange
        var characterId = Id.New();
        var locationId = Id.New();
        
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive));
        
        var transitionService = new StateTransitionService();
        var progressionService = new ProgressionService(transitionService);
        
        // Act
        var action = new MoveCharacterAction(characterId, locationId);
        var result = progressionService.Progress(state, action);
        
        // Assert
        result.Should().BeOfType<Result<StoryState>.Success>();
        var newState = ((Result<StoryState>.Success)result).Value;
        progressionService.GetEventCount(newState).Should().Be(1);
        progressionService.GetEventHistory(newState).Should().HaveCount(1);
    }

    [Fact]
    public void MultipleActions_ShouldChainCorrectly()
    {
        // Arrange
        var characterId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        var location3 = Id.New();
        
        var state = StoryState.Create(Id.New(), "World")
            .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive, location1));
        
        var transitionService = new StateTransitionService();
        var progressionService = new ProgressionService(transitionService);
        
        // Act - sequence of moves
        var state1 = progressionService.Progress(state, new MoveCharacterAction(characterId, location2));
        state1.Should().BeOfType<Result<StoryState>.Success>();
        
        var newState1 = ((Result<StoryState>.Success)state1).Value;
        var state2 = progressionService.Progress(newState1, new MoveCharacterAction(characterId, location3));
        state2.Should().BeOfType<Result<StoryState>.Success>();
        
        var newState2 = ((Result<StoryState>.Success)state2).Value;
        
        // Assert
        newState2.GetCharacter(characterId)?.CurrentLocationId.Should().Be(location3);
        progressionService.GetEventCount(newState2).Should().Be(2);
    }

    [Fact]
    public void DeterministicSequence_ShouldProduceSameResult()
    {
        // This test verifies that the same sequence of actions produces identical states
        // Arrange
        var characterId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        var location3 = Id.New();
        
        var createInitialState = () =>
            StoryState.Create(Id.New(), "World")
                .WithCharacter(new CharacterState(characterId, "Aric", VitalStatus.Alive, location1));
        
        var transitionService = new StateTransitionService();
        var progressionService = new ProgressionService(transitionService);
        
        var actions = new StoryAction[]
        {
            new MoveCharacterAction(characterId, location2),
            new AdvanceTimeAction(TimeSpan.FromHours(1)),
            new MoveCharacterAction(characterId, location3)
        };
        
        // Act - run sequence twice
        var state1 = createInitialState();
        foreach (var action in actions)
        {
            var result = progressionService.Progress(state1, action);
            state1 = ((Result<StoryState>.Success)result).Value;
        }
        
        var state2 = createInitialState();
        foreach (var action in actions)
        {
            var result = progressionService.Progress(state2, action);
            state2 = ((Result<StoryState>.Success)result).Value;
        }
        
        // Assert - both sequences produce identical results
        state1.EventHistory.Count.Should().Be(state2.EventHistory.Count);
        state1.GetCharacter(characterId)?.CurrentLocationId.Should().Be(state2.GetCharacter(characterId)?.CurrentLocationId);
        state1.WorldState.TotalEventCount.Should().Be(state2.WorldState.TotalEventCount);
    }

    [Fact]
    public void InvalidCharacterInAction_ShouldFail()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "World");
        var transitionService = new StateTransitionService();
        var action = new MoveCharacterAction(Id.New(), Id.New()); // Non-existent character
        
        // Act
        var result = transitionService.ValidateAction(state, action);
        
        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void NullStateOrAction_ShouldFail()
    {
        // Arrange
        var transitionService = new StateTransitionService();
        var state = StoryState.Create(Id.New(), "World");
        
        // Act & Assert
        transitionService.ValidateAction(null, new MoveCharacterAction(Id.New(), Id.New()))
            .Should().BeOfType<Result<Unit>.Failure>();
        
        transitionService.ValidateAction(state, null)
            .Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void CompleteNarrativeFlow_WithChaptersAndEvents()
    {
        // Arrange
        var worldId = Id.New();
        var heroId = Id.New();
        var villainId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        
        var state = StoryState.Create(worldId, "Aethermoor")
            .WithCharacter(new CharacterState(heroId, "Aric", VitalStatus.Alive, location1))
            .WithCharacter(new CharacterState(villainId, "Malachar", VitalStatus.Alive, location2));
        
        var transitionService = new StateTransitionService();
        var progressionService = new ProgressionService(transitionService);
        
        // Act - simulate a complete sequence
        var actions = new StoryAction[]
        {
            new AdvanceTimeAction(TimeSpan.FromHours(1)),
            new MoveCharacterAction(heroId, location2),
            new TriggerEncounterAction(heroId, villainId, location2),
            new AdvanceTimeAction(TimeSpan.FromHours(2)),
            new RecordCharacterDeathAction(villainId, location2, "Defeated in combat"),
            new RecordRevelationAction(heroId, "Malachar was your lost brother")
        };
        
        var currentState = state;
        foreach (var action in actions)
        {
            var result = progressionService.Progress(currentState, action);
            currentState = ((Result<StoryState>.Success)result).Value;
        }
        
        // Assert
        progressionService.GetEventCount(currentState).Should().Be(4); // Encounter, Death, Revelation, Move
        currentState.GetCharacter(villainId)?.VitalStatus.Should().Be(VitalStatus.Dead);
        currentState.GetCharacter(heroId)?.KnownFacts.Should().Contain("Malachar was your lost brother");
        currentState.WorldState.TotalEventCount.Should().Be(4);
    }
}
