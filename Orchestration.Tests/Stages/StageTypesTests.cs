using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour les types de base du pipeline.
/// </summary>
public class StageTypesTests
{
    #region CharacterContext Tests

    [Fact]
    public void CharacterContext_FromCharacterState_ShouldMapCorrectly()
    {
        // Arrange
        var characterId = Id.New();
        var locationId = Id.New();
        var state = new CharacterState(characterId, "Alice", VitalStatus.Alive, locationId)
            .WithKnownFact("Is a princess")
            .WithKnownFact("Has magic powers");

        // Act
        var context = CharacterContext.FromCharacterState(state);

        // Assert
        context.CharacterId.Should().Be(characterId);
        context.Name.Should().Be("Alice");
        context.Status.Should().Be(VitalStatus.Alive);
        context.CurrentLocationId.Should().Be(locationId);
        context.KnownFacts.Should().Contain("Is a princess");
        context.KnownFacts.Should().Contain("Has magic powers");
    }

    [Fact]
    public void CharacterContext_FromDeadCharacter_ShouldPreserveStatus()
    {
        // Arrange
        var state = new CharacterState(Id.New(), "Bob", VitalStatus.Dead);

        // Act
        var context = CharacterContext.FromCharacterState(state);

        // Assert
        context.Status.Should().Be(VitalStatus.Dead);
    }

    #endregion

    #region LocationContext Tests

    [Fact]
    public void LocationContext_Create_ShouldInitializeEmpty()
    {
        // Arrange
        var locationId = Id.New();

        // Act
        var context = LocationContext.Create(locationId, "Castle", "A grand medieval castle.");

        // Assert
        context.LocationId.Should().Be(locationId);
        context.Name.Should().Be("Castle");
        context.Description.Should().Be("A grand medieval castle.");
        context.PresentCharacterIds.Should().BeEmpty();
    }

    [Fact]
    public void LocationContext_WithCharacter_ShouldAddCharacterId()
    {
        // Arrange
        var locationId = Id.New();
        var characterId = Id.New();
        var context = LocationContext.Create(locationId, "Forest", "A dark forest.");

        // Act
        var updated = context.WithCharacter(characterId);

        // Assert
        updated.PresentCharacterIds.Should().Contain(characterId);
        context.PresentCharacterIds.Should().BeEmpty(); // Original unchanged
    }

    #endregion

    #region NarrativeContext Tests

    [Fact]
    public void NarrativeContext_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "Test World");

        // Act
        var context = new NarrativeContext(state);

        // Assert
        context.State.Should().Be(state);
        context.ContextId.Should().NotBeNull();
        context.RecentMemoria.Should().BeEmpty();
        context.ActiveCharacters.Should().BeEmpty();
        context.RecentEvents.Should().BeEmpty();
        context.CurrentLocation.Should().BeNull();
        context.CanonicalState.Should().BeNull();
        context.RecentSummary.Should().BeNull();
    }

    [Fact]
    public void NarrativeContext_CreateMinimal_ShouldWork()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "Minimal World");

        // Act
        var context = NarrativeContext.CreateMinimal(state);

        // Assert
        context.State.Should().Be(state);
        context.ContextBuiltAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void NarrativeContext_WithActiveCharacters_ShouldPreserveThem()
    {
        // Arrange
        var state = StoryState.Create(Id.New(), "Test World");
        var characters = new[]
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>()),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>())
        };

        // Act
        var context = new NarrativeContext(state, activeCharacters: characters);

        // Assert
        context.ActiveCharacters.Should().HaveCount(2);
        context.ActiveCharacters.Select(c => c.Name).Should().Contain("Alice", "Bob");
    }

    #endregion

    #region AgentPrompt Tests

    [Fact]
    public void AgentPrompt_Create_ShouldInitializeCorrectly()
    {
        // Act
        var prompt = AgentPrompt.Create(
            AgentType.Narrator,
            "You are a narrator.",
            "Continue the story.");

        // Assert
        prompt.TargetAgent.Should().Be(AgentType.Narrator);
        prompt.SystemPrompt.Should().Be("You are a narrator.");
        prompt.UserPrompt.Should().Be("Continue the story.");
        prompt.Variables.Should().BeEmpty();
        prompt.Priority.Should().Be(PromptPriority.Required);
    }

    [Fact]
    public void AgentPrompt_WithVariable_ShouldAddVariable()
    {
        // Arrange
        var prompt = AgentPrompt.Create(AgentType.Character, "System", "User");

        // Act
        var updated = prompt.WithVariable("characterName", "Alice");

        // Assert
        updated.Variables.Should().ContainKey("characterName");
        updated.Variables["characterName"].Should().Be("Alice");
        prompt.Variables.Should().BeEmpty(); // Original unchanged
    }

    #endregion

    #region PromptSet Tests

    [Fact]
    public void PromptSet_Single_ShouldCreateSinglePromptSet()
    {
        // Arrange
        var prompt = AgentPrompt.Create(AgentType.Summary, "System", "User");

        // Act
        var set = PromptSet.Single(prompt);

        // Assert
        set.Prompts.Should().HaveCount(1);
        set.Order.Should().Be(ExecutionOrder.Sequential);
    }

    [Fact]
    public void PromptSet_Parallel_ShouldHaveParallelOrder()
    {
        // Arrange
        var prompts = new[]
        {
            AgentPrompt.Create(AgentType.Narrator, "S1", "U1"),
            AgentPrompt.Create(AgentType.Character, "S2", "U2")
        };

        // Act
        var set = PromptSet.Parallel(prompts);

        // Assert
        set.Order.Should().Be(ExecutionOrder.Parallel);
        set.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public void PromptSet_HasPromptFor_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = PromptSet.Sequential(
            AgentPrompt.Create(AgentType.Narrator, "S", "U"));

        // Act & Assert
        set.HasPromptFor(AgentType.Narrator).Should().BeTrue();
        set.HasPromptFor(AgentType.Character).Should().BeFalse();
    }

    [Fact]
    public void PromptSet_GetPromptFor_ShouldReturnCorrectPrompt()
    {
        // Arrange
        var set = PromptSet.Sequential(
            AgentPrompt.Create(AgentType.Narrator, "NarratorSystem", "NarratorUser"),
            AgentPrompt.Create(AgentType.Summary, "SummarySystem", "SummaryUser"));

        // Act
        var narrator = set.GetPromptFor(AgentType.Narrator);
        var character = set.GetPromptFor(AgentType.Character);

        // Assert
        narrator.Should().NotBeNull();
        narrator!.SystemPrompt.Should().Be("NarratorSystem");
        character.Should().BeNull();
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_CreateSuccess_ShouldSetCorrectValues()
    {
        // Act
        var response = AgentResponse.CreateSuccess(
            AgentType.Narrator,
            "Generated content",
            TimeSpan.FromMilliseconds(500));

        // Assert
        response.Agent.Should().Be(AgentType.Narrator);
        response.Content.Should().Be("Generated content");
        response.Success.Should().BeTrue();
        response.ErrorMessage.Should().BeNull();
        response.Duration.TotalMilliseconds.Should().Be(500);
    }

    [Fact]
    public void AgentResponse_CreateFailure_ShouldSetCorrectValues()
    {
        // Act
        var response = AgentResponse.CreateFailure(
            AgentType.Character,
            "LLM timeout",
            TimeSpan.FromSeconds(30));

        // Assert
        response.Agent.Should().Be(AgentType.Character);
        response.Content.Should().BeEmpty();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("LLM timeout");
    }

    [Fact]
    public void AgentResponse_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var response = AgentResponse.CreateSuccess(AgentType.Summary, "Content", TimeSpan.Zero);

        // Act
        var updated = response.WithMetadata("tokens", 150);

        // Assert
        updated.Metadata.Should().ContainKey("tokens");
        updated.Metadata["tokens"].Should().Be(150);
    }

    #endregion

    #region RawOutput Tests

    [Fact]
    public void RawOutput_Create_ShouldGroupByAgentType()
    {
        // Arrange
        var responses = new[]
        {
            AgentResponse.CreateSuccess(AgentType.Narrator, "Narrative", TimeSpan.FromMilliseconds(100)),
            AgentResponse.CreateSuccess(AgentType.Summary, "Summary", TimeSpan.FromMilliseconds(50))
        };

        // Act
        var output = RawOutput.Create(responses, TimeSpan.FromMilliseconds(200));

        // Assert
        output.Responses.Should().HaveCount(2);
        output.GetContent(AgentType.Narrator).Should().Be("Narrative");
        output.GetContent(AgentType.Summary).Should().Be("Summary");
        output.TotalDuration.TotalMilliseconds.Should().Be(200);
    }

    [Fact]
    public void RawOutput_AllSuccessful_ShouldReturnCorrectValue()
    {
        // Arrange
        var successfulResponses = new[]
        {
            AgentResponse.CreateSuccess(AgentType.Narrator, "A", TimeSpan.Zero),
            AgentResponse.CreateSuccess(AgentType.Summary, "B", TimeSpan.Zero)
        };
        var mixedResponses = new[]
        {
            AgentResponse.CreateSuccess(AgentType.Narrator, "A", TimeSpan.Zero),
            AgentResponse.CreateFailure(AgentType.Summary, "Error", TimeSpan.Zero)
        };

        // Act
        var successOutput = RawOutput.Create(successfulResponses, TimeSpan.Zero);
        var mixedOutput = RawOutput.Create(mixedResponses, TimeSpan.Zero);

        // Assert
        successOutput.AllSuccessful.Should().BeTrue();
        mixedOutput.AllSuccessful.Should().BeFalse();
    }

    [Fact]
    public void RawOutput_HasSuccessfulResponse_ShouldWork()
    {
        // Arrange
        var responses = new[]
        {
            AgentResponse.CreateSuccess(AgentType.Narrator, "A", TimeSpan.Zero),
            AgentResponse.CreateFailure(AgentType.Summary, "Error", TimeSpan.Zero)
        };
        var output = RawOutput.Create(responses, TimeSpan.Zero);

        // Act & Assert
        output.HasSuccessfulResponse(AgentType.Narrator).Should().BeTrue();
        output.HasSuccessfulResponse(AgentType.Summary).Should().BeFalse();
        output.HasSuccessfulResponse(AgentType.Character).Should().BeFalse();
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Valid_ShouldBeValid()
    {
        // Act
        var result = ValidationResult.Valid();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_Invalid_ShouldContainErrors()
    {
        // Act
        var result = ValidationResult.Invalid("Something went wrong");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.ErrorMessages.Should().Contain("Something went wrong");
    }

    [Fact]
    public void ValidationResult_HasCriticalErrors_ShouldDetectCritical()
    {
        // Arrange
        var withCritical = ValidationResult.Invalid(ValidationError.Critical("Critical issue"));
        var withMajor = ValidationResult.Invalid(ValidationError.Major("Major issue"));

        // Act & Assert
        withCritical.HasCriticalErrors.Should().BeTrue();
        withMajor.HasCriticalErrors.Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_WithWarnings_ShouldBeValidButHaveWarnings()
    {
        // Act
        var result = ValidationResult.WithWarnings(
            new ValidationWarning("Consider adding more detail"));

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
    }

    #endregion

    #region StateChange Tests

    [Fact]
    public void StateChange_CharacterMoved_ShouldCreateCorrectly()
    {
        // Arrange
        var charId = Id.New();
        var fromLoc = Id.New();
        var toLoc = Id.New();

        // Act
        var change = StateChange.CharacterMoved(charId, fromLoc, toLoc);

        // Assert
        change.Type.Should().Be(StateChangeType.CharacterMoved);
        change.EntityId.Should().Be(charId);
        change.OldValue.Should().Be(fromLoc);
        change.NewValue.Should().Be(toLoc);
    }

    [Fact]
    public void StateChange_FactRevealed_ShouldCreateCorrectly()
    {
        // Arrange
        var charId = Id.New();

        // Act
        var change = StateChange.FactRevealed(charId, "The princess is a dragon");

        // Assert
        change.Type.Should().Be(StateChangeType.FactRevealed);
        change.EntityId.Should().Be(charId);
        change.Description.Should().Be("The princess is a dragon");
    }

    #endregion
}
