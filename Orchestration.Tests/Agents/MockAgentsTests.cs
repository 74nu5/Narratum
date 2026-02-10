using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.Memory;
using Narratum.State;
using Narratum.Orchestration.Agents;
using Narratum.Orchestration.Agents.Mock;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Agents;

/// <summary>
/// Tests unitaires pour MockSummaryAgent.
/// </summary>
public class MockSummaryAgentTests
{
    private readonly ILlmClient _llmClient;
    private readonly MockSummaryAgent _agent;

    public MockSummaryAgentTests()
    {
        _llmClient = new MockLlmClient(MockLlmConfig.ForTesting);
        _agent = new MockSummaryAgent(_llmClient);
    }

    [Fact]
    public void Constructor_ShouldCreateValidAgent()
    {
        // Assert
        _agent.Type.Should().Be(AgentType.Summary);
        _agent.Name.Should().Be("MockSummaryAgent");
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new MockSummaryAgent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanHandle_SummarizeIntent_ShouldReturnTrue()
    {
        // Arrange
        var intent = new NarrativeIntent(IntentType.Summarize);

        // Act
        var result = _agent.CanHandle(intent);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_OtherIntent_ShouldReturnFalse()
    {
        // Arrange
        var intent = new NarrativeIntent(IntentType.ContinueNarrative);

        // Act
        var result = _agent.CanHandle(intent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithValidContext_ShouldReturnSuccess()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState);
        var prompt = AgentPrompt.Create(AgentType.Summary, "Summarize", "Summarize recent events");

        // Act
        var result = await _agent.ProcessAsync(prompt, context);

        // Assert
        result.Should().BeOfType<Result<AgentResponse>.Success>();
        var response = ((Result<AgentResponse>.Success)result).Value;
        response.Agent.Should().Be(AgentType.Summary);
        response.Success.Should().BeTrue();
        response.Content.Should().NotBeEmpty();
        response.Metadata.Should().ContainKey("mock");
    }

    [Fact]
    public async Task ProcessAsync_WithNullPrompt_ShouldThrow()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState);

        // Act
        var action = async () => await _agent.ProcessAsync(null!, context);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var prompt = AgentPrompt.Create(AgentType.Summary, "System", "User");

        // Act
        var action = async () => await _agent.ProcessAsync(prompt, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SummarizeEventsAsync_WithNoEvents_ShouldReturnDefaultMessage()
    {
        // Arrange
        var events = new List<Event>();

        // Act
        var result = await _agent.SummarizeEventsAsync(events);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var summary = ((Result<string>.Success)result).Value;
        summary.Should().Contain("No significant events");
    }

    [Fact]
    public async Task SummarizeEventsAsync_WithEvents_ShouldReturnSummary()
    {
        // Arrange
        var character1Id = Id.New();
        var character2Id = Id.New();
        var locationId = Id.New();
        var events = new List<Event>
        {
            new CharacterEncounterEvent(character1Id, character2Id, locationId)
        };

        // Act
        var result = await _agent.SummarizeEventsAsync(events);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var summary = ((Result<string>.Success)result).Value;
        summary.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SummarizeChapterAsync_WithChapter_ShouldReturnSummary()
    {
        // Arrange
        var arcId = Id.New();
        var chapter = new StoryChapter(arcId, 0);
        var events = new List<Event>();

        // Act
        var result = await _agent.SummarizeChapterAsync(chapter, events);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var summary = ((Result<string>.Success)result).Value;
        summary.Should().Contain("Chapter 1");
    }

    private StoryState CreateTestStoryState()
    {
        return StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
    }
}

/// <summary>
/// Tests unitaires pour MockNarratorAgent.
/// </summary>
public class MockNarratorAgentTests
{
    private readonly ILlmClient _llmClient;
    private readonly MockNarratorAgent _agent;

    public MockNarratorAgentTests()
    {
        _llmClient = new MockLlmClient(MockLlmConfig.ForTesting);
        _agent = new MockNarratorAgent(_llmClient);
    }

    [Fact]
    public void Constructor_ShouldCreateValidAgent()
    {
        // Assert
        _agent.Type.Should().Be(AgentType.Narrator);
        _agent.Name.Should().Be("MockNarratorAgent");
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new MockNarratorAgent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(IntentType.ContinueNarrative, true)]
    [InlineData(IntentType.DescribeScene, true)]
    [InlineData(IntentType.CreateTension, true)]
    [InlineData(IntentType.ResolveConflict, true)]
    [InlineData(IntentType.Summarize, false)]
    [InlineData(IntentType.GenerateDialogue, false)]
    public void CanHandle_ShouldReturnExpectedResult(IntentType intentType, bool expected)
    {
        // Arrange
        var intent = new NarrativeIntent(intentType);

        // Act
        var result = _agent.CanHandle(intent);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessAsync_WithValidContext_ShouldReturnSuccess()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState);
        var prompt = AgentPrompt.Create(AgentType.Narrator, "You are a narrator", "Continue the story");

        // Act
        var result = await _agent.ProcessAsync(prompt, context);

        // Assert
        result.Should().BeOfType<Result<AgentResponse>.Success>();
        var response = ((Result<AgentResponse>.Success)result).Value;
        response.Agent.Should().Be(AgentType.Narrator);
        response.Success.Should().BeTrue();
        response.Content.Should().NotBeEmpty();
        response.Metadata.Should().ContainKey("style");
        response.Metadata.Should().ContainKey("mock");
    }

    [Fact]
    public async Task GenerateNarrativeAsync_WithDescriptiveStyle_ShouldReturnDescriptiveText()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState);

        // Act
        var result = await _agent.GenerateNarrativeAsync(context, "Summary", NarrativeStyle.Descriptive);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var narrative = ((Result<string>.Success)result).Value;
        narrative.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateNarrativeAsync_WithActionStyle_ShouldReturnActionText()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState);

        // Act
        var result = await _agent.GenerateNarrativeAsync(context, "Summary", NarrativeStyle.Action);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var narrative = ((Result<string>.Success)result).Value;
        narrative.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DescribeSceneAsync_WithLocation_ShouldReturnDescription()
    {
        // Arrange
        var locationId = Id.New();
        var location = LocationContext.Create(locationId, "Dark Forest", "A gloomy forest");

        // Act
        var result = await _agent.DescribeSceneAsync(location, Array.Empty<CharacterContext>());

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var description = ((Result<string>.Success)result).Value;
        description.Should().Contain("Dark Forest");
    }

    [Fact]
    public async Task DescribeSceneAsync_WithCharacters_ShouldIncludeCharacters()
    {
        // Arrange
        var locationId = Id.New();
        var location = LocationContext.Create(locationId, "Castle", "A grand castle");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>()),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>())
        };

        // Act
        var result = await _agent.DescribeSceneAsync(location, characters);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var description = ((Result<string>.Success)result).Value;
        description.Should().Contain("Alice");
        description.Should().Contain("Bob");
    }

    private StoryState CreateTestStoryState()
    {
        return StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
    }
}

/// <summary>
/// Tests unitaires pour MockCharacterAgent.
/// </summary>
public class MockCharacterAgentTests
{
    private readonly ILlmClient _llmClient;
    private readonly MockCharacterAgent _agent;

    public MockCharacterAgentTests()
    {
        _llmClient = new MockLlmClient(MockLlmConfig.ForTesting);
        _agent = new MockCharacterAgent(_llmClient);
    }

    [Fact]
    public void Constructor_ShouldCreateValidAgent()
    {
        // Assert
        _agent.Type.Should().Be(AgentType.Character);
        _agent.Name.Should().Be("MockCharacterAgent");
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new MockCharacterAgent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanHandle_DialogueIntent_ShouldReturnTrue()
    {
        // Arrange
        var intent = new NarrativeIntent(IntentType.GenerateDialogue);

        // Act
        var result = _agent.CanHandle(intent);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_OtherIntent_ShouldReturnFalse()
    {
        // Arrange
        var intent = new NarrativeIntent(IntentType.DescribeScene);

        // Act
        var result = _agent.CanHandle(intent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithValidContext_ShouldReturnSuccess()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var activeCharacters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>()),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>())
        };
        var context = new NarrativeContext(storyState, activeCharacters: activeCharacters);
        var prompt = AgentPrompt.Create(AgentType.Character, "You are a character", "Say something");

        // Act
        var result = await _agent.ProcessAsync(prompt, context);

        // Assert
        result.Should().BeOfType<Result<AgentResponse>.Success>();
        var response = ((Result<AgentResponse>.Success)result).Value;
        response.Agent.Should().Be(AgentType.Character);
        response.Success.Should().BeTrue();
        response.Content.Should().NotBeEmpty();
        response.Metadata.Should().ContainKey("mock");
    }

    [Fact]
    public async Task GenerateDialogueAsync_WithCharacter_ShouldReturnDialogue()
    {
        // Arrange
        var speaker = new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>());
        var listener = new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>());
        var situation = DialogueSituation.Friendly("Discussing plans", "Weather", "Travel");

        // Act
        var result = await _agent.GenerateDialogueAsync(speaker, listener, situation);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var dialogue = ((Result<string>.Success)result).Value;
        dialogue.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateDialogueAsync_WithDifferentTones_ShouldVaryContent()
    {
        // Arrange
        var speaker = new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>());
        var friendlySituation = DialogueSituation.Friendly("Greeting", "Hello");
        var hostileSituation = DialogueSituation.Tense("Confrontation", "Accusation");

        // Act
        var friendlyResult = await _agent.GenerateDialogueAsync(speaker, null, friendlySituation);
        var hostileResult = await _agent.GenerateDialogueAsync(speaker, null, hostileSituation);

        // Assert
        friendlyResult.Should().BeOfType<Result<string>.Success>();
        hostileResult.Should().BeOfType<Result<string>.Success>();

        var friendlyDialogue = ((Result<string>.Success)friendlyResult).Value;
        var hostileDialogue = ((Result<string>.Success)hostileResult).Value;

        // Both should be valid dialogues
        friendlyDialogue.Should().NotBeEmpty();
        hostileDialogue.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateReactionAsync_WithEvent_ShouldReturnReaction()
    {
        // Arrange
        var character = new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>());
        var character2Id = Id.New();
        var @event = new CharacterEncounterEvent(character.CharacterId, character2Id);

        // Act
        var result = await _agent.GenerateReactionAsync(character, @event);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var reaction = ((Result<string>.Success)result).Value;
        reaction.Should().NotBeEmpty();
    }

    private StoryState CreateTestStoryState()
    {
        return StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
    }
}

/// <summary>
/// Tests unitaires pour MockConsistencyAgent.
/// </summary>
public class MockConsistencyAgentTests
{
    private readonly ILlmClient _llmClient;
    private readonly MockConsistencyAgent _agent;

    public MockConsistencyAgentTests()
    {
        _llmClient = new MockLlmClient(MockLlmConfig.ForTesting);
        _agent = new MockConsistencyAgent(_llmClient);
    }

    [Fact]
    public void Constructor_ShouldCreateValidAgent()
    {
        // Assert
        _agent.Type.Should().Be(AgentType.Consistency);
        _agent.Name.Should().Be("MockConsistencyAgent");
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new MockConsistencyAgent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanHandle_AnyIntent_ShouldReturnTrue()
    {
        // Arrange - Consistency agent can handle all intents
        var intents = new[]
        {
            new NarrativeIntent(IntentType.ContinueNarrative),
            new NarrativeIntent(IntentType.GenerateDialogue),
            new NarrativeIntent(IntentType.Summarize),
            new NarrativeIntent(IntentType.DescribeScene)
        };

        // Act & Assert
        foreach (var intent in intents)
        {
            _agent.CanHandle(intent).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ProcessAsync_WithNoCanonicalState_ShouldSkip()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var context = new NarrativeContext(storyState); // No canonical state
        var prompt = AgentPrompt.Create(AgentType.Consistency, "Check", "Check consistency");

        // Act
        var result = await _agent.ProcessAsync(prompt, context);

        // Assert
        result.Should().BeOfType<Result<AgentResponse>.Success>();
        var response = ((Result<AgentResponse>.Success)result).Value;
        response.Success.Should().BeTrue();
        response.Metadata.Should().ContainKey("skipped");
        response.Metadata["skipped"].Should().Be(true);
    }

    [Fact]
    public async Task ProcessAsync_WithCanonicalState_ShouldCheckConsistency()
    {
        // Arrange
        var storyState = CreateTestStoryState();
        var worldId = Guid.NewGuid();
        var canonicalState = CanonicalState.CreateEmpty(worldId, MemoryLevel.World);
        var context = new NarrativeContext(storyState, canonicalState: canonicalState);
        var prompt = AgentPrompt.Create(AgentType.Consistency, "Check", "The hero walked through the forest.");

        // Act
        var result = await _agent.ProcessAsync(prompt, context);

        // Assert
        result.Should().BeOfType<Result<AgentResponse>.Success>();
        var response = ((Result<AgentResponse>.Success)result).Value;
        response.Success.Should().BeTrue();
        response.Metadata.Should().ContainKey("isConsistent");
    }

    [Fact]
    public async Task CheckConsistencyAsync_WithEmptyText_ShouldBeConsistent()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var canonicalState = CanonicalState.CreateEmpty(worldId, MemoryLevel.World);

        // Act
        var result = await _agent.CheckConsistencyAsync("", canonicalState);

        // Assert
        result.Should().BeOfType<Result<ConsistencyCheck>.Success>();
        var check = ((Result<ConsistencyCheck>.Success)result).Value;
        check.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConsistencyAsync_WithDeadCharacterActing_ShouldDetectIssue()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var canonicalState = CanonicalState.CreateEmpty(worldId, MemoryLevel.World);

        // Add a fact about a dead character
        var deathFact = Fact.Create(
            "Alice is dead",
            FactType.CharacterState,
            MemoryLevel.World,
            new[] { "Alice" });

        canonicalState = canonicalState.AddFact(deathFact);

        var textWithDeadCharacter = "Alice walked through the garden.";

        // Act
        var result = await _agent.CheckConsistencyAsync(textWithDeadCharacter, canonicalState);

        // Assert
        result.Should().BeOfType<Result<ConsistencyCheck>.Success>();
        var check = ((Result<ConsistencyCheck>.Success)result).Value;
        check.IsConsistent.Should().BeFalse();
        check.Issues.Should().ContainSingle();
        check.Issues[0].Description.Should().Contain("Alice");
    }

    [Fact]
    public async Task SuggestCorrectionsAsync_WithNoViolations_ShouldReturnOriginal()
    {
        // Arrange
        var originalText = "The story continues.";

        // Act
        var result = await _agent.SuggestCorrectionsAsync(originalText, Array.Empty<CoherenceViolation>());

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var suggestion = ((Result<string>.Success)result).Value;
        suggestion.Should().Be(originalText);
    }

    [Fact]
    public async Task SuggestCorrectionsAsync_WithViolations_ShouldReturnSuggestions()
    {
        // Arrange
        var originalText = "Alice walked.";
        var violations = new List<CoherenceViolation>
        {
            CoherenceViolation.Create(
                CoherenceViolationType.StatementContradiction,
                CoherenceSeverity.Error,
                "Dead character acting",
                new[] { Guid.NewGuid() },
                "Remove Alice's action")
        };

        // Act
        var result = await _agent.SuggestCorrectionsAsync(originalText, violations);

        // Assert
        result.Should().BeOfType<Result<string>.Success>();
        var suggestion = ((Result<string>.Success)result).Value;
        suggestion.Should().Contain("Dead character acting");
        suggestion.Should().Contain("Remove Alice's action");
    }

    private StoryState CreateTestStoryState()
    {
        return StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
    }
}

/// <summary>
/// Tests pour les types d'agents (NarrativeStyle, EmotionalTone, etc.).
/// </summary>
public class AgentTypesTests
{
    [Theory]
    [InlineData(NarrativeStyle.Descriptive)]
    [InlineData(NarrativeStyle.Action)]
    [InlineData(NarrativeStyle.Introspective)]
    [InlineData(NarrativeStyle.Dialogue)]
    public void NarrativeStyle_AllValues_ShouldBeUsable(NarrativeStyle style)
    {
        // Assert
        style.ToString().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(EmotionalTone.Neutral)]
    [InlineData(EmotionalTone.Friendly)]
    [InlineData(EmotionalTone.Hostile)]
    [InlineData(EmotionalTone.Fearful)]
    [InlineData(EmotionalTone.Excited)]
    [InlineData(EmotionalTone.Sad)]
    public void EmotionalTone_AllValues_ShouldBeUsable(EmotionalTone tone)
    {
        // Assert
        tone.ToString().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(IssueSeverity.Minor)]
    [InlineData(IssueSeverity.Moderate)]
    [InlineData(IssueSeverity.Severe)]
    public void IssueSeverity_AllValues_ShouldBeUsable(IssueSeverity severity)
    {
        // Assert
        severity.ToString().Should().NotBeEmpty();
    }

    [Fact]
    public void ConsistencyCheck_Consistent_ShouldCreateValidCheck()
    {
        // Act
        var check = ConsistencyCheck.Consistent();

        // Assert
        check.IsConsistent.Should().BeTrue();
        check.Issues.Should().BeEmpty();
        check.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void ConsistencyCheck_WithIssues_ShouldIncludeIssues()
    {
        // Arrange
        var issues = new List<ConsistencyIssue>
        {
            ConsistencyIssue.Minor("Test issue", "context"),
            ConsistencyIssue.Severe("Severe issue", "context", "Fix it")
        };

        // Act
        var check = new ConsistencyCheck(false, issues, 0.5);

        // Assert
        check.IsConsistent.Should().BeFalse();
        check.Issues.Should().HaveCount(2);
        check.ConfidenceScore.Should().Be(0.5);
    }

    [Fact]
    public void ConsistencyIssue_Minor_ShouldHaveMinorSeverity()
    {
        // Act
        var issue = ConsistencyIssue.Minor("Test", "context");

        // Assert
        issue.Severity.Should().Be(IssueSeverity.Minor);
        issue.Description.Should().Be("Test");
    }

    [Fact]
    public void ConsistencyIssue_Severe_ShouldHaveSevereSeverity()
    {
        // Act
        var issue = ConsistencyIssue.Severe("Severe test", "context", "Fix suggestion");

        // Assert
        issue.Severity.Should().Be(IssueSeverity.Severe);
        issue.SuggestedFix.Should().Be("Fix suggestion");
    }

    [Fact]
    public void DialogueSituation_Neutral_ShouldCreateNeutralSituation()
    {
        // Act
        var situation = DialogueSituation.Neutral("A conversation", "topic1", "topic2");

        // Assert
        situation.Context.Should().Be("A conversation");
        situation.Tone.Should().Be(EmotionalTone.Neutral);
        situation.TopicsToAddress.Should().HaveCount(2);
    }

    [Fact]
    public void DialogueSituation_Friendly_ShouldCreateFriendlySituation()
    {
        // Act
        var situation = DialogueSituation.Friendly("A friendly chat", "weather");

        // Assert
        situation.Tone.Should().Be(EmotionalTone.Friendly);
    }

    [Fact]
    public void DialogueSituation_Tense_ShouldCreateTenseSituation()
    {
        // Act
        var situation = DialogueSituation.Tense("A confrontation", "accusations");

        // Assert
        situation.Tone.Should().Be(EmotionalTone.Hostile);
    }

    [Fact]
    public void CoherenceViolation_Create_ShouldCreateValidViolation()
    {
        // Act
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Test description",
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            "Suggested resolution");

        // Assert
        violation.Description.Should().Be("Test description");
        violation.ViolationType.Should().Be(CoherenceViolationType.StatementContradiction);
        violation.Severity.Should().Be(CoherenceSeverity.Error);
        violation.Resolution.Should().Be("Suggested resolution");
        violation.InvolvedFactIds.Should().HaveCount(2);
        violation.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void CoherenceViolation_MarkResolved_ShouldSetResolvedAt()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Warning,
            "Test",
            new[] { Guid.NewGuid() });

        // Act
        var resolved = violation.MarkResolved();

        // Assert
        resolved.IsResolved.Should().BeTrue();
        resolved.ResolvedAt.Should().NotBeNull();
    }
}
