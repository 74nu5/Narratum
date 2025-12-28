using Narratum.Domain;
using Narratum.State;
using Narratum.Core;
using Xunit;

namespace Narratum.Tests;

/// <summary>
/// Integration tests for Step 1.2: Core and Domain.
/// Tests the creation and basic manipulation of narrative entities.
/// </summary>
public class Phase1Step2IntegrationTests
{
    [Fact]
    public void CreateStoryWorld_ShouldSucceed()
    {
        // Arrange & Act
        var world = new StoryWorld("Aethermoor", "A mystical world of magic and wonder");

        // Assert
        Assert.NotNull(world);
        Assert.NotEqual(Guid.Empty, world.Id.Value);
        Assert.Equal("Aethermoor", world.Name);
        Assert.Empty(world.GlobalRules);
    }

    [Fact]
    public void CreateCharacter_ShouldSucceed()
    {
        // Arrange
        var traits = new Dictionary<string, string>
        {
            { "class", "warrior" },
            { "alignment", "neutral good" }
        };

        // Act
        var character = new Character("Aric the Brave", traits);

        // Assert
        Assert.NotNull(character);
        Assert.Equal("Aric the Brave", character.Name);
        Assert.Equal(VitalStatus.Alive, character.VitalStatus);
        Assert.Equal(traits, character.Traits);
    }

    [Fact]
    public void CreateLocation_ShouldSucceed()
    {
        // Act
        var location = new Location("Silvermist Forest", "An ancient forest shrouded in magic");

        // Assert
        Assert.NotNull(location);
        Assert.Equal("Silvermist Forest", location.Name);
        Assert.Null(location.ParentLocationId);
    }

    [Fact]
    public void CreateStoryArc_ShouldSucceed()
    {
        // Arrange
        var worldId = Id.New();

        // Act
        var arc = new StoryArc(worldId, "The Quest for the Crystal", 
            "Find the legendary crystal hidden in the mountains");

        // Assert
        Assert.NotNull(arc);
        Assert.Equal("The Quest for the Crystal", arc.Title);
        Assert.Equal(StoryProgressStatus.NotStarted, arc.Status);
        Assert.Empty(arc.Chapters);
    }

    [Fact]
    public void CreateAndProgressChapter_ShouldSucceed()
    {
        // Arrange
        var arcId = Id.New();
        var chapter = new StoryChapter(arcId, 0);

        // Act
        chapter.Start();

        // Assert
        Assert.Equal(StoryProgressStatus.InProgress, chapter.Status);
        Assert.NotNull(chapter.StartedAt);
    }

    [Fact]
    public void CreateEvent_ShouldSucceed()
    {
        // Arrange
        var characterId = Id.New();
        var locationId = Id.New();

        // Act
        var deathEvent = new CharacterDeathEvent(characterId, locationId, "Fell in battle");

        // Assert
        Assert.NotNull(deathEvent);
        Assert.Equal("CharacterDeath", deathEvent.Type);
        Assert.NotNull(deathEvent.Id);
        Assert.Contains(characterId, deathEvent.ActorIds);
        Assert.Equal("Fell in battle", deathEvent.GetCause());
    }

    [Fact]
    public void CreateStoryState_ShouldSucceed()
    {
        // Arrange
        var worldId = Id.New();
        var characterId = Id.New();
        var characterState = new CharacterState(characterId, "Aric", VitalStatus.Alive);

        // Act
        var storyState = StoryState.Create(worldId, "Aethermoor")
            .WithCharacter(characterState);

        // Assert
        Assert.NotNull(storyState);
        Assert.Equal("Aethermoor", storyState.WorldState.WorldName);
        Assert.Single(storyState.Characters);
        Assert.Empty(storyState.EventHistory);
    }

    [Fact]
    public void AddEventToStoryState_ShouldBeImmutable()
    {
        // Arrange
        var worldId = Id.New();
        var characterId = Id.New();
        var state = StoryState.Create(worldId, "World");

        // Act
        var deathEvent = new CharacterDeathEvent(characterId);
        var newState = state.WithEvent(deathEvent);

        // Assert - Original state unchanged
        Assert.Empty(state.EventHistory);
        // New state has the event
        Assert.Single(newState.EventHistory);
        Assert.Equal(1, newState.WorldState.TotalEventCount);
    }

    [Fact]
    public void CharacterMovement_ShouldUpdateLocation()
    {
        // Arrange
        var characterId = Id.New();
        var location1 = Id.New();
        var location2 = Id.New();
        var characterState = new CharacterState(characterId, "Aric", VitalStatus.Alive, location1);

        // Act
        var newState = characterState.MoveTo(location2);

        // Assert
        Assert.Equal(location1, characterState.CurrentLocationId); // Original unchanged
        Assert.Equal(location2, newState.CurrentLocationId); // New state updated
    }

    [Fact]
    public void DeadCharacterCannotMove_ShouldThrow()
    {
        // Arrange
        var characterId = Id.New();
        var characterState = new CharacterState(characterId, "Aric", VitalStatus.Dead);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => characterState.MoveTo(Id.New()));
    }

    [Fact]
    public void CharacterRelationships_ShouldBeSymmetric()
    {
        // Arrange
        var character = new Character("Aric");
        var otherId = Id.New();
        var relationship = new Relationship("friend", trust: 50, affection: 75);

        // Act
        character.SetRelationship(otherId, relationship);

        // Assert
        var retrieved = character.GetRelationship(otherId);
        Assert.NotNull(retrieved);
        Assert.Equal("friend", retrieved.Type);
        Assert.Equal(50, retrieved.Trust);
        Assert.Equal(75, retrieved.Affection);
    }

    [Fact]
    public void CompleteNarrativeScenario_ShouldBeDeterministic()
    {
        // This test demonstrates a complete deterministic narrative flow
        // Arrange
        var worldId = Id.New();
        var heroId = Id.New();
        var villainId = Id.New();
        var locationId = Id.New();

        // Create initial state
        var heroState = new CharacterState(heroId, "Aric", VitalStatus.Alive, locationId);
        var villainState = new CharacterState(villainId, "Malachar", VitalStatus.Alive, locationId);
        var state = StoryState.Create(worldId, "Aethermoor")
            .WithCharacters(heroState, villainState);

        // Act - Simulate sequence of events
        var encounter = new CharacterEncounterEvent(heroId, villainId, locationId);
        state = state.WithEvent(encounter);

        var battle = new CharacterDeathEvent(villainId, locationId, "Defeated in combat");
        state = state.WithEvent(battle);

        var revelation = new RevelationEvent(heroId, "Malachar was the lost heir");
        state = state.WithEvent(revelation);

        // Assert
        Assert.Equal(3, state.EventHistory.Count);
        Assert.Equal(3, state.WorldState.TotalEventCount);

        // Verify event types
        Assert.Equal("CharacterEncounter", state.EventHistory[0].Type);
        Assert.Equal("CharacterDeath", state.EventHistory[1].Type);
        Assert.Equal("Revelation", state.EventHistory[2].Type);
    }

    [Fact]
    public void StateSnapshot_ShouldCaptureMoment()
    {
        // Arrange
        var worldId = Id.New();
        var heroId = Id.New();
        var state = StoryState.Create(worldId, "Aethermoor")
            .WithCharacter(new CharacterState(heroId, "Aric"));

        // Act
        var snapshot = state.CreateSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(worldId, snapshot.WorldId);
        Assert.NotNull(snapshot.SnapshotId);
        Assert.Equal(state, snapshot.State);
    }

    [Fact]
    public void CharacterCannotHaveSelfRelationship_ShouldThrow()
    {
        // Arrange
        var character = new Character("Aric");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            character.SetRelationship(character.Id, new Relationship("self", 0, 0)));
    }

    [Fact]
    public void LocationAccessibility_ShouldBeManaged()
    {
        // Arrange
        var location1 = new Location("Forest");
        var location2 = new Location("Cave");

        // Act
        location1.AddAccessibleLocation(location2.Id);

        // Assert
        Assert.True(location1.IsAccessibleFrom(location2.Id));
        Assert.False(location2.IsAccessibleFrom(location1.Id)); // Unidirectional
    }

    [Fact]
    public void InvalidNames_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new StoryWorld(""));
        Assert.Throws<ArgumentException>(() => new Character(""));
        Assert.Throws<ArgumentException>(() => new Location(""));
        Assert.Throws<ArgumentException>(() => new StoryArc(Id.New(), ""));
    }

    [Fact]
    public void TimeShouldBeMonotonic()
    {
        // Arrange
        var worldState = new WorldState(Id.New(), "World");

        // Act
        var t1 = worldState.NarrativeTime;
        var advanced = worldState.AdvanceTime(TimeSpan.FromHours(1));
        var t2 = advanced.NarrativeTime;

        // Assert
        Assert.True(t2 > t1);
        Assert.Throws<ArgumentException>(() => worldState.AdvanceTime(TimeSpan.FromHours(-1)));
    }
}
