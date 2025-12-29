using Narratum.Memory;

namespace Narratum.Memory.Tests;

public class CoherenceViolationTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateViolation()
    {
        // Arrange
        var violationType = CoherenceViolationType.StatementContradiction;
        var severity = CoherenceSeverity.Error;
        var description = "Aric is both alive and dead";
        var factIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var violation = CoherenceViolation.Create(violationType, severity, description, factIds);

        // Assert
        Assert.NotEqual(Guid.Empty, violation.Id);
        Assert.Equal(violationType, violation.ViolationType);
        Assert.Equal(severity, violation.Severity);
        Assert.Equal(description, violation.Description);
        Assert.Equal(2, violation.InvolvedFactIds.Count);
        Assert.Null(violation.ResolvedAt);
    }

    [Fact]
    public void MarkResolved_ShouldSetResolvedAt()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Contradiction detected",
            new[] { Guid.NewGuid() }
        );

        // Act
        var resolvedViolation = violation.MarkResolved();

        // Assert
        Assert.Null(violation.ResolvedAt);
        Assert.NotNull(resolvedViolation.ResolvedAt);
        Assert.True(resolvedViolation.IsResolved);
    }

    [Fact]
    public void IsResolved_WithoutResolvedAt_ShouldReturnFalse()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Warning,
            "Potential issue",
            new[] { Guid.NewGuid() }
        );

        // Act
        var isResolved = violation.IsResolved;

        // Assert
        Assert.False(isResolved);
    }

    [Fact]
    public void Validate_WithValidViolation_ShouldReturnTrue()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.EntityInconsistency,
            CoherenceSeverity.Error,
            "Valid violation",
            new[] { Guid.NewGuid() }
        );

        // Act
        var isValid = violation.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithEmptyDescription_ShouldReturnFalse()
    {
        // Arrange
        var violation = new CoherenceViolation(
            Id: Guid.NewGuid(),
            ViolationType: CoherenceViolationType.StatementContradiction,
            Severity: CoherenceSeverity.Error,
            Description: "",
            InvolvedFactIds: new HashSet<Guid> { Guid.NewGuid() }
        );

        // Act
        var isValid = violation.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithNoInvolvedFacts_ShouldReturnFalse()
    {
        // Arrange
        var violation = new CoherenceViolation(
            Id: Guid.NewGuid(),
            ViolationType: CoherenceViolationType.StatementContradiction,
            Severity: CoherenceSeverity.Error,
            Description: "Violation description",
            InvolvedFactIds: new HashSet<Guid>()
        );

        // Act
        var isValid = violation.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithResolvedBeforeDetected_ShouldReturnFalse()
    {
        // Arrange
        var detected = DateTime.UtcNow;
        var resolved = detected.AddMinutes(-1); // Résolu avant la détection
        var violation = new CoherenceViolation(
            Id: Guid.NewGuid(),
            ViolationType: CoherenceViolationType.SequenceViolation,
            Severity: CoherenceSeverity.Error,
            Description: "Timeline issue",
            InvolvedFactIds: new HashSet<Guid> { Guid.NewGuid() },
            DetectedAt: detected,
            ResolvedAt: resolved
        );

        // Act
        var isValid = violation.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void CoherenceViolation_IsImmutable()
    {
        // Arrange
        var violation1 = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Warning,
            "Original description",
            new[] { Guid.NewGuid() }
        );

        // Act
        var violation2 = violation1.MarkResolved();

        // Assert
        Assert.False(violation1.IsResolved);
        Assert.True(violation2.IsResolved);
        Assert.NotEqual(violation1, violation2);
    }

    [Fact]
    public void Create_WithResolution_ShouldPreserveIt()
    {
        // Arrange
        const string resolution = "Choose canonical fact and deprecate other";
        var factIds = new[] { Guid.NewGuid() };

        // Act
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Contradiction",
            factIds,
            resolution
        );

        // Assert
        Assert.Equal(resolution, violation.Resolution);
    }

    [Fact]
    public void GetFullDescription_ShouldIncludeAllInfo()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.EntityInconsistency,
            CoherenceSeverity.Error,
            "Aric state mismatch",
            new[] { Guid.NewGuid() },
            "Verify canonical facts"
        );

        // Act
        var fullDescription = violation.GetFullDescription();

        // Assert
        Assert.Contains("Error", fullDescription);
        Assert.Contains("Aric state mismatch", fullDescription);
        Assert.Contains("1 fait", fullDescription);
        Assert.Contains("Résolution", fullDescription);
    }

    [Fact]
    public void GetFullDescription_ForResolvedViolation_ShouldIncludeResolution()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Warning,
            "Potential contradiction",
            new[] { Guid.NewGuid() }
        ).MarkResolved();

        // Act
        var fullDescription = violation.GetFullDescription();

        // Assert
        Assert.Contains("Résolu le", fullDescription);
    }
}
