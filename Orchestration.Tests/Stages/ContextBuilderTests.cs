using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour ContextBuilder.
/// </summary>
public class ContextBuilderTests
{
    private readonly StoryState _testState;
    private readonly NarrativeIntent _testIntent;

    public ContextBuilderTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testIntent = NarrativeIntent.Continue();
    }

    [Fact]
    public async Task BuildAsync_WithMinimalInputs_ShouldSucceed()
    {
        // Arrange
        var builder = new ContextBuilder();

        // Act
        var result = await builder.BuildAsync(_testState, _testIntent);

        // Assert
        result.Should().BeOfType<Result<NarrativeContext>.Success>();
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.State.Should().Be(_testState);
        context.ContextId.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_WithNullState_ShouldThrow()
    {
        // Arrange
        var builder = new ContextBuilder();

        // Act
        var action = async () => await builder.BuildAsync(null!, _testIntent);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_WithNullIntent_ShouldThrow()
    {
        // Arrange
        var builder = new ContextBuilder();

        // Act
        var action = async () => await builder.BuildAsync(_testState, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var builder = new ContextBuilder();

        // Act
        var result = await builder.BuildAsync(_testState, _testIntent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.Metadata.Should().ContainKey("intentType");
        context.Metadata.Should().ContainKey("characterCount");
        context.Metadata.Should().ContainKey("hasLocation");
    }

    [Fact]
    public async Task BuildAsync_WithCharacters_ShouldIncludeAliveCharacters()
    {
        // Arrange
        var builder = new ContextBuilder();
        var charId1 = Id.New();
        var charId2 = Id.New();
        var charId3 = Id.New();

        var state = _testState
            .WithCharacter(new CharacterState(charId1, "Alice", VitalStatus.Alive))
            .WithCharacter(new CharacterState(charId2, "Bob", VitalStatus.Alive))
            .WithCharacter(new CharacterState(charId3, "Charlie", VitalStatus.Dead));

        // Act
        var result = await builder.BuildAsync(state, _testIntent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.ActiveCharacters.Should().HaveCount(2);
        context.ActiveCharacters.Select(c => c.Name).Should().Contain("Alice", "Bob");
        context.ActiveCharacters.Select(c => c.Name).Should().NotContain("Charlie");
    }

    [Fact]
    public async Task BuildAsync_WithTargetCharacters_ShouldOnlyIncludeTargeted()
    {
        // Arrange
        var builder = new ContextBuilder();
        var charId1 = Id.New();
        var charId2 = Id.New();

        var state = _testState
            .WithCharacter(new CharacterState(charId1, "Alice", VitalStatus.Alive))
            .WithCharacter(new CharacterState(charId2, "Bob", VitalStatus.Alive));

        var intent = new NarrativeIntent(
            IntentType.GenerateDialogue,
            targetCharacterIds: new[] { charId1 });

        // Act
        var result = await builder.BuildAsync(state, intent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.ActiveCharacters.Should().HaveCount(1);
        context.ActiveCharacters[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task BuildAsync_WithTargetLocation_ShouldSetCurrentLocation()
    {
        // Arrange
        var builder = new ContextBuilder();
        var locationId = Id.New();
        var intent = NarrativeIntent.DescribeLocation(locationId, "Describe the castle");

        // Act
        var result = await builder.BuildAsync(_testState, intent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.CurrentLocation.Should().NotBeNull();
        context.CurrentLocation!.LocationId.Should().Be(locationId);
    }

    [Fact]
    public async Task BuildAsync_WithCharactersAtLocation_ShouldIncludePresentCharacters()
    {
        // Arrange
        var builder = new ContextBuilder();
        var locationId = Id.New();
        var charId = Id.New();

        var state = _testState
            .WithCharacter(new CharacterState(charId, "Alice", VitalStatus.Alive, locationId));

        var intent = NarrativeIntent.DescribeLocation(locationId);

        // Act
        var result = await builder.BuildAsync(state, intent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.CurrentLocation.Should().NotBeNull();
        context.CurrentLocation!.PresentCharacterIds.Should().Contain(charId);
    }

    [Fact]
    public async Task BuildAsync_WithEventHistory_ShouldIncludeRecentEvents()
    {
        // Arrange
        var builder = new ContextBuilder();
        var charId = Id.New();

        // Add some events to the state using WithEvent
        var state = _testState;
        for (int i = 0; i < 15; i++)
        {
            var evt = new CharacterEncounterEvent(charId, charId);
            state = state.WithEvent(evt);
        }

        // Act
        var result = await builder.BuildAsync(state, _testIntent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.RecentEvents.Should().HaveCount(10); // Max 10 recent events
    }

    [Fact]
    public async Task BuildAsync_WithoutLocation_ShouldDetermineFromCharacters()
    {
        // Arrange
        var builder = new ContextBuilder();
        var locationId = Id.New();
        var charId1 = Id.New();
        var charId2 = Id.New();

        var state = _testState
            .WithCharacter(new CharacterState(charId1, "Alice", VitalStatus.Alive, locationId))
            .WithCharacter(new CharacterState(charId2, "Bob", VitalStatus.Alive, locationId));

        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(state, intent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.CurrentLocation.Should().NotBeNull();
        context.CurrentLocation!.LocationId.Should().Be(locationId);
    }

    [Fact]
    public async Task BuildAsync_WithNoCharactersAtLocation_ShouldHaveNoLocation()
    {
        // Arrange
        var builder = new ContextBuilder();
        var state = _testState
            .WithCharacter(new CharacterState(Id.New(), "Alice", VitalStatus.Alive));

        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(state, intent);

        // Assert
        var context = ((Result<NarrativeContext>.Success)result).Value;
        context.CurrentLocation.Should().BeNull();
    }
}
