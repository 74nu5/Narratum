using FluentAssertions;
using Narratum.Core;
using Narratum.Domain.Events;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Prompts;
using Narratum.State;
using Xunit;

namespace Narratum.Orchestration.Tests.Prompts;

public class PromptOptimizationServiceTests
{
    private readonly PromptOptimizationService _service;

    public PromptOptimizationServiceTests()
    {
        _service = new PromptOptimizationService();
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithMinimalState_ReturnsValidPrompt()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Explore the forest");

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(state, intent);

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("Test World");
        prompt.Should().Contain("Explore the forest");
        prompt.Should().Contain("CONTEXT:");
        prompt.Should().Contain("GUIDELINES:");
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithCharacters_IncludesCharacterContext()
    {
        // Arrange
        var worldId = Id.New();
        var character1 = new CharacterState(Id.New(), "Alice");
        var character2 = new CharacterState(Id.New(), "Bob");
        var state = StoryState.Create(worldId, "Test World")
            .WithCharacters(character1, character2);
        var intent = NarrativeIntent.Continue("Characters meet");

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(state, intent);

        // Assert
        prompt.Should().Contain("CHARACTERS PRESENT:");
        prompt.Should().Contain("Alice");
        prompt.Should().Contain("Bob");
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithGenreAndTone_IncludesStyleGuidance()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Test");

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(
            state, intent,
            genre: "Science Fiction",
            tone: "Dark and mysterious");

        // Assert
        prompt.Should().Contain("Science Fiction");
        prompt.Should().Contain("Dark and mysterious");
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithPreviousNarrative_IncludesContinuityContext()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Test");
        var previousNarrative = "The hero walked through the dark forest...";

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(
            state, intent, previousNarrative);

        // Assert
        prompt.Should().Contain("PREVIOUS NARRATIVE");
        prompt.Should().Contain("dark forest");
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithLongPreviousNarrative_TruncatesCorrectly()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Test");
        var longNarrative = new string('a', 500);

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(
            state, intent, longNarrative);

        // Assert - Should contain last 200 chars + ellipsis
        prompt.Should().Contain("...");
    }

    [Fact]
    public void BuildOptimizedCharacterPrompt_WithCharacter_ReturnsValidPrompt()
    {
        // Arrange
        var worldId = Id.New();
        var character = new CharacterState(Id.New(), "Alice");
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Alice speaks");

        // Act
        var prompt = _service.BuildOptimizedCharacterPrompt(character, state, intent);

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("Alice");
        prompt.Should().Contain("CHARACTER PROFILE:");
        prompt.Should().Contain("DIALOGUE GUIDELINES:");
    }

    [Fact]
    public void BuildOptimizedCharacterPrompt_WithKnownFacts_IncludesFactsContext()
    {
        // Arrange
        var worldId = Id.New();
        var character = new CharacterState(Id.New(), "Alice")
            .WithKnownFact("The forest is dangerous")
            .WithKnownFact("Bob is her friend")
            .WithKnownFact("She carries a sword");
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Alice speaks");

        // Act
        var prompt = _service.BuildOptimizedCharacterPrompt(character, state, intent);

        // Assert
        prompt.Should().Contain("WHAT ALICE KNOWS:");
        prompt.Should().Contain("forest is dangerous");
        prompt.Should().Contain("Bob is her friend");
    }

    [Fact]
    public void BuildOptimizedCharacterPrompt_WithPersonality_IncludesPersonalityContext()
    {
        // Arrange
        var worldId = Id.New();
        var character = new CharacterState(Id.New(), "Alice");
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Test");

        // Act
        var prompt = _service.BuildOptimizedCharacterPrompt(
            character, state, intent,
            characterPersonality: "Brave and impulsive");

        // Assert
        prompt.Should().Contain("Brave and impulsive");
    }

    [Fact]
    public void BuildOptimizedSummaryPrompt_WithEvents_ReturnsValidPrompt()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");

        // Act
        var prompt = _service.BuildOptimizedSummaryPrompt(state);

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("Summarize");
        prompt.Should().Contain("Test World");
        prompt.Should().Contain("SUMMARY REQUIREMENTS:");
    }

    [Fact]
    public void BuildOptimizedConsistencyPrompt_WithFacts_ReturnsValidPrompt()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var narrative = "Alice walked through the forest.";
        var facts = new[] { "Alice is a knight", "The forest is magical" };

        // Act
        var prompt = _service.BuildOptimizedConsistencyPrompt(state, narrative, facts);

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("ESTABLISHED FACTS:");
        prompt.Should().Contain("Alice is a knight");
        prompt.Should().Contain("NARRATIVE TO CHECK:");
        prompt.Should().Contain("Alice walked through");
    }

    [Fact]
    public void BuildOptimizedConsistencyPrompt_WithManyFacts_LimitsToTen()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var narrative = "Test";
        var facts = Enumerable.Range(1, 20).Select(i => $"Fact {i}").ToArray();

        // Act
        var prompt = _service.BuildOptimizedConsistencyPrompt(state, narrative, facts);

        // Assert
        prompt.Should().Contain("Fact 1");
        prompt.Should().Contain("Fact 10");
        prompt.Should().NotContain("Fact 11"); // Should be truncated
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithTargetCharacters_IncludesOnlyRelevantCharacters()
    {
        // Arrange
        var worldId = Id.New();
        var aliceId = Id.New();
        var bobId = Id.New();
        var charlieId = Id.New();
        var alice = new CharacterState(aliceId, "Alice");
        var bob = new CharacterState(bobId, "Bob");
        var charlie = new CharacterState(charlieId, "Charlie");

        var state = StoryState.Create(worldId, "Test World")
            .WithCharacters(alice, bob, charlie);
        var intent = NarrativeIntent.Continue("Scene with Alice and Bob")
            .WithTargetCharacters(aliceId, bobId);

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(state, intent);

        // Assert
        prompt.Should().Contain("Alice");
        prompt.Should().Contain("Bob");
        prompt.Should().NotContain("Charlie"); // Not in target characters
    }

    [Fact]
    public void BuildOptimizedNarratorPrompt_WithNoCharacters_ReturnsValidPrompt()
    {
        // Arrange
        var worldId = Id.New();
        var state = StoryState.Create(worldId, "Test World");
        var intent = NarrativeIntent.Continue("Empty scene");

        // Act
        var prompt = _service.BuildOptimizedNarratorPrompt(state, intent);

        // Assert
        prompt.Should().Contain("No specific characters in focus");
    }
}
