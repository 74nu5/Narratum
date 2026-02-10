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
/// Tests pour PromptRegistry.
/// </summary>
public class PromptRegistryTests
{
    [Fact]
    public void Register_ShouldStoreTemplate()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template = new SummaryPromptTemplate();

        // Act
        registry.Register(template, IntentType.Summarize);

        // Assert
        var retrieved = registry.GetTemplate(AgentType.Summary, IntentType.Summarize);
        retrieved.Should().Be(template);
    }

    [Fact]
    public void Register_WithNullTemplate_ShouldThrow()
    {
        // Arrange
        var registry = new PromptRegistry();

        // Act
        var action = () => registry.Register(null!, IntentType.Summarize);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterDefault_ShouldStoreAsDefault()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template = new SummaryPromptTemplate();

        // Act
        registry.RegisterDefault(template);

        // Assert
        var retrieved = registry.GetDefaultTemplate(AgentType.Summary);
        retrieved.Should().Be(template);
    }

    [Fact]
    public void RegisterDefault_ShouldRegisterForAllSupportedIntents()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template = new NarratorPromptTemplate();

        // Act
        registry.RegisterDefault(template);

        // Assert - Should be available for all supported intents
        registry.GetTemplate(AgentType.Narrator, IntentType.ContinueNarrative).Should().Be(template);
        registry.GetTemplate(AgentType.Narrator, IntentType.DescribeScene).Should().Be(template);
        registry.GetTemplate(AgentType.Narrator, IntentType.CreateTension).Should().Be(template);
        registry.GetTemplate(AgentType.Narrator, IntentType.ResolveConflict).Should().Be(template);
    }

    [Fact]
    public void GetTemplate_WithSpecificRegistration_ShouldReturnSpecific()
    {
        // Arrange
        var registry = new PromptRegistry();
        var defaultTemplate = new NarratorPromptTemplate();
        var specificTemplate = new NarratorPromptTemplate(); // Different instance

        registry.RegisterDefault(defaultTemplate);
        registry.Register(specificTemplate, IntentType.DescribeScene);

        // Act
        var result = registry.GetTemplate(AgentType.Narrator, IntentType.DescribeScene);

        // Assert
        result.Should().Be(specificTemplate);
        result.Should().NotBe(defaultTemplate);
    }

    [Fact]
    public void GetTemplate_WithNoRegistration_ShouldReturnNull()
    {
        // Arrange
        var registry = new PromptRegistry();

        // Act
        var result = registry.GetTemplate(AgentType.Summary, IntentType.Summarize);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetTemplate_WithOnlyDefault_ShouldFallbackToDefault()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template = new SummaryPromptTemplate();
        registry.RegisterDefault(template);

        // Act - Query for an unsupported intent
        var result = registry.GetTemplate(AgentType.Summary, IntentType.ContinueNarrative);

        // Assert - Should fallback to default
        result.Should().Be(template);
    }

    [Fact]
    public void GetAllTemplates_ShouldReturnAllDistinct()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template1 = new SummaryPromptTemplate();
        var template2 = new NarratorPromptTemplate();

        registry.RegisterDefault(template1);
        registry.RegisterDefault(template2);

        // Act
        var all = registry.GetAllTemplates();

        // Assert
        all.Should().HaveCount(2);
        all.Should().Contain(template1);
        all.Should().Contain(template2);
    }

    [Fact]
    public void GetTemplatesForAgent_ShouldReturnOnlyForAgent()
    {
        // Arrange
        var registry = new PromptRegistry();
        var summaryTemplate = new SummaryPromptTemplate();
        var narratorTemplate = new NarratorPromptTemplate();

        registry.RegisterDefault(summaryTemplate);
        registry.RegisterDefault(narratorTemplate);

        // Act
        var summaryTemplates = registry.GetTemplatesForAgent(AgentType.Summary);
        var narratorTemplates = registry.GetTemplatesForAgent(AgentType.Narrator);

        // Assert
        summaryTemplates.Should().ContainSingle().Which.Should().Be(summaryTemplate);
        narratorTemplates.Should().ContainSingle().Which.Should().Be(narratorTemplate);
    }

    [Fact]
    public void HasTemplate_WithRegistration_ShouldReturnTrue()
    {
        // Arrange
        var registry = new PromptRegistry();
        registry.RegisterDefault(new SummaryPromptTemplate());

        // Act & Assert
        registry.HasTemplate(AgentType.Summary, IntentType.Summarize).Should().BeTrue();
    }

    [Fact]
    public void HasTemplate_WithNoRegistration_ShouldReturnFalse()
    {
        // Arrange
        var registry = new PromptRegistry();

        // Act & Assert
        registry.HasTemplate(AgentType.Summary, IntentType.Summarize).Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllTemplates()
    {
        // Arrange
        var registry = new PromptRegistry();
        registry.RegisterDefault(new SummaryPromptTemplate());
        registry.RegisterDefault(new NarratorPromptTemplate());

        // Act
        registry.Clear();

        // Assert
        registry.Count.Should().Be(0);
        registry.GetAllTemplates().Should().BeEmpty();
    }

    [Fact]
    public void Count_ShouldReturnDistinctTemplateCount()
    {
        // Arrange
        var registry = new PromptRegistry();
        var template = new NarratorPromptTemplate();

        // RegisterDefault enregistre pour tous les intents supportés
        registry.RegisterDefault(template);

        // Act & Assert - Should count as 1 distinct template, not 4
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void CreateWithDefaults_ShouldRegisterAllDefaultTemplates()
    {
        // Act
        var registry = PromptRegistry.CreateWithDefaults();

        // Assert
        registry.Count.Should().Be(4); // Summary, Narrator, Character, Consistency

        registry.GetDefaultTemplate(AgentType.Summary).Should().NotBeNull();
        registry.GetDefaultTemplate(AgentType.Narrator).Should().NotBeNull();
        registry.GetDefaultTemplate(AgentType.Character).Should().NotBeNull();
        registry.GetDefaultTemplate(AgentType.Consistency).Should().NotBeNull();
    }

    [Fact]
    public void CreateWithDefaults_ShouldProvideTemplatesForCommonIntents()
    {
        // Act
        var registry = PromptRegistry.CreateWithDefaults();

        // Assert - Should have templates for common scenarios
        registry.HasTemplate(AgentType.Summary, IntentType.Summarize).Should().BeTrue();
        registry.HasTemplate(AgentType.Narrator, IntentType.ContinueNarrative).Should().BeTrue();
        registry.HasTemplate(AgentType.Narrator, IntentType.DescribeScene).Should().BeTrue();
        registry.HasTemplate(AgentType.Character, IntentType.GenerateDialogue).Should().BeTrue();
        registry.HasTemplate(AgentType.Consistency, IntentType.ContinueNarrative).Should().BeTrue();
    }

    [Fact]
    public void Registry_ShouldBeThreadSafe()
    {
        // Arrange
        var registry = new PromptRegistry();
        var exceptions = new List<Exception>();

        // Act - Concurrent registration and retrieval
        Parallel.For(0, 100, i =>
        {
            try
            {
                if (i % 2 == 0)
                {
                    registry.RegisterDefault(new SummaryPromptTemplate());
                }
                else
                {
                    _ = registry.GetTemplate(AgentType.Summary, IntentType.Summarize);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });

        // Assert
        exceptions.Should().BeEmpty();
    }
}

/// <summary>
/// Tests d'intégration pour le système de prompts complet.
/// </summary>
public class PromptSystemIntegrationTests
{
    [Fact]
    public void AllTemplates_ShouldGenerateValidPrompts()
    {
        // Arrange
        var registry = PromptRegistry.CreateWithDefaults();
        var context = CreateTestContext();

        // Act & Assert - All templates should generate non-empty prompts
        foreach (var template in registry.GetAllTemplates())
        {
            var systemPrompt = template.BuildSystemPrompt(context);
            systemPrompt.Should().NotBeNullOrEmpty($"SystemPrompt for {template.Name} should not be empty");

            foreach (var intentType in template.SupportedIntents)
            {
                var intent = new NarrativeIntent(intentType);
                var userPrompt = template.BuildUserPrompt(context, intent);
                userPrompt.Should().NotBeNullOrEmpty($"UserPrompt for {template.Name}/{intentType} should not be empty");
            }
        }
    }

    [Fact]
    public void AllTemplates_ShouldProvideVariables()
    {
        // Arrange
        var registry = PromptRegistry.CreateWithDefaults();
        var context = CreateTestContext();

        // Act & Assert
        foreach (var template in registry.GetAllTemplates())
        {
            var variables = template.GetVariables(context);
            variables.Should().NotBeEmpty($"Variables for {template.Name} should not be empty");
            variables.Should().ContainKey("location_name");
        }
    }

    [Fact]
    public void PromptBuilder_CanUseRegistryTemplates()
    {
        // Arrange
        var registry = PromptRegistry.CreateWithDefaults();
        var context = CreateTestContext();
        var intent = new NarrativeIntent(IntentType.ContinueNarrative);

        // Act
        var template = registry.GetTemplate(AgentType.Narrator, intent.Type);

        // Assert
        template.Should().NotBeNull();
        var systemPrompt = template!.BuildSystemPrompt(context);
        var userPrompt = template.BuildUserPrompt(context, intent);

        systemPrompt.Should().Contain("narrative writer");
        userPrompt.Should().Contain("Continue the narrative");
    }

    private NarrativeContext CreateTestContext()
    {
        var storyState = StoryState.Create(Id.New(), "Test World");
        var characters = new List<CharacterContext>
        {
            new CharacterContext(
                Id.New(),
                "Alice",
                VitalStatus.Alive,
                new HashSet<string> { "Alice is brave", "Alice knows the secret" },
                new HashSet<string> { "brave", "curious" },
                "determined"),
            new CharacterContext(
                Id.New(),
                "Bob",
                VitalStatus.Alive,
                new HashSet<string>(),
                new HashSet<string> { "wise" })
        };
        var location = LocationContext.Create(Id.New(), "Ancient Tower", "A mysterious tower rising into the clouds");

        return new NarrativeContext(
            storyState,
            activeCharacters: characters,
            currentLocation: location,
            recentSummary: "Alice and Bob entered the tower seeking the ancient artifact.");
    }
}
