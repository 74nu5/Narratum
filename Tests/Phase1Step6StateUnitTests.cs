using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Domain;

namespace Narratum.Tests;

public class Phase1Step6StateUnitTests
{
    [Fact]
    public void WorldState_Constructor_ShouldCreateValidState()
    {
        var worldId = Id.New();
        var state = new WorldState(worldId: worldId, worldName: "Test World");
        state.WorldId.Should().Be(worldId);
        state.WorldName.Should().Be("Test World");
        state.NarrativeTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WorldState_AdvanceTime_ShouldIncrementTime()
    {
        var worldId = Id.New();
        var state = new WorldState(worldId: worldId, worldName: "Test");
        var timespan = TimeSpan.FromHours(1);
        var newState = state.AdvanceTime(timespan);
        newState.NarrativeTime.Should().Be(state.NarrativeTime.Add(timespan));
    }

    [Fact]
    public void WorldState_AdvanceTime_ShouldNotGoBackward()
    {
        var state = new WorldState(worldId: Id.New(), worldName: "Test");
        var action = () => state.AdvanceTime(TimeSpan.FromHours(-1));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorldState_ShouldBeImmutable()
    {
        var state1 = new WorldState(worldId: Id.New(), worldName: "Test");
        var originalTime = state1.NarrativeTime;
        var state2 = state1.AdvanceTime(TimeSpan.FromHours(1));
        state1.NarrativeTime.Should().Be(originalTime);
        state2.NarrativeTime.Should().BeAfter(state1.NarrativeTime);
    }

    [Fact]
    public void CharacterState_Constructor_ShouldCreateValidState()
    {
        var characterId = Id.New();
        var state = new CharacterState(characterId: characterId, name: "Aric");
        state.CharacterId.Should().Be(characterId);
        state.Name.Should().Be("Aric");
        state.VitalStatus.Should().Be(VitalStatus.Alive);
    }

    [Fact]
    public void CharacterState_WithKnownFact_ShouldAddFact()
    {
        var characterId = Id.New();
        var state = new CharacterState(characterId: characterId, name: "Aric");
        var newState = state.WithKnownFact("Fact1");
        newState.KnownFacts.Should().Contain("Fact1");
        state.KnownFacts.Should().NotContain("Fact1");
    }

    [Fact]
    public void CharacterState_ShouldBeImmutable()
    {
        var characterId = Id.New();
        var state1 = new CharacterState(characterId: characterId, name: "Aric");
        var state2 = state1.WithKnownFact("Fact1");
        state1.KnownFacts.Should().NotContain("Fact1");
        state2.KnownFacts.Should().Contain("Fact1");
    }

    [Fact]
    public void StoryState_Constructor_ShouldCreateValidState()
    {
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        state.WorldState.Should().Be(worldState);
        state.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StoryState_Create_ShouldCreateCompleteState()
    {
        var worldId = Id.New();
        var state = StoryState.Create(worldId: worldId, worldName: "Test World");
        state.Should().NotBeNull();
        state.WorldState.WorldId.Should().Be(worldId);
        state.WorldState.WorldName.Should().Be("Test World");
    }

    [Fact]
    public void StoryState_ShouldBeImmutable()
    {
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state1 = new StoryState(worldState: worldState);
        var characterId = Id.New();
        var characterState = new CharacterState(characterId: characterId, name: "Aric");
        var state2 = state1.WithCharacter(characterState);
        state1.Characters.Should().NotContainKey(characterId);
        state2.Characters.Should().ContainKey(characterId);
    }

    [Fact]
    public void CharacterState_WithVitalStatus_ShouldUpdateStatus()
    {
        var characterId = Id.New();
        var state = new CharacterState(characterId: characterId, name: "Aric");
        var newState = state.WithVitalStatus(VitalStatus.Dead);
        newState.VitalStatus.Should().Be(VitalStatus.Dead);
        state.VitalStatus.Should().Be(VitalStatus.Alive);
    }

    [Fact]
    public void WorldState_WithCurrentArc_ShouldSetArc()
    {
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var arcId = Id.New();
        var newState = worldState.WithCurrentArc(arcId);
        newState.CurrentArcId.Should().Be(arcId);
        worldState.CurrentArcId.Should().BeNull();
    }

    [Fact]
    public void WorldState_WithEventOccurred_ShouldIncrementCounter()
    {
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var newState = worldState.WithEventOccurred();
        newState.TotalEventCount.Should().Be(1);
        worldState.TotalEventCount.Should().Be(0);
    }

    [Fact]
    public void StoryState_WithCharacters_ShouldAddMultiple()
    {
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var char1 = new CharacterState(characterId: Id.New(), name: "Aric");
        var char2 = new CharacterState(characterId: Id.New(), name: "Lyssa");
        var newState = state.WithCharacters(char1, char2);
        newState.Characters.Should().HaveCount(2);
        state.Characters.Should().BeEmpty();
    }
}
