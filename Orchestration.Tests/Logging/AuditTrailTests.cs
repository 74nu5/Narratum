using FluentAssertions;
using Narratum.Orchestration.Logging;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Orchestration.Tests.Logging;

/// <summary>
/// Tests pour AuditTrail.
/// </summary>
public class AuditTrailTests
{
    [Fact]
    public void Record_ShouldAddEntry()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var entry = AuditEntry.Create(
            Guid.NewGuid(),
            "TestAction",
            "TestActor",
            "Test description");

        // Act
        auditTrail.Record(entry);

        // Assert
        auditTrail.Count.Should().Be(1);
        auditTrail.GetEntries().Should().ContainSingle();
    }

    [Fact]
    public void Record_WithNullEntry_ShouldThrow()
    {
        // Arrange
        var auditTrail = new AuditTrail();

        // Act
        var action = () => auditTrail.Record(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordDecision_ShouldCreateDecisionEntry()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        // Act
        auditTrail.RecordDecision(pipelineId, "SkipRetry", "Max retries reached");

        // Assert
        var entries = auditTrail.GetEntries(pipelineId);
        entries.Should().ContainSingle();
        entries[0].Action.Should().Be("Decision");
        entries[0].Actor.Should().Be("Orchestrator");
        entries[0].Description.Should().Contain("SkipRetry");
    }

    [Fact]
    public void RecordAgentAction_ShouldCreateAgentEntry()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        // Act
        auditTrail.RecordAgentAction(pipelineId, AgentType.Narrator, "Generate", "Generated narrative");

        // Assert
        var entries = auditTrail.GetEntries(pipelineId);
        entries.Should().ContainSingle();
        entries[0].Category.Should().Be(AuditCategory.Agent);
        entries[0].Actor.Should().Be("Narrator");
    }

    [Fact]
    public void RecordValidationFailure_ShouldCreateWarningEntry()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        auditTrail.RecordValidationFailure(pipelineId, "StructureValidator", errors);

        // Assert
        var entries = auditTrail.GetEntries(pipelineId);
        entries[0].Severity.Should().Be(AuditSeverity.Warning);
        entries[0].Category.Should().Be(AuditCategory.Validation);
    }

    [Fact]
    public void RecordStateChange_ShouldIncludeOldAndNewValues()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        // Act
        auditTrail.RecordStateChange(
            pipelineId,
            StateChangeType.CharacterMoved,
            "Character moved to new location",
            oldValue: "Location A",
            newValue: "Location B");

        // Assert
        var entries = auditTrail.GetEntries(pipelineId);
        entries[0].Details.Should().ContainKey("old_value");
        entries[0].Details.Should().ContainKey("new_value");
    }

    [Fact]
    public void GetEntries_WithPipelineId_ShouldFilterCorrectly()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId1 = Guid.NewGuid();
        var pipelineId2 = Guid.NewGuid();

        auditTrail.RecordDecision(pipelineId1, "Decision1", "Reason1");
        auditTrail.RecordDecision(pipelineId2, "Decision2", "Reason2");
        auditTrail.RecordDecision(pipelineId1, "Decision3", "Reason3");

        // Act
        var entries1 = auditTrail.GetEntries(pipelineId1);
        var entries2 = auditTrail.GetEntries(pipelineId2);
        var allEntries = auditTrail.GetEntries();

        // Assert
        entries1.Should().HaveCount(2);
        entries2.Should().HaveCount(1);
        allEntries.Should().HaveCount(3);
    }

    [Fact]
    public void GetEntriesBySeverity_ShouldFilterCorrectly()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        auditTrail.Record(AuditEntry.Create(pipelineId, "Info", "Actor", "Info entry", AuditSeverity.Info));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Warning", "Actor", "Warning entry", AuditSeverity.Warning));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Error", "Actor", "Error entry", AuditSeverity.Error));

        // Act
        var warnings = auditTrail.GetEntriesBySeverity(AuditSeverity.Warning);

        // Assert
        warnings.Should().ContainSingle();
        warnings[0].Description.Should().Be("Warning entry");
    }

    [Fact]
    public void GetEntriesByCategory_ShouldFilterCorrectly()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        auditTrail.RecordAgentAction(pipelineId, AgentType.Narrator, "Action1", "Desc1");
        auditTrail.RecordAgentAction(pipelineId, AgentType.Summary, "Action2", "Desc2");
        auditTrail.RecordValidationFailure(pipelineId, "Validator", new[] { "Error" });

        // Act
        var agentEntries = auditTrail.GetEntriesByCategory(AuditCategory.Agent);
        var validationEntries = auditTrail.GetEntriesByCategory(AuditCategory.Validation);

        // Assert
        agentEntries.Should().HaveCount(2);
        validationEntries.Should().HaveCount(1);
    }

    [Fact]
    public void GetEntriesByAction_ShouldFilterCorrectly()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        auditTrail.RecordDecision(pipelineId, "D1", "R1");
        auditTrail.RecordDecision(pipelineId, "D2", "R2");
        auditTrail.RecordAgentAction(pipelineId, AgentType.Narrator, "Generate", "Desc");

        // Act
        var decisions = auditTrail.GetEntriesByAction("Decision");

        // Assert
        decisions.Should().HaveCount(2);
    }

    [Fact]
    public void GetEntriesInRange_ShouldFilterByTime()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        var now = DateTime.UtcNow;
        auditTrail.Record(new AuditEntry(
            Guid.NewGuid(), pipelineId, now.AddMinutes(-10),
            "Old", "Actor", "Old entry", AuditSeverity.Info, AuditCategory.Pipeline));
        auditTrail.Record(new AuditEntry(
            Guid.NewGuid(), pipelineId, now.AddMinutes(-5),
            "Middle", "Actor", "Middle entry", AuditSeverity.Info, AuditCategory.Pipeline));
        auditTrail.Record(new AuditEntry(
            Guid.NewGuid(), pipelineId, now,
            "Recent", "Actor", "Recent entry", AuditSeverity.Info, AuditCategory.Pipeline));

        // Act
        var entries = auditTrail.GetEntriesInRange(now.AddMinutes(-6), now.AddMinutes(-4));

        // Assert
        entries.Should().ContainSingle();
        entries[0].Action.Should().Be("Middle");
    }

    [Fact]
    public void GetProblems_ShouldReturnWarningsAndAbove()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        auditTrail.Record(AuditEntry.Create(pipelineId, "Debug", "A", "D", AuditSeverity.Debug));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Info", "A", "I", AuditSeverity.Info));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Warning", "A", "W", AuditSeverity.Warning));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Error", "A", "E", AuditSeverity.Error));
        auditTrail.Record(AuditEntry.Create(pipelineId, "Critical", "A", "C", AuditSeverity.Critical));

        // Act
        var problems = auditTrail.GetProblems();

        // Assert
        problems.Should().HaveCount(3); // Warning, Error, Critical
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();
        auditTrail.RecordDecision(pipelineId, "D1", "R1");
        auditTrail.RecordDecision(pipelineId, "D2", "R2");

        // Act
        auditTrail.Clear();

        // Assert
        auditTrail.Count.Should().Be(0);
        auditTrail.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void MaxEntriesToRetain_ShouldLimitEntries()
    {
        // Arrange
        var config = new AuditTrailConfig { MaxEntriesToRetain = 3 };
        var auditTrail = new AuditTrail(config);
        var pipelineId = Guid.NewGuid();

        // Act
        for (int i = 0; i < 5; i++)
        {
            auditTrail.RecordDecision(pipelineId, $"Decision{i}", $"Reason{i}");
        }

        // Assert
        auditTrail.Count.Should().Be(3);
        var entries = auditTrail.GetEntries();
        entries[0].Description.Should().Contain("Decision2"); // Les plus anciens sont supprimés
    }

    [Fact]
    public void GenerateReport_ShouldProduceValidReport()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId = Guid.NewGuid();

        auditTrail.RecordDecision(pipelineId, "Start", "Starting pipeline");
        auditTrail.RecordAgentAction(pipelineId, AgentType.Narrator, "Generate", "Generated");
        auditTrail.RecordValidationFailure(pipelineId, "Validator", new[] { "Error" });

        // Act
        var report = auditTrail.GenerateReport(pipelineId);

        // Assert
        report.PipelineId.Should().Be(pipelineId);
        report.EntryCount.Should().Be(3);
        report.WarningCount.Should().Be(1);
    }

    [Fact]
    public void GenerateGlobalReport_ShouldIncludeAllPipelines()
    {
        // Arrange
        var auditTrail = new AuditTrail();
        var pipelineId1 = Guid.NewGuid();
        var pipelineId2 = Guid.NewGuid();

        auditTrail.RecordDecision(pipelineId1, "D1", "R1");
        auditTrail.RecordDecision(pipelineId2, "D2", "R2");

        // Act
        var report = auditTrail.GenerateGlobalReport();

        // Assert
        report.PipelineId.Should().Be(Guid.Empty);
        report.EntryCount.Should().Be(2);
    }
}

/// <summary>
/// Tests pour AuditEntry.
/// </summary>
public class AuditEntryTests
{
    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var entry1 = AuditEntry.Create(Guid.NewGuid(), "Action", "Actor", "Desc");
        var entry2 = AuditEntry.Create(Guid.NewGuid(), "Action", "Actor", "Desc");

        // Assert
        entry1.Id.Should().NotBe(entry2.Id);
    }

    [Fact]
    public void Decision_ShouldCreateCorrectEntry()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();

        // Act
        var entry = AuditEntry.Decision(pipelineId, "SkipAgent", "Not required");

        // Assert
        entry.Action.Should().Be("Decision");
        entry.Actor.Should().Be("Orchestrator");
        entry.Category.Should().Be(AuditCategory.Pipeline);
        entry.Description.Should().Contain("SkipAgent");
    }

    [Fact]
    public void AgentAction_ShouldIncludeAgentType()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();

        // Act
        var entry = AuditEntry.AgentAction(pipelineId, AgentType.Character, "Dialogue", "Generated dialogue");

        // Assert
        entry.Actor.Should().Be("Character");
        entry.Category.Should().Be(AuditCategory.Agent);
        entry.Details!["agent_type"].Should().Be("Character");
    }

    [Fact]
    public void CriticalError_ShouldCaptureException()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test exception");

        // Act
        var entry = AuditEntry.CriticalError(pipelineId, "TestSource", exception);

        // Assert
        entry.Severity.Should().Be(AuditSeverity.Critical);
        entry.Category.Should().Be(AuditCategory.System);
        entry.Details!["exception_type"].Should().Be("InvalidOperationException");
    }
}

/// <summary>
/// Tests pour AuditReport.
/// </summary>
public class AuditReportTests
{
    [Fact]
    public void HasProblems_ShouldBeTrueWithErrors()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "A", "A", "D", AuditSeverity.Error)
        };

        // Act
        var report = new AuditReport(Guid.NewGuid(), entries);

        // Assert
        report.HasProblems.Should().BeTrue();
        report.ErrorCount.Should().Be(1);
    }

    [Fact]
    public void HasProblems_ShouldBeFalseWithOnlyInfo()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "A", "A", "D", AuditSeverity.Info)
        };

        // Act
        var report = new AuditReport(Guid.NewGuid(), entries);

        // Assert
        report.HasProblems.Should().BeFalse();
    }

    [Fact]
    public void ByCategory_ShouldGroupCorrectly()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "A", "A", "D", category: AuditCategory.Agent),
            AuditEntry.Create(Guid.NewGuid(), "A", "A", "D", category: AuditCategory.Agent),
            AuditEntry.Create(Guid.NewGuid(), "A", "A", "D", category: AuditCategory.Validation)
        };

        // Act
        var report = new AuditReport(Guid.NewGuid(), entries);

        // Assert
        report.ByCategory.Should().ContainKey(AuditCategory.Agent);
        report.ByCategory[AuditCategory.Agent].Should().HaveCount(2);
    }

    [Fact]
    public void ToText_ShouldProduceReadableOutput()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "TestAction", "TestActor", "Test description")
        };
        var report = new AuditReport(Guid.NewGuid(), entries);

        // Act
        var text = report.ToText();

        // Assert
        text.Should().Contain("Audit Report");
        text.Should().Contain("TestAction");
        text.Should().Contain("TestActor");
    }
}
