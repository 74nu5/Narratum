using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Stages;

/// <summary>
/// Tests unitaires pour StateIntegrator.
/// </summary>
public class StateIntegratorTests
{
    private readonly StoryState _testState;
    private readonly NarrativeContext _testContext;

    public StateIntegratorTests()
    {
        _testState = StoryState.Create(Id.New(), "Test World");
        _testContext = new NarrativeContext(_testState);
    }

    [Fact]
    public async Task IntegrateAsync_WithValidOutput_ShouldReturnNarrativeOutput()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "The hero ventured into the dark forest.",
                    TimeSpan.FromMilliseconds(100))
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.Should().NotBeNull();
        result.NarrativeText.Should().Contain("hero");
        result.NarrativeText.Should().Contain("dark forest");
    }

    [Fact]
    public async Task IntegrateAsync_WithNullOutput_ShouldThrow()
    {
        // Arrange
        var integrator = new StateIntegrator();

        // Act
        var action = async () => await integrator.IntegrateAsync(null!, _testContext);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IntegrateAsync_WithNullContext_ShouldThrow()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[] { AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.Zero) },
            TimeSpan.Zero);

        // Act
        var action = async () => await integrator.IntegrateAsync(rawOutput, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IntegrateAsync_ShouldCombineMultipleAgentResponses()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Narrator,
                    "The sun set over the mountains.",
                    TimeSpan.FromMilliseconds(50)),
                AgentResponse.CreateSuccess(
                    AgentType.Character,
                    "\"We should make camp here,\" said Alice.",
                    TimeSpan.FromMilliseconds(50))
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.NarrativeText.Should().Contain("sun set");
        result.NarrativeText.Should().Contain("Alice");
    }

    [Fact]
    public async Task IntegrateAsync_NarratorShouldHavePriorityOverCharacter()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Character, "Character speaks first.", TimeSpan.Zero),
                AgentResponse.CreateSuccess(AgentType.Narrator, "Narrator comes second.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        var narratorIndex = result.NarrativeText.IndexOf("Narrator");
        var characterIndex = result.NarrativeText.IndexOf("Character");
        narratorIndex.Should().BeLessThan(characterIndex); // Narrator first in output
    }

    [Fact]
    public async Task IntegrateAsync_WithNoContent_ShouldReturnPlaceholder()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateFailure(AgentType.Narrator, "Failed", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.NarrativeText.Should().Contain("No narrative content");
    }

    [Fact]
    public async Task IntegrateAsync_ShouldGenerateEvents()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Some narrative content here.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.ExtractedEvents.Should().NotBeEmpty();
        result.ExtractedEvents.OfType<GeneratedEvent>()
            .Should().Contain(ge => ge.EventType == "NarrativeGenerated");
    }

    [Fact]
    public async Task IntegrateAsync_WithDialogue_ShouldGenerateDialogueEvent()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Character, "\"Hello there!\" said Bob.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.ExtractedEvents.OfType<GeneratedEvent>()
            .Should().Contain(ge => ge.EventType == "DialogueGenerated");
    }

    [Fact]
    public async Task IntegrateAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.FromMilliseconds(100))
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.Metadata.Should().ContainKey("sourceAgents");
        result.Metadata.Should().ContainKey("totalDuration");
        result.Metadata.Should().ContainKey("eventCount");
        result.Metadata.Should().ContainKey("stateChangeCount");
    }

    [Fact]
    public async Task IntegrateAsync_ShouldIncludeStateChanges()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "Content", TimeSpan.FromMilliseconds(500))
            },
            TimeSpan.FromMilliseconds(500));

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        // StateChanges includes TimeAdvanced
        result.Metadata["stateChangeCount"].Should().Be(1);
    }

    [Fact]
    public async Task IntegrateAsync_WithEmptyStringContent_ShouldSkipAgent()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "", TimeSpan.Zero),
                AgentResponse.CreateSuccess(AgentType.Summary, "Valid summary content here.", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.NarrativeText.Should().Contain("Valid summary");
        result.NarrativeText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task IntegrateAsync_ShouldRecordSourceAgents()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(AgentType.Narrator, "A", TimeSpan.Zero),
                AgentResponse.CreateSuccess(AgentType.Character, "B", TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        var sourceAgents = result.Metadata["sourceAgents"] as IEnumerable<string>;
        sourceAgents.Should().NotBeNull();
        sourceAgents.Should().Contain("Narrator");
        sourceAgents.Should().Contain("Character");
    }

    [Fact]
    public async Task IntegrateAsync_WithOnlySummary_ShouldIncludeSummaryContent()
    {
        // Arrange
        var integrator = new StateIntegrator();
        var rawOutput = RawOutput.Create(
            new[]
            {
                AgentResponse.CreateSuccess(
                    AgentType.Summary,
                    "In the previous chapter, the heroes found a magical artifact.",
                    TimeSpan.Zero)
            },
            TimeSpan.Zero);

        // Act
        var result = await integrator.IntegrateAsync(rawOutput, _testContext);

        // Assert
        result.NarrativeText.Should().Contain("magical artifact");
    }
}

/// <summary>
/// Tests pour GeneratedEvent.
/// </summary>
public class GeneratedEventTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Act
        var evt = GeneratedEvent.Create("TestEvent", "Something happened");

        // Assert
        evt.EventId.Should().NotBeNull();
        evt.EventType.Should().Be("TestEvent");
        evt.Description.Should().Be("Something happened");
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_ShouldPreserveAllValues()
    {
        // Arrange
        var id = Id.New();
        var timestamp = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var evt = new GeneratedEvent(id, "Custom", "Custom event", timestamp);

        // Assert
        evt.EventId.Should().Be(id);
        evt.EventType.Should().Be("Custom");
        evt.Description.Should().Be("Custom event");
        evt.Timestamp.Should().Be(timestamp);
    }
}
