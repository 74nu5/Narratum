using FluentAssertions;
using Narratum.Core;
using Narratum.Orchestration.Agents;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Prompts;
using Narratum.Orchestration.Prompts.Templates;
using Narratum.Orchestration.Stages;
using Narratum.State;
using Xunit;

namespace Narratum.Orchestration.Tests.Prompts;

/// <summary>
/// Tests pour IPromptTemplate et PromptTemplateBase.
/// </summary>
public class PromptTemplateBaseTests
{
    [Fact]
    public void GetVariables_ShouldIncludeBasicVariables()
    {
        // Arrange
        var template = new SummaryPromptTemplate();
        var context = CreateTestContext();

        // Act
        var variables = template.GetVariables(context);

        // Assert
        variables.Should().ContainKey("location_name");
        variables.Should().ContainKey("character_count");
        variables.Should().ContainKey("event_count");
        variables.Should().ContainKey("context_time");
        variables.Should().ContainKey("active_characters");
    }

    [Fact]
    public void CanHandle_WithSupportedIntent_ShouldReturnTrue()
    {
        // Arrange
        var template = new SummaryPromptTemplate();
        var intent = new NarrativeIntent(IntentType.Summarize);

        // Act
        var result = template.CanHandle(intent);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithUnsupportedIntent_ShouldReturnFalse()
    {
        // Arrange
        var template = new SummaryPromptTemplate();
        var intent = new NarrativeIntent(IntentType.GenerateDialogue);

        // Act
        var result = template.CanHandle(intent);

        // Assert
        result.Should().BeFalse();
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string> { "Alice is brave" }),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>())
        };
        var location = LocationContext.Create(Id.New(), "Test Location", "A test location");

        return new NarrativeContext(storyState, activeCharacters: characters, currentLocation: location);
    }
}

/// <summary>
/// Tests pour SummaryPromptTemplate.
/// </summary>
public class SummaryPromptTemplateTests
{
    private readonly SummaryPromptTemplate _template = new();

    [Fact]
    public void Name_ShouldBeSummaryPrompt()
    {
        _template.Name.Should().Be("SummaryPrompt");
    }

    [Fact]
    public void TargetAgent_ShouldBeSummary()
    {
        _template.TargetAgent.Should().Be(AgentType.Summary);
    }

    [Fact]
    public void SupportedIntents_ShouldContainSummarize()
    {
        _template.SupportedIntents.Should().Contain(IntentType.Summarize);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainRoleDescription()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var prompt = _template.BuildSystemPrompt(context);

        // Assert
        prompt.Should().Contain("narrative summarizer");
        prompt.Should().Contain("RULES");
        prompt.Should().Contain("FORMAT");
        prompt.Should().Contain("FORBIDDEN");
    }

    [Fact]
    public void BuildUserPrompt_ShouldIncludeEvents()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.Summarize);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("EVENTS:");
        prompt.Should().Contain("ACTIVE CHARACTERS:");
    }

    [Fact]
    public void BuildUserPrompt_WithLocation_ShouldIncludeLocation()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.Summarize);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("CURRENT LOCATION:");
        prompt.Should().Contain("Test Location");
    }

    [Fact]
    public void BuildUserPrompt_WithDescription_ShouldIncludeFocus()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.Summarize, description: "Focus on combat events");

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("SPECIFIC FOCUS:");
        prompt.Should().Contain("Focus on combat events");
    }

    [Fact]
    public void GetVariables_ShouldIncludeSummarySpecificVariables()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var variables = _template.GetVariables(context);

        // Assert
        variables.Should().ContainKey("summary_type");
        variables.Should().ContainKey("max_words");
        variables["summary_type"].Should().Be("chapter");
        variables["max_words"].Should().Be("500");
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>())
        };
        var location = LocationContext.Create(Id.New(), "Test Location", "A test location");

        return new NarrativeContext(storyState, activeCharacters: characters, currentLocation: location);
    }
}

/// <summary>
/// Tests pour NarratorPromptTemplate.
/// </summary>
public class NarratorPromptTemplateTests
{
    private readonly NarratorPromptTemplate _template = new();

    [Fact]
    public void Name_ShouldBeNarratorPrompt()
    {
        _template.Name.Should().Be("NarratorPrompt");
    }

    [Fact]
    public void TargetAgent_ShouldBeNarrator()
    {
        _template.TargetAgent.Should().Be(AgentType.Narrator);
    }

    [Fact]
    public void SupportedIntents_ShouldContainNarratorIntents()
    {
        _template.SupportedIntents.Should().Contain(IntentType.ContinueNarrative);
        _template.SupportedIntents.Should().Contain(IntentType.DescribeScene);
        _template.SupportedIntents.Should().Contain(IntentType.CreateTension);
        _template.SupportedIntents.Should().Contain(IntentType.ResolveConflict);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainNarratorGuidelines()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var prompt = _template.BuildSystemPrompt(context);

        // Assert
        prompt.Should().Contain("narrative writer");
        prompt.Should().Contain("Third person");
        prompt.Should().Contain("NEVER contradict");
        prompt.Should().Contain("NEVER kill characters");
    }

    [Theory]
    [InlineData(IntentType.ContinueNarrative, "Continue the narrative")]
    [InlineData(IntentType.DescribeScene, "Describe the following scene")]
    [InlineData(IntentType.CreateTension, "Create tension")]
    [InlineData(IntentType.ResolveConflict, "Resolve the conflict")]
    public void BuildUserPrompt_ShouldHaveIntentSpecificHeader(IntentType intentType, string expectedHeader)
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(intentType);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain(expectedHeader);
    }

    [Fact]
    public void BuildUserPrompt_WithTargetCharacter_ShouldIncludeFocus()
    {
        // Arrange
        var characterId = Id.New();
        var characters = new List<CharacterContext>
        {
            new CharacterContext(characterId, "Alice", VitalStatus.Alive, new HashSet<string>())
        };
        var storyState = StoryState.Create(Id.New(), "Test World");
        var context = new NarrativeContext(storyState, activeCharacters: characters);
        var intent = new NarrativeIntent(IntentType.ContinueNarrative, targetCharacterIds: new[] { characterId });

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("FOCUS ON:");
        prompt.Should().Contain("Alice");
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>())
        };
        var location = LocationContext.Create(Id.New(), "Castle", "A grand castle");

        return new NarrativeContext(storyState, activeCharacters: characters, currentLocation: location);
    }
}

/// <summary>
/// Tests pour CharacterPromptTemplate.
/// </summary>
public class CharacterPromptTemplateTests
{
    private readonly CharacterPromptTemplate _template = new();

    [Fact]
    public void Name_ShouldBeCharacterPrompt()
    {
        _template.Name.Should().Be("CharacterPrompt");
    }

    [Fact]
    public void TargetAgent_ShouldBeCharacter()
    {
        _template.TargetAgent.Should().Be(AgentType.Character);
    }

    [Fact]
    public void SupportedIntents_ShouldContainDialogueIntent()
    {
        _template.SupportedIntents.Should().Contain(IntentType.GenerateDialogue);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainDialogueGuidelines()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var prompt = _template.BuildSystemPrompt(context);

        // Assert
        prompt.Should().Contain("dialogue writer");
        prompt.Should().Contain("distinct voice");
        prompt.Should().Contain("quotation marks");
    }

    [Fact]
    public void BuildUserPrompt_ShouldIncludeCharacterDetails()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.GenerateDialogue);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("CHARACTERS IN SCENE:");
        prompt.Should().Contain("Alice");
        prompt.Should().Contain("Status:");
    }

    [Fact]
    public void BuildUserPrompt_WithTraits_ShouldIncludeTraits()
    {
        // Arrange
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>(), new HashSet<string> { "brave", "wise" })
        };
        var storyState = StoryState.Create(Id.New(), "Test World");
        var context = new NarrativeContext(storyState, activeCharacters: characters);
        var intent = new NarrativeIntent(IntentType.GenerateDialogue);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("Traits:");
        prompt.Should().Contain("brave");
    }

    [Fact]
    public void BuildUserPrompt_WithToneParameter_ShouldIncludeTone()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(
            IntentType.GenerateDialogue,
            parameters: new Dictionary<string, object> { { "tone", "hostile" } });

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("EMOTIONAL TONE:");
        prompt.Should().Contain("hostile");
    }

    [Fact]
    public void GetVariables_ShouldIncludeCharacterVariables()
    {
        // Arrange
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>(), new HashSet<string> { "brave" }),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Alive, new HashSet<string>(), new HashSet<string> { "cunning" })
        };
        var storyState = StoryState.Create(Id.New(), "Test World");
        var context = new NarrativeContext(storyState, activeCharacters: characters);

        // Act
        var variables = _template.GetVariables(context);

        // Assert
        variables.Should().ContainKey("character_1_name");
        variables.Should().ContainKey("character_2_name");
        variables["character_1_name"].Should().Be("Alice");
        variables["character_2_name"].Should().Be("Bob");
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>())
        };

        return new NarrativeContext(storyState, activeCharacters: characters);
    }
}

/// <summary>
/// Tests pour ConsistencyPromptTemplate.
/// </summary>
public class ConsistencyPromptTemplateTests
{
    private readonly ConsistencyPromptTemplate _template = new();

    [Fact]
    public void Name_ShouldBeConsistencyPrompt()
    {
        _template.Name.Should().Be("ConsistencyPrompt");
    }

    [Fact]
    public void TargetAgent_ShouldBeConsistency()
    {
        _template.TargetAgent.Should().Be(AgentType.Consistency);
    }

    [Fact]
    public void SupportedIntents_ShouldContainAllIntents()
    {
        // L'agent de cohérence peut vérifier tout type de sortie
        _template.SupportedIntents.Should().Contain(IntentType.ContinueNarrative);
        _template.SupportedIntents.Should().Contain(IntentType.GenerateDialogue);
        _template.SupportedIntents.Should().Contain(IntentType.Summarize);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainConsistencyGuidelines()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var prompt = _template.BuildSystemPrompt(context);

        // Assert
        prompt.Should().Contain("consistency checker");
        prompt.Should().Contain("Dead characters cannot act");
        prompt.Should().Contain("SEVERITY:");
        prompt.Should().Contain("CONSISTENT:");
    }

    [Fact]
    public void BuildUserPrompt_WithDescription_ShouldIncludeTextToVerify()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.ContinueNarrative, description: "Alice walked through the forest.");

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("TEXT TO VERIFY:");
        prompt.Should().Contain("Alice walked through the forest.");
    }

    [Fact]
    public void BuildUserPrompt_ShouldIncludeCharacterStates()
    {
        // Arrange
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.ContinueNarrative);

        // Act
        var prompt = _template.BuildUserPrompt(context, intent);

        // Assert
        prompt.Should().Contain("Character States:");
        prompt.Should().Contain("Alice:");
        prompt.Should().Contain("Alive");
    }

    [Fact]
    public void GetVariables_WithDeadCharacters_ShouldIncludeDeadCount()
    {
        // Arrange
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string>()),
            new CharacterContext(Id.New(), "Bob", VitalStatus.Dead, new HashSet<string>())
        };
        var storyState = StoryState.Create(Id.New(), "Test World");
        var context = new NarrativeContext(storyState, activeCharacters: characters);

        // Act
        var variables = _template.GetVariables(context);

        // Assert
        variables.Should().ContainKey("dead_character_count");
        variables["dead_character_count"].Should().Be("1");
        variables.Should().ContainKey("dead_characters");
        variables["dead_characters"].Should().Contain("Bob");
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(Id.New(), "Alice", VitalStatus.Alive, new HashSet<string> { "Alice is brave" })
        };
        var location = LocationContext.Create(Id.New(), "Forest", "A dark forest");

        return new NarrativeContext(storyState, activeCharacters: characters, currentLocation: location);
    }
}
