using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;
using Xunit;

namespace Narratum.Orchestration.Tests.Models;

/// <summary>
/// Tests unitaires pour PipelineResult et types associés.
/// </summary>
public class PipelineResultTests
{
    private readonly PipelineContext _testContext;

    public PipelineResultTests()
    {
        var state = StoryState.Create(worldId: Id.New(), worldName: "Test");
        var intent = NarrativeIntent.Continue();
        _testContext = PipelineContext.CreateMinimal(state, intent);
    }

    [Fact]
    public void NarrativeOutput_Constructor_ShouldCreateValidOutput()
    {
        // Act
        var output = new NarrativeOutput("This is narrative text.");

        // Assert
        output.OutputId.Should().NotBe(default(Id));
        output.NarrativeText.Should().Be("This is narrative text.");
        output.GeneratedMemorandum.Should().BeNull();
        output.ExtractedEvents.Should().BeEmpty();
        output.Metadata.Should().BeEmpty();
        output.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void NarrativeOutput_Constructor_WithNullText_ShouldThrow()
    {
        // Act
        var action = () => new NarrativeOutput(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NarrativeOutput_CreateMock_ShouldCreateMockOutput()
    {
        // Act
        var output = NarrativeOutput.CreateMock("Mock text");

        // Assert
        output.NarrativeText.Should().Be("Mock text");
        output.Metadata.Should().ContainKey("mock");
        output.Metadata["mock"].Should().Be(true);
    }

    [Fact]
    public void PipelineStageResult_Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = PipelineStageResult.Success(
            "TestStage",
            TimeSpan.FromMilliseconds(100));

        // Assert
        result.StageName.Should().Be("TestStage");
        result.Status.Should().Be(PipelineStageStatus.Completed);
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(100));
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PipelineStageResult_Success_WithData_ShouldIncludeData()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = PipelineStageResult.Success("TestStage", TimeSpan.Zero, data);

        // Assert
        result.OutputData.Should().ContainKey("key");
    }

    [Fact]
    public void PipelineStageResult_Failure_ShouldCreateFailureResult()
    {
        // Act
        var result = PipelineStageResult.Failure(
            "TestStage",
            TimeSpan.FromMilliseconds(50),
            "Something went wrong");

        // Assert
        result.StageName.Should().Be("TestStage");
        result.Status.Should().Be(PipelineStageStatus.Failed);
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(50));
        result.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void PipelineStageResult_Skipped_ShouldCreateSkippedResult()
    {
        // Act
        var result = PipelineStageResult.Skipped("OptionalStage");

        // Assert
        result.StageName.Should().Be("OptionalStage");
        result.Status.Should().Be(PipelineStageStatus.Skipped);
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void PipelineResult_Success_ShouldCreateSuccessResult()
    {
        // Arrange
        var output = new NarrativeOutput("Generated text");
        var stages = new[]
        {
            PipelineStageResult.Success("Stage1", TimeSpan.FromMilliseconds(10)),
            PipelineStageResult.Success("Stage2", TimeSpan.FromMilliseconds(20))
        };

        // Act
        var result = PipelineResult.Success(
            _testContext,
            output,
            stages,
            TimeSpan.FromMilliseconds(30));

        // Assert
        result.ExecutionId.Should().NotBe(default(Id));
        result.InputContext.Should().Be(_testContext);
        result.Output.Should().Be(output);
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.StageResults.Should().HaveCount(2);
        result.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(30));
        result.RetryCount.Should().Be(0);
    }

    [Fact]
    public void PipelineResult_Success_WithRetries_ShouldRecordRetryCount()
    {
        // Arrange
        var output = new NarrativeOutput("Text");

        // Act
        var result = PipelineResult.Success(
            _testContext,
            output,
            Array.Empty<PipelineStageResult>(),
            TimeSpan.Zero,
            retryCount: 2);

        // Assert
        result.RetryCount.Should().Be(2);
    }

    [Fact]
    public void PipelineResult_Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var stages = new[]
        {
            PipelineStageResult.Success("Stage1", TimeSpan.FromMilliseconds(10)),
            PipelineStageResult.Failure("Stage2", TimeSpan.FromMilliseconds(5), "Failed")
        };

        // Act
        var result = PipelineResult.Failure(
            _testContext,
            "Pipeline failed at Stage2",
            stages,
            TimeSpan.FromMilliseconds(15));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Output.Should().BeNull();
        result.ErrorMessage.Should().Be("Pipeline failed at Stage2");
        result.StageResults.Should().HaveCount(2);
    }

    [Fact]
    public void PipelineResult_Timestamps_ShouldBeConsistent()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = PipelineResult.Success(
            _testContext,
            new NarrativeOutput("Text"),
            Array.Empty<PipelineStageResult>(),
            duration);

        // Assert
        result.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.StartedAt.Should().Be(result.CompletedAt.Subtract(duration));
    }

    [Theory]
    [InlineData(PipelineStageStatus.Pending)]
    [InlineData(PipelineStageStatus.Running)]
    [InlineData(PipelineStageStatus.Completed)]
    [InlineData(PipelineStageStatus.Failed)]
    [InlineData(PipelineStageStatus.Skipped)]
    public void AllStageStatuses_ShouldBeUsable(PipelineStageStatus status)
    {
        // Act
        var result = new PipelineStageResult("Test", status, TimeSpan.Zero);

        // Assert
        result.Status.Should().Be(status);
    }
}
