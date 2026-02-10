using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour PromptBuilder.
/// </summary>
public class PromptBuilderTests
{
    private readonly StoryState _testState;
    private readonly NarrativeContext _testContext;

    public PromptBuilderTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testContext = new NarrativeContext(_testState);
    }

    [Fact]
    public async Task BuildAsync_WithContinueIntent_ShouldCreateNarratorPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = NarrativeIntent.Continue("Continue the story naturally");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.HasPromptFor(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_WithDialogueIntent_ShouldCreateCharacterPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var charId = Id.New();
        var locId = Id.New();
        var intent = NarrativeIntent.Dialogue(new[] { charId }, locId);

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.HasPromptFor(AgentType.Character).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_WithDescribeSceneIntent_ShouldCreateNarratorPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var locationId = Id.New();
        var intent = NarrativeIntent.DescribeLocation(locationId, "Describe the mysterious cave");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.HasPromptFor(AgentType.Narrator).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_WithSummarizeIntent_ShouldCreateSummaryPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = new NarrativeIntent(IntentType.Summarize, "Summarize chapter 1");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.HasPromptFor(AgentType.Summary).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_ShouldIncludeContextInUserPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var characterContext = new CharacterContext(
            Id.New(), "Alice", VitalStatus.Alive,
            new HashSet<string> { "Is brave", "Knows magic" });
        var context = new NarrativeContext(_testState, activeCharacters: new[] { characterContext });
        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(context, intent);

        // Assert
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        var narratorPrompt = promptSet.GetPromptFor(AgentType.Narrator);
        narratorPrompt!.UserPrompt.Should().Contain("Alice");
    }

    [Fact]
    public async Task BuildAsync_WithLocation_ShouldIncludeLocationInPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var location = LocationContext.Create(Id.New(), "Dark Forest", "A mysterious forest");
        var context = new NarrativeContext(_testState, currentLocation: location);
        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(context, intent);

        // Assert
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        var narratorPrompt = promptSet.GetPromptFor(AgentType.Narrator);
        narratorPrompt!.UserPrompt.Should().Contain("Dark Forest");
    }

    [Fact]
    public async Task BuildAsync_WithRecentSummary_ShouldIncludeSummary()
    {
        // Arrange
        var builder = new PromptBuilder();
        var context = new NarrativeContext(_testState, recentSummary: "Previously, our heroes discovered a hidden treasure.");
        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(context, intent);

        // Assert
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        var narratorPrompt = promptSet.GetPromptFor(AgentType.Narrator);
        narratorPrompt!.UserPrompt.Should().Contain("hidden treasure");
    }

    [Fact]
    public async Task BuildAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var builder = new PromptBuilder();

        // Act
        var action = async () => await builder.BuildAsync(null!, NarrativeIntent.Continue());

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_WithNullIntent_ShouldThrow()
    {
        // Arrange
        var builder = new PromptBuilder();

        // Act
        var action = async () => await builder.BuildAsync(_testContext, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_ShouldSetCorrectPriorities()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = NarrativeIntent.Continue();

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        var narratorPrompt = promptSet.GetPromptFor(AgentType.Narrator);
        narratorPrompt!.Priority.Should().Be(PromptPriority.Required);
    }

    [Fact]
    public async Task BuildAsync_WithIntentDescription_ShouldIncludeInPrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = NarrativeIntent.Continue("Make the scene dramatic and tense");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        var narratorPrompt = promptSet.GetPromptFor(AgentType.Narrator);
        narratorPrompt!.UserPrompt.Should().Contain("dramatic");
    }

    [Fact]
    public async Task BuildAsync_WithTensionIntent_ShouldCreateAppropriatePrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = new NarrativeIntent(IntentType.CreateTension, "Build suspense");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.Prompts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BuildAsync_WithResolveConflictIntent_ShouldCreateAppropriatePrompt()
    {
        // Arrange
        var builder = new PromptBuilder();
        var intent = new NarrativeIntent(IntentType.ResolveConflict, "Resolve the battle");

        // Act
        var result = await builder.BuildAsync(_testContext, intent);

        // Assert
        result.Should().BeOfType<Result<PromptSet>.Success>();
        var promptSet = ((Result<PromptSet>.Success)result).Value;
        promptSet.Prompts.Should().NotBeEmpty();
    }
}
