using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Tests;

/// <summary>
/// Unit tests for Phase 1.6 - Core module
/// Tests fundamental types and abstractions
/// </summary>
public class Phase1Step6CoreTests
{
    [Fact]
    public void Result_Ok_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result<int>.Ok(42);

        // Assert
        result.Should().BeOfType<Result<int>.Success>();
        var success = (Result<int>.Success)result;
        success.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Fail_ShouldCreateFailureResult()
    {
        // Act
        var result = Result<int>.Fail("Error message");

        // Assert
        result.Should().BeOfType<Result<int>.Failure>();
        var failure = (Result<int>.Failure)result;
        failure.Message.Should().Be("Error message");
    }

    [Fact]
    public void Result_Match_ShouldExecuteSuccessPath()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var value = result.Match(
            onSuccess: v => v * 2,
            onFailure: msg => -1
        );

        // Assert
        value.Should().Be(84);
    }

    [Fact]
    public void Result_Match_ShouldExecuteFailurePath()
    {
        // Arrange
        var result = Result<int>.Fail("Error");

        // Act
        var value = result.Match(
            onSuccess: v => v * 2,
            onFailure: msg => -1
        );

        // Assert
        value.Should().Be(-1);
    }

    [Fact]
    public void Id_New_ShouldCreateUniqueIds()
    {
        // Act
        var id1 = Id.New();
        var id2 = Id.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
        id2.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Id_From_ShouldCreateIdFromGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = Id.From(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Unit_Default_ShouldReturnUnit()
    {
        // Act
        var unit = Unit.Default();

        // Assert
        unit.Should().NotBeNull();
    }
}

/// <summary>
/// Unit tests for Phase 1.6 - Domain module
/// Tests domain entities and invariants
/// </summary>
public class Phase1Step6DomainTests
{
    [Fact]
    public void StoryWorld_Constructor_ShouldCreateValidWorld()
    {
        // Act
        var world = new StoryWorld(name: "Test World", description: "A test world");

        // Assert
        world.Id.Should().NotBe(default);
        world.Name.Should().Be("Test World");
        world.Description.Should().Be("A test world");
        world.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        world.GlobalRules.Should().BeEmpty();
    }

    [Fact]
    public void StoryWorld_Constructor_ShouldThrowIfNameEmpty()
    {
        // Act & Assert
        var action = () => new StoryWorld(name: "", description: "");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StoryArc_Constructor_ShouldCreateValidArc()
    {
        // Arrange
        var worldId = Id.New();

        // Act
        var arc = new StoryArc(worldId: worldId, title: "Test Arc");

        // Assert
        arc.Id.Should().NotBe(default);
        arc.WorldId.Should().Be(worldId);
        arc.Title.Should().Be("Test Arc");
        arc.Status.Should().Be(StoryProgressStatus.NotStarted);
    }

    [Fact]
    public void Character_Constructor_ShouldCreateValidCharacter()
    {
        // Act
        var traits = new Dictionary<string, string> { { "personality", "brave" }, { "trait", "determined" } };
        var character = new Character(name: "Aric", traits: traits);

        // Assert
        character.Id.Should().NotBe(default);
        character.Name.Should().Be("Aric");
        character.Traits.Should().ContainKey("personality");
        character.Traits.Should().ContainKey("trait");
        character.VitalStatus.Should().Be(VitalStatus.Alive);
    }

    [Fact]
    public void Character_Traits_ShouldBeImmutable()
    {
        // Arrange
        var traits = new Dictionary<string, string> { { "personality", "brave" } };
        var character = new Character(name: "Aric", traits: traits);

        // Act & Assert
        character.Traits.Should().NotBeEmpty();
        character.Traits.Should().ContainKey("personality");
    }

    [Fact]
    public void Location_Constructor_ShouldCreateValidLocation()
    {
        // Act
        var location = new Location(name: "Eldoria", description: "A mystical city");

        // Assert
        location.Id.Should().NotBe(default);
        location.Name.Should().Be("Eldoria");
        location.Description.Should().Be("A mystical city");
    }

    [Fact]
    public void Relationship_Constructor_ShouldCreateValidRelationship()
    {
        // Act
        var relationship = new Relationship(
            type: "friend",
            trust: 70,
            affection: 50
        );

        // Assert
        relationship.Type.Should().Be("friend");
        relationship.Trust.Should().Be(70);
        relationship.Affection.Should().Be(50);
    }

    [Fact]
    public void Event_ShouldBeCreatable()
    {
        // Arrange
        var characterId = Id.New();

        // Act
        var evt = new CharacterDeathEvent(
            characterId: characterId,
            locationId: null,
            cause: "Test"
        );

        // Assert
        evt.Id.Should().NotBe(default);
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CharacterEncounterEvent_ShouldBeCreatable()
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
        evt.Id.Should().NotBe(default);
        evt.ActorIds.Should().Contain(char1Id);
        evt.ActorIds.Should().Contain(char2Id);
    }
}

/// <summary>
/// Unit tests for Phase 1.6 - State module
/// Tests state entities and transitions
/// </summary>
public class Phase1Step6StateTests
{
    [Fact]
    public void WorldState_Constructor_ShouldCreateValidWorldState()
    {
        // Arrange
        var worldId = Id.New();

        // Act
        var worldState = new WorldState(worldId: worldId, worldName: "Test World");

        // Assert
        worldState.WorldId.Should().Be(worldId);
        worldState.WorldName.Should().Be("Test World");
        worldState.NarrativeTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        worldState.TotalEventCount.Should().Be(0);
    }

    [Fact]
    public void WorldState_AdvanceTime_ShouldProgressNarrativeTime()
    {
        // Arrange
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var originalTime = worldState.NarrativeTime;
        var delta = TimeSpan.FromDays(1);

        // Act
        var newWorldState = worldState.AdvanceTime(delta);

        // Assert
        newWorldState.NarrativeTime.Should().Be(originalTime.Add(delta));
        worldState.NarrativeTime.Should().Be(originalTime); // Original unchanged (immutable)
    }

    [Fact]
    public void WorldState_AdvanceTime_ShouldThrowIfNegativeDelta()
    {
        // Arrange
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");

        // Act & Assert
        var action = () => worldState.AdvanceTime(TimeSpan.FromDays(-1));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CharacterState_Constructor_ShouldCreateValidState()
    {
        // Arrange
        var characterId = Id.New();

        // Act
        var state = new CharacterState(
            characterId: characterId,
            name: "Aric",
            vitalStatus: VitalStatus.Alive
        );

        // Assert
        state.CharacterId.Should().Be(characterId);
        state.Name.Should().Be("Aric");
        state.VitalStatus.Should().Be(VitalStatus.Alive);
        state.KnownFacts.Should().BeEmpty();
    }

    [Fact]
    public void CharacterState_WithKnownFact_ShouldAddFact()
    {
        // Arrange
        var state = new CharacterState(characterId: Id.New(), name: "Aric");
        const string fact = "King of the North";

        // Act
        var newState = state.WithKnownFact(fact);

        // Assert
        newState.KnownFacts.Should().Contain(fact);
        state.KnownFacts.Should().NotContain(fact); // Original unchanged
    }

    [Fact]
    public void StoryState_Constructor_ShouldCreateValidState()
    {
        // Arrange
        var world = new StoryWorld(name: "Test World");
        var worldState = new WorldState(worldId: world.Id, worldName: world.Name);

        // Act
        var storyState = new StoryState(worldState: worldState);

        // Assert
        storyState.WorldState.Should().Be(worldState);
        storyState.Characters.Should().BeEmpty();
        storyState.EventHistory.Should().BeEmpty();
        storyState.CurrentChapter.Should().BeNull();
    }

    [Fact]
    public void StoryState_Create_ShouldInitializeWithCharacters()
    {
        // Arrange
        var characters = new Dictionary<Id, CharacterState>
        {
            { Id.New(), new CharacterState(Id.New(), "Aric") },
            { Id.New(), new CharacterState(Id.New(), "Lyra") }
        };

        // Act
        var storyState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test",
            characters: characters
        );

        // Assert
        storyState.Characters.Should().HaveCount(2);
    }

    [Fact]
    public void StoryState_Should_BeImmutable()
    {
        // Arrange
        var storyState = StoryState.Create(worldId: Id.New(), worldName: "Test");

        // Act & Assert
        storyState.Characters.Should().BeEmpty();
        storyState.EventHistory.Should().BeEmpty();
    }
}
