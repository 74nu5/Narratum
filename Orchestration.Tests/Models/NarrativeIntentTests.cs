using FluentAssertions;
using Narratum.Core;
using Narratum.Orchestration.Models;
using Xunit;

namespace Narratum.Orchestration.Tests.Models;

/// <summary>
/// Tests unitaires pour NarrativeIntent.
/// </summary>
public class NarrativeIntentTests
{
    [Fact]
    public void Constructor_ShouldCreateValidIntent()
    {
        // Act
        var intent = new NarrativeIntent(IntentType.ContinueNarrative);

        // Assert
        intent.Id.Should().NotBe(default(Id));
        intent.Type.Should().Be(IntentType.ContinueNarrative);
        intent.Description.Should().BeNull();
        intent.TargetCharacterIds.Should().BeEmpty();
        intent.TargetLocationId.Should().BeNull();
        intent.Parameters.Should().BeEmpty();
        intent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithDescription_ShouldSetDescription()
    {
        // Act
        var intent = new NarrativeIntent(
            IntentType.GenerateDialogue,
            description: "Generate a tense conversation");

        // Assert
        intent.Type.Should().Be(IntentType.GenerateDialogue);
        intent.Description.Should().Be("Generate a tense conversation");
    }

    [Fact]
    public void Constructor_WithTargetCharacters_ShouldSetCharacters()
    {
        // Arrange
        var char1 = Id.New();
        var char2 = Id.New();

        // Act
        var intent = new NarrativeIntent(
            IntentType.GenerateDialogue,
            targetCharacterIds: new[] { char1, char2 });

        // Assert
        intent.TargetCharacterIds.Should().HaveCount(2);
        intent.TargetCharacterIds.Should().Contain(char1);
        intent.TargetCharacterIds.Should().Contain(char2);
    }

    [Fact]
    public void Constructor_WithTargetLocation_ShouldSetLocation()
    {
        // Arrange
        var locationId = Id.New();

        // Act
        var intent = new NarrativeIntent(
            IntentType.DescribeScene,
            targetLocationId: locationId);

        // Assert
        intent.TargetLocationId.Should().Be(locationId);
    }

    [Fact]
    public void Constructor_WithParameters_ShouldSetParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["tension"] = 0.8,
            ["style"] = "dramatic"
        };

        // Act
        var intent = new NarrativeIntent(
            IntentType.CreateTension,
            parameters: parameters);

        // Assert
        intent.Parameters.Should().HaveCount(2);
        intent.Parameters["tension"].Should().Be(0.8);
        intent.Parameters["style"].Should().Be("dramatic");
    }

    [Fact]
    public void Continue_ShouldCreateContinueIntent()
    {
        // Act
        var intent = NarrativeIntent.Continue("Keep the story going");

        // Assert
        intent.Type.Should().Be(IntentType.ContinueNarrative);
        intent.Description.Should().Be("Keep the story going");
    }

    [Fact]
    public void Dialogue_ShouldCreateDialogueIntent()
    {
        // Arrange
        var char1 = Id.New();
        var char2 = Id.New();
        var locationId = Id.New();

        // Act
        var intent = NarrativeIntent.Dialogue(new[] { char1, char2 }, locationId);

        // Assert
        intent.Type.Should().Be(IntentType.GenerateDialogue);
        intent.TargetCharacterIds.Should().HaveCount(2);
        intent.TargetLocationId.Should().Be(locationId);
    }

    [Fact]
    public void DescribeLocation_ShouldCreateDescribeIntent()
    {
        // Arrange
        var locationId = Id.New();

        // Act
        var intent = NarrativeIntent.DescribeLocation(locationId, "A dark forest");

        // Assert
        intent.Type.Should().Be(IntentType.DescribeScene);
        intent.TargetLocationId.Should().Be(locationId);
        intent.Description.Should().Be("A dark forest");
    }

    [Theory]
    [InlineData(IntentType.ContinueNarrative)]
    [InlineData(IntentType.IntroduceEvent)]
    [InlineData(IntentType.GenerateDialogue)]
    [InlineData(IntentType.DescribeScene)]
    [InlineData(IntentType.Summarize)]
    [InlineData(IntentType.CreateTension)]
    [InlineData(IntentType.ResolveConflict)]
    public void AllIntentTypes_ShouldBeCreatable(IntentType type)
    {
        // Act
        var intent = new NarrativeIntent(type);

        // Assert
        intent.Type.Should().Be(type);
    }

    [Fact]
    public void TargetCharacterIds_ShouldBeImmutable()
    {
        // Arrange
        var charIds = new List<Id> { Id.New() };
        var intent = new NarrativeIntent(IntentType.GenerateDialogue, targetCharacterIds: charIds);

        // Act - Try to modify the original list
        charIds.Add(Id.New());

        // Assert - Intent should not be affected
        intent.TargetCharacterIds.Should().HaveCount(1);
    }
}
