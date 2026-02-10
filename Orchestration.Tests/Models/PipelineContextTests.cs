using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;
using Xunit;

namespace Narratum.Orchestration.Tests.Models;

/// <summary>
/// Tests unitaires pour PipelineContext.
/// </summary>
public class PipelineContextTests
{
    private readonly StoryState _testState;
    private readonly NarrativeIntent _testIntent;

    public PipelineContextTests()
    {
        _testState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
        _testIntent = NarrativeIntent.Continue();
    }

    [Fact]
    public void Constructor_ShouldCreateValidContext()
    {
        // Act
        var context = new PipelineContext(_testState, _testIntent);

        // Assert
        context.ContextId.Should().NotBe(default(Id));
        context.StoryState.Should().Be(_testState);
        context.Intent.Should().Be(_testIntent);
        context.CurrentMemorandum.Should().BeNull();
        context.CanonicalState.Should().BeNull();
        context.Summaries.Should().BeEmpty();
        context.ActiveCharacterIds.Should().BeEmpty();
        context.CurrentLocationId.Should().BeNull();
        context.Metadata.Should().BeEmpty();
        context.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithNullStoryState_ShouldThrow()
    {
        // Act
        var action = () => new PipelineContext(null!, _testIntent);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("storyState");
    }

    [Fact]
    public void Constructor_WithNullIntent_ShouldThrow()
    {
        // Act
        var action = () => new PipelineContext(_testState, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("intent");
    }

    [Fact]
    public void Constructor_WithActiveCharacters_ShouldSetCharacters()
    {
        // Arrange
        var charIds = new[] { Id.New(), Id.New() };

        // Act
        var context = new PipelineContext(
            _testState,
            _testIntent,
            activeCharacterIds: charIds);

        // Assert
        context.ActiveCharacterIds.Should().HaveCount(2);
        context.ActiveCharacterIds.Should().Contain(charIds[0]);
        context.ActiveCharacterIds.Should().Contain(charIds[1]);
    }

    [Fact]
    public void Constructor_WithLocation_ShouldSetLocation()
    {
        // Arrange
        var locationId = Id.New();

        // Act
        var context = new PipelineContext(
            _testState,
            _testIntent,
            currentLocationId: locationId);

        // Assert
        context.CurrentLocationId.Should().Be(locationId);
    }

    [Fact]
    public void Constructor_WithMetadata_ShouldSetMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var context = new PipelineContext(
            _testState,
            _testIntent,
            metadata: metadata);

        // Assert
        context.Metadata.Should().ContainKey("key");
        context.Metadata["key"].Should().Be("value");
    }

    [Fact]
    public void CreateMinimal_ShouldCreateBasicContext()
    {
        // Act
        var context = PipelineContext.CreateMinimal(_testState, _testIntent);

        // Assert
        context.StoryState.Should().Be(_testState);
        context.Intent.Should().Be(_testIntent);
        context.CurrentMemorandum.Should().BeNull();
    }

    [Fact]
    public void WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var context = new PipelineContext(_testState, _testIntent);

        // Act
        var newContext = context.WithMetadata("testKey", 42);

        // Assert
        newContext.Metadata.Should().ContainKey("testKey");
        newContext.Metadata["testKey"].Should().Be(42);
        context.Metadata.Should().NotContainKey("testKey"); // Original unchanged
    }

    [Fact]
    public void WithMetadata_ShouldOverwriteExisting()
    {
        // Arrange
        var context = new PipelineContext(
            _testState,
            _testIntent,
            metadata: new Dictionary<string, object> { ["key"] = "old" });

        // Act
        var newContext = context.WithMetadata("key", "new");

        // Assert
        newContext.Metadata["key"].Should().Be("new");
    }

    [Fact]
    public void WithActiveCharacter_ShouldAddCharacter()
    {
        // Arrange
        var context = new PipelineContext(_testState, _testIntent);
        var charId = Id.New();

        // Act
        var newContext = context.WithActiveCharacter(charId);

        // Assert
        newContext.ActiveCharacterIds.Should().Contain(charId);
        context.ActiveCharacterIds.Should().BeEmpty(); // Original unchanged
    }

    [Fact]
    public void WithActiveCharacter_ShouldNotDuplicate()
    {
        // Arrange
        var charId = Id.New();
        var context = new PipelineContext(
            _testState,
            _testIntent,
            activeCharacterIds: new[] { charId });

        // Act
        var newContext = context.WithActiveCharacter(charId);

        // Assert
        newContext.ActiveCharacterIds.Should().HaveCount(1);
    }

    [Fact]
    public void ActiveCharacterIds_ShouldBeImmutable()
    {
        // Arrange
        var charIds = new List<Id> { Id.New() };
        var context = new PipelineContext(
            _testState,
            _testIntent,
            activeCharacterIds: charIds);

        // Act - Try to modify the original list
        charIds.Add(Id.New());

        // Assert - Context should not be affected
        context.ActiveCharacterIds.Should().HaveCount(1);
    }
}
