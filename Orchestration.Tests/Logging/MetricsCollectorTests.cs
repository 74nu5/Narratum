using FluentAssertions;
using Narratum.Orchestration.Logging;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Logging;

/// <summary>
/// Tests pour MetricsCollector.
/// </summary>
public class MetricsCollectorTests
{
    [Fact]
    public void RecordDataPoint_ShouldStoreData()
    {
        // Arrange
        var collector = new MetricsCollector();
        var dataPoint = MetricDataPoint.Counter("test.counter", 1);

        // Act
        collector.RecordDataPoint(dataPoint);

        // Assert
        collector.DataPointCount.Should().Be(1);
        collector.GetDataPoints().Should().ContainSingle();
    }

    [Fact]
    public void IncrementCounter_ShouldAddCounterPoint()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.IncrementCounter("test.counter");
        collector.IncrementCounter("test.counter");

        // Assert
        var points = collector.GetDataPoints("test.counter");
        points.Should().HaveCount(2);
        points.All(p => p.Type == MetricType.Counter).Should().BeTrue();
    }

    [Fact]
    public void RecordDuration_ShouldStoreDurationInMs()
    {
        // Arrange
        var collector = new MetricsCollector();
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        collector.RecordDuration("test.duration", duration);

        // Assert
        var points = collector.GetDataPoints("test.duration");
        points.Should().ContainSingle();
        points[0].Value.Should().Be(150);
        points[0].Type.Should().Be(MetricType.Duration);
    }

    [Fact]
    public void RecordGauge_ShouldStoreValue()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.RecordGauge("memory.usage", 75.5);

        // Assert
        var points = collector.GetDataPoints("memory.usage");
        points.Should().ContainSingle();
        points[0].Value.Should().Be(75.5);
        points[0].Type.Should().Be(MetricType.Gauge);
    }

    [Fact]
    public void Measure_ShouldRecordDuration()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        var result = collector.Measure("test.operation", () =>
        {
            Thread.Sleep(50);
            return 42;
        });

        // Assert
        result.Should().Be(42);
        var points = collector.GetDataPoints("test.operation");
        points.Should().ContainSingle();
        points[0].Value.Should().BeGreaterOrEqualTo(40); // Allow some tolerance
    }

    [Fact]
    public async Task MeasureAsync_ShouldRecordDuration()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        var result = await collector.MeasureAsync("test.async.operation", async () =>
        {
            await Task.Delay(50);
            return "result";
        });

        // Assert
        result.Should().Be("result");
        var points = collector.GetDataPoints("test.async.operation");
        points.Should().ContainSingle();
        points[0].Value.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public void StartPipeline_EndPipeline_ShouldTrackDuration()
    {
        // Arrange
        var collector = new MetricsCollector();
        var pipelineId = Guid.NewGuid();

        // Act
        collector.StartPipeline(pipelineId);
        Thread.Sleep(50);
        var summary = collector.EndPipeline(pipelineId, success: true);

        // Assert
        summary.PipelineId.Should().Be(pipelineId);
        summary.Success.Should().BeTrue();
        summary.TotalDuration.TotalMilliseconds.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public void EndPipeline_WithoutStart_ShouldThrow()
    {
        // Arrange
        var collector = new MetricsCollector();
        var pipelineId = Guid.NewGuid();

        // Act
        var action = () => collector.EndPipeline(pipelineId, success: true);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartStage_EndStage_ShouldTrackDuration()
    {
        // Arrange
        var collector = new MetricsCollector();
        var pipelineId = Guid.NewGuid();
        collector.StartPipeline(pipelineId);

        // Act
        collector.StartStage(pipelineId, "ContextBuilder");
        Thread.Sleep(30);
        collector.EndStage(pipelineId, "ContextBuilder");
        var summary = collector.EndPipeline(pipelineId, success: true);

        // Assert
        summary.StageDurations.Should().ContainKey("ContextBuilder");
        summary.StageDurations["ContextBuilder"].TotalMilliseconds.Should().BeGreaterOrEqualTo(20);
    }

    [Fact]
    public void RecordAgentCall_ShouldTrackAgentMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        var pipelineId = Guid.NewGuid();
        collector.StartPipeline(pipelineId);

        // Act
        collector.RecordAgentCall(pipelineId, AgentType.Narrator, TimeSpan.FromMilliseconds(100), true);
        collector.RecordAgentCall(pipelineId, AgentType.Summary, TimeSpan.FromMilliseconds(50), true);
        var summary = collector.EndPipeline(pipelineId, success: true);

        // Assert
        summary.TotalAgentCalls.Should().Be(2);
        summary.AgentDurations.Should().ContainKey(AgentType.Narrator);
        summary.AgentDurations.Should().ContainKey(AgentType.Summary);
    }

    [Fact]
    public void RecordRetry_ShouldIncrementRetryCount()
    {
        // Arrange
        var collector = new MetricsCollector();
        var pipelineId = Guid.NewGuid();
        collector.StartPipeline(pipelineId);

        // Act
        collector.RecordRetry(pipelineId, 1);
        collector.RecordRetry(pipelineId, 2);
        var summary = collector.EndPipeline(pipelineId, success: true);

        // Assert
        summary.RetryCount.Should().Be(2);
    }

    [Fact]
    public void GetStatistics_ShouldCalculateCorrectly()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.RecordDuration("test.metric", TimeSpan.FromMilliseconds(100));
        collector.RecordDuration("test.metric", TimeSpan.FromMilliseconds(200));
        collector.RecordDuration("test.metric", TimeSpan.FromMilliseconds(300));

        // Act
        var stats = collector.GetStatistics("test.metric");

        // Assert
        stats.Count.Should().Be(3);
        stats.Min.Should().Be(100);
        stats.Max.Should().Be(300);
        stats.Average.Should().Be(200);
    }

    [Fact]
    public void GetAllStatistics_ShouldGroupByMetricName()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.RecordDuration("metric.a", TimeSpan.FromMilliseconds(100));
        collector.RecordDuration("metric.b", TimeSpan.FromMilliseconds(200));

        // Act
        var allStats = collector.GetAllStatistics();

        // Assert
        allStats.Should().ContainKey("metric.a");
        allStats.Should().ContainKey("metric.b");
    }

    [Fact]
    public void Clear_ShouldRemoveAllData()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.RecordDuration("test.metric", TimeSpan.FromMilliseconds(100));
        var pipelineId = Guid.NewGuid();
        collector.StartPipeline(pipelineId);

        // Act
        collector.Clear();

        // Assert
        collector.DataPointCount.Should().Be(0);
        collector.GetDataPoints().Should().BeEmpty();
    }

    [Fact]
    public void MaxDataPointsToRetain_ShouldLimitData()
    {
        // Arrange
        var config = new MetricsCollectorConfig { MaxDataPointsToRetain = 5 };
        var collector = new MetricsCollector(config);

        // Act
        for (int i = 0; i < 10; i++)
        {
            collector.IncrementCounter($"counter.{i}");
        }

        // Assert
        collector.DataPointCount.Should().Be(5);
    }

    [Fact]
    public void GenerateReport_ShouldProduceValidReport()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.RecordDuration("pipeline.duration", TimeSpan.FromMilliseconds(500));
        collector.IncrementCounter("pipeline.calls");

        // Act
        var report = collector.GenerateReport();

        // Assert
        report.Statistics.Should().ContainKey("pipeline.duration");
        report.Statistics.Should().ContainKey("pipeline.calls");
    }

    [Fact]
    public void RecordDataPoint_WithTags_ShouldStoreTags()
    {
        // Arrange
        var collector = new MetricsCollector();
        var tags = new Dictionary<string, string>
        {
            ["environment"] = "test",
            ["version"] = "1.0"
        };

        // Act
        collector.RecordDuration("test.metric", TimeSpan.FromMilliseconds(100), tags);

        // Assert
        var points = collector.GetDataPoints("test.metric");
        points[0].Tags.Should().ContainKey("environment");
        points[0].Tags["environment"].Should().Be("test");
    }
}

/// <summary>
/// Tests pour MetricDataPoint.
/// </summary>
public class MetricDataPointTests
{
    [Fact]
    public void Duration_ShouldCreateCorrectType()
    {
        // Act
        var point = MetricDataPoint.Duration("test", TimeSpan.FromMilliseconds(150));

        // Assert
        point.Type.Should().Be(MetricType.Duration);
        point.Value.Should().Be(150);
    }

    [Fact]
    public void Counter_ShouldCreateCorrectType()
    {
        // Act
        var point = MetricDataPoint.Counter("test", 42);

        // Assert
        point.Type.Should().Be(MetricType.Counter);
        point.Value.Should().Be(42);
    }

    [Fact]
    public void Gauge_ShouldCreateCorrectType()
    {
        // Act
        var point = MetricDataPoint.Gauge("test", 75.5);

        // Assert
        point.Type.Should().Be(MetricType.Gauge);
        point.Value.Should().Be(75.5);
    }
}

/// <summary>
/// Tests pour MetricStatistics.
/// </summary>
public class MetricStatisticsTests
{
    [Fact]
    public void Calculate_WithEmptyValues_ShouldReturnZeros()
    {
        // Act
        var stats = MetricStatistics.Calculate("test", Array.Empty<double>());

        // Assert
        stats.Count.Should().Be(0);
        stats.Min.Should().Be(0);
        stats.Max.Should().Be(0);
        stats.Average.Should().Be(0);
    }

    [Fact]
    public void Calculate_WithSingleValue_ShouldReturnSameValue()
    {
        // Act
        var stats = MetricStatistics.Calculate("test", new[] { 100.0 });

        // Assert
        stats.Count.Should().Be(1);
        stats.Min.Should().Be(100);
        stats.Max.Should().Be(100);
        stats.Average.Should().Be(100);
        stats.Percentile50.Should().Be(100);
    }

    [Fact]
    public void Calculate_WithMultipleValues_ShouldCalculateCorrectly()
    {
        // Arrange
        var values = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };

        // Act
        var stats = MetricStatistics.Calculate("test", values);

        // Assert
        stats.Count.Should().Be(5);
        stats.Min.Should().Be(10);
        stats.Max.Should().Be(50);
        stats.Average.Should().Be(30);
    }

    [Fact]
    public void Calculate_Percentiles_ShouldBeCorrect()
    {
        // Arrange - 100 values from 1 to 100
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        // Act
        var stats = MetricStatistics.Calculate("test", values);

        // Assert
        stats.Percentile50.Should().Be(50);
        stats.Percentile95.Should().Be(95);
        stats.Percentile99.Should().Be(99);
    }
}

/// <summary>
/// Tests pour PipelineMetricsSummary.
/// </summary>
public class PipelineMetricsSummaryTests
{
    [Fact]
    public void AverageStageDuration_ShouldCalculateCorrectly()
    {
        // Arrange
        var summary = new PipelineMetricsSummary(
            Guid.NewGuid(),
            TimeSpan.FromMilliseconds(300),
            new Dictionary<string, TimeSpan>
            {
                ["Stage1"] = TimeSpan.FromMilliseconds(100),
                ["Stage2"] = TimeSpan.FromMilliseconds(200)
            },
            new Dictionary<AgentType, TimeSpan>(),
            0, 0, true);

        // Act & Assert
        summary.AverageStageDuration.TotalMilliseconds.Should().Be(150);
    }

    [Fact]
    public void SlowestStage_ShouldIdentifyCorrectly()
    {
        // Arrange
        var summary = new PipelineMetricsSummary(
            Guid.NewGuid(),
            TimeSpan.FromMilliseconds(300),
            new Dictionary<string, TimeSpan>
            {
                ["Fast"] = TimeSpan.FromMilliseconds(50),
                ["Slow"] = TimeSpan.FromMilliseconds(200),
                ["Medium"] = TimeSpan.FromMilliseconds(100)
            },
            new Dictionary<AgentType, TimeSpan>(),
            0, 0, true);

        // Act & Assert
        summary.SlowestStage.Should().Be("Slow");
    }

    [Fact]
    public void SlowestAgent_ShouldIdentifyCorrectly()
    {
        // Arrange
        var summary = new PipelineMetricsSummary(
            Guid.NewGuid(),
            TimeSpan.FromMilliseconds(300),
            new Dictionary<string, TimeSpan>(),
            new Dictionary<AgentType, TimeSpan>
            {
                [AgentType.Summary] = TimeSpan.FromMilliseconds(50),
                [AgentType.Narrator] = TimeSpan.FromMilliseconds(150),
                [AgentType.Character] = TimeSpan.FromMilliseconds(100)
            },
            3, 0, true);

        // Act & Assert
        summary.SlowestAgent.Should().Be(AgentType.Narrator);
    }
}

/// <summary>
/// Tests pour MetricsReport.
/// </summary>
public class MetricsReportTests
{
    [Fact]
    public void ToText_ShouldProduceReadableOutput()
    {
        // Arrange
        var stats = new Dictionary<string, MetricStatistics>
        {
            ["pipeline.duration"] = MetricStatistics.Calculate("pipeline.duration", new[] { 100.0, 200.0 })
        };
        var report = new MetricsReport(DateTime.UtcNow, stats);

        // Act
        var text = report.ToText();

        // Assert
        text.Should().Contain("Metrics Report");
        text.Should().Contain("pipeline.duration");
        text.Should().Contain("Avg:");
    }

    [Fact]
    public void ToText_WithEmptyStats_ShouldIndicateNoMetrics()
    {
        // Arrange
        var report = new MetricsReport(DateTime.UtcNow, new Dictionary<string, MetricStatistics>());

        // Act
        var text = report.ToText();

        // Assert
        text.Should().Contain("No metrics collected");
    }
}

/// <summary>
/// Tests pour MetricsCollectorConfig.
/// </summary>
public class MetricsCollectorConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableValues()
    {
        // Act
        var config = MetricsCollectorConfig.Default;

        // Assert
        config.MaxDataPointsToRetain.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ForTesting_ShouldHaveLowerLimits()
    {
        // Act
        var config = MetricsCollectorConfig.ForTesting;

        // Assert
        config.MaxDataPointsToRetain.Should().BeLessThan(MetricsCollectorConfig.Default.MaxDataPointsToRetain);
    }
}
