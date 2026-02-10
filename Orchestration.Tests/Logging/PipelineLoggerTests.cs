using FluentAssertions;
using Narratum.Core;
using Narratum.Orchestration.Logging;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Logging;

/// <summary>
/// Tests pour PipelineLogger.
/// </summary>
public class PipelineLoggerTests
{
    [Fact]
    public void LogPipelineStart_ShouldRecordEvent()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var intent = NarrativeIntent.Continue("Test intent");

        // Act
        logger.LogPipelineStart(pipelineId, intent);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.PipelineStarted);
        events[0].PipelineId.Should().Be(pipelineId);
    }

    [Fact]
    public void LogPipelineComplete_ShouldRecordDuration()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        logger.LogPipelineComplete(pipelineId, duration);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.PipelineCompleted);
        events[0].Duration.Should().Be(duration);
    }

    [Fact]
    public void LogPipelineError_ShouldRecordException()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test error");

        // Act
        logger.LogPipelineError(pipelineId, exception);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.PipelineError);
        events[0].Description.Should().Contain("Test error");
        events[0].Data.Should().ContainKey("exception_type");
    }

    [Fact]
    public void LogStageStart_ShouldRecordStageName()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();

        // Act
        logger.LogStageStart(pipelineId, "ContextBuilder");

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.StageStarted);
        events[0].Data!["stage_name"].Should().Be("ContextBuilder");
    }

    [Fact]
    public void LogStageComplete_ShouldRecordDuration()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        logger.LogStageComplete(pipelineId, "PromptBuilder", duration);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.StageCompleted);
        events[0].Duration.Should().Be(duration);
    }

    [Fact]
    public void LogStageFailure_ShouldRecordError()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();

        // Act
        logger.LogStageFailure(pipelineId, "OutputValidator", "Validation failed");

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.StageError);
        events[0].Data!["error"].Should().Be("Validation failed");
    }

    [Fact]
    public void LogAgentCall_ShouldRecordPromptLength()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var prompt = "Test prompt with some content";

        // Act
        logger.LogAgentCall(pipelineId, AgentType.Narrator, prompt);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.AgentCalled);
        events[0].Data!["prompt_length"].Should().Be(prompt.Length);
    }

    [Fact]
    public void LogAgentResponse_ShouldRecordSuccess()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var response = AgentResponse.CreateSuccess(
            AgentType.Narrator,
            "Generated narrative content",
            TimeSpan.FromMilliseconds(200));

        // Act
        logger.LogAgentResponse(pipelineId, AgentType.Narrator, response);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.AgentResponded);
        events[0].Data!["success"].Should().Be(true);
        events[0].Data!["response_length"].Should().Be(response.Content.Length);
    }

    [Fact]
    public void LogAgentResponse_ShouldRecordFailure()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var response = AgentResponse.CreateFailure(
            AgentType.Narrator,
            "Agent failed",
            TimeSpan.FromMilliseconds(50));

        // Act
        logger.LogAgentResponse(pipelineId, AgentType.Narrator, response);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events[0].Data!["success"].Should().Be(false);
    }

    [Fact]
    public void LogRetry_ShouldRecordAttemptNumber()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        logger.LogRetry(pipelineId, 2, errors);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.RetryAttempted);
        events[0].Data!["attempt_number"].Should().Be(2);
    }

    [Fact]
    public void LogValidation_ShouldRecordResults()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var result = ValidationResult.Invalid("Test error");

        // Act
        logger.LogValidation(pipelineId, result);

        // Assert
        var events = logger.GetPipelineHistory(pipelineId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(PipelineEventType.ValidationPerformed);
        events[0].Data!["is_valid"].Should().Be(false);
        events[0].Data!["error_count"].Should().Be(1);
    }

    [Fact]
    public void GetPipelineHistory_ShouldReturnOnlyMatchingEvents()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId1 = Guid.NewGuid();
        var pipelineId2 = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();

        // Act
        logger.LogPipelineStart(pipelineId1, intent);
        logger.LogPipelineStart(pipelineId2, intent);
        logger.LogStageStart(pipelineId1, "Stage1");

        // Assert
        var history1 = logger.GetPipelineHistory(pipelineId1);
        var history2 = logger.GetPipelineHistory(pipelineId2);

        history1.Should().HaveCount(2);
        history2.Should().HaveCount(1);
    }

    [Fact]
    public void GetAllEvents_ShouldReturnAllEvents()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId1 = Guid.NewGuid();
        var pipelineId2 = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();

        // Act
        logger.LogPipelineStart(pipelineId1, intent);
        logger.LogPipelineStart(pipelineId2, intent);

        // Assert
        logger.GetAllEvents().Should().HaveCount(2);
    }

    [Fact]
    public void GetEventsByType_ShouldFilterCorrectly()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();

        // Act
        logger.LogPipelineStart(pipelineId, intent);
        logger.LogStageStart(pipelineId, "Stage1");
        logger.LogStageStart(pipelineId, "Stage2");

        // Assert
        var stageEvents = logger.GetEventsByType(PipelineEventType.StageStarted);
        stageEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEvents()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();
        logger.LogPipelineStart(pipelineId, intent);

        // Act
        logger.Clear();

        // Assert
        logger.GetAllEvents().Should().BeEmpty();
        logger.EventCount.Should().Be(0);
    }

    [Fact]
    public void MaxEventsToRetain_ShouldLimitEvents()
    {
        // Arrange
        var config = new PipelineLoggerConfig { MaxEventsToRetain = 5 };
        var logger = new PipelineLogger(config: config);
        var pipelineId = Guid.NewGuid();

        // Act
        for (int i = 0; i < 10; i++)
        {
            logger.LogStageStart(pipelineId, $"Stage{i}");
        }

        // Assert
        logger.EventCount.Should().Be(5);
        // Le plus ancien devrait être Stage5
        var events = logger.GetAllEvents();
        events[0].Data!["stage_name"].Should().Be("Stage5");
    }

    [Fact]
    public void GenerateReport_ShouldProduceValidReport()
    {
        // Arrange
        var logger = new PipelineLogger();
        var pipelineId = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();

        logger.LogPipelineStart(pipelineId, intent);
        logger.LogStageStart(pipelineId, "ContextBuilder");
        logger.LogStageComplete(pipelineId, "ContextBuilder", TimeSpan.FromMilliseconds(50));
        logger.LogPipelineComplete(pipelineId, TimeSpan.FromMilliseconds(100));

        // Act
        var report = logger.GenerateReport(pipelineId);

        // Assert
        report.Should().Contain("Pipeline Report");
        report.Should().Contain("ContextBuilder");
        report.Should().Contain("SUCCESS");
    }

    [Fact]
    public void NullPipelineLogger_ShouldNotThrow()
    {
        // Arrange
        var logger = NullPipelineLogger.Instance;
        var pipelineId = Guid.NewGuid();
        var intent = NarrativeIntent.Continue();

        // Act & Assert - should not throw
        logger.LogPipelineStart(pipelineId, intent);
        logger.LogStageStart(pipelineId, "Test");
        logger.LogPipelineComplete(pipelineId, TimeSpan.Zero);

        logger.GetPipelineHistory(pipelineId).Should().BeEmpty();
        logger.GetAllEvents().Should().BeEmpty();
    }
}

/// <summary>
/// Tests pour PipelineEvent.
/// </summary>
public class PipelineEventTests
{
    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var event1 = PipelineEvent.Create(Guid.NewGuid(), PipelineEventType.Debug, "Test 1");
        var event2 = PipelineEvent.Create(Guid.NewGuid(), PipelineEventType.Debug, "Test 2");

        // Assert
        event1.EventId.Should().NotBe(event2.EventId);
    }

    [Fact]
    public void PipelineStarted_ShouldContainIntentInfo()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var intent = new NarrativeIntent(IntentType.GenerateDialogue);

        // Act
        var evt = PipelineEvent.PipelineStarted(pipelineId, intent);

        // Assert
        evt.Type.Should().Be(PipelineEventType.PipelineStarted);
        evt.Data.Should().ContainKey("intent_type");
        evt.Data!["intent_type"].Should().Be("GenerateDialogue");
    }

    [Fact]
    public void AgentResponded_ShouldIncludeAllDetails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        var evt = PipelineEvent.AgentResponded(
            pipelineId,
            AgentType.Summary,
            success: true,
            responseLength: 500,
            duration);

        // Assert
        evt.Type.Should().Be(PipelineEventType.AgentResponded);
        evt.Duration.Should().Be(duration);
        evt.Data!["agent_type"].Should().Be("Summary");
        evt.Data!["success"].Should().Be(true);
        evt.Data!["response_length"].Should().Be(500);
    }
}

/// <summary>
/// Tests pour PipelineLoggerConfig.
/// </summary>
public class PipelineLoggerConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableValues()
    {
        // Act
        var config = PipelineLoggerConfig.Default;

        // Assert
        config.MaxEventsToRetain.Should().BeGreaterThan(0);
        config.MaxContentLengthInLogs.Should().BeGreaterThan(0);
        config.LogPromptContent.Should().BeFalse();
        config.LogResponseContent.Should().BeFalse();
    }

    [Fact]
    public void Verbose_ShouldEnableContentLogging()
    {
        // Act
        var config = PipelineLoggerConfig.Verbose;

        // Assert
        config.LogPromptContent.Should().BeTrue();
        config.LogResponseContent.Should().BeTrue();
        config.MaxContentLengthInLogs.Should().BeGreaterThan(PipelineLoggerConfig.Default.MaxContentLengthInLogs);
    }

    [Fact]
    public void Minimal_ShouldHaveLowerLimits()
    {
        // Act
        var config = PipelineLoggerConfig.Minimal;

        // Assert
        config.MaxEventsToRetain.Should().BeLessThan(PipelineLoggerConfig.Default.MaxEventsToRetain);
        config.MaxContentLengthInLogs.Should().BeLessThan(PipelineLoggerConfig.Default.MaxContentLengthInLogs);
    }
}
