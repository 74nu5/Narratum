namespace Narratum.Memory.Tests;

using Narratum.Memory;
using Narratum.Memory.Services;
using Xunit;

public class CoherenceValidatorTests
{
    private readonly CoherenceValidator _validator = new();

    #region ContainsContradiction Tests

    [Fact]
    public void ContainsContradiction_AliveVsDead_ReturnsTrue()
    {
        // Arrange
        var aricAlive = Fact.Create(
            content: "Aric is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        var aricDead = Fact.Create(
            content: "Aric is dead",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        // Act
        var result = _validator.ContainsContradiction(aricAlive, aricDead);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsContradiction_DeadVsAlive_ReturnsTrue()
    {
        // Arrange
        var aricDead = Fact.Create(
            content: "Aric died",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        var aricLiving = Fact.Create(
            content: "Aric is living",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        // Act
        var result = _validator.ContainsContradiction(aricDead, aricLiving);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsContradiction_SameFact_ReturnsFalse()
    {
        // Arrange
        var fact = Fact.Create(
            content: "Aric is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        // Act
        var result = _validator.ContainsContradiction(fact, fact);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsContradiction_DifferentEntities_ReturnsFalse()
    {
        // Arrange
        var aricDead = Fact.Create(
            content: "Aric is dead",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        var lyraAlive = Fact.Create(
            content: "Lyra is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Lyra" }
        );

        // Act
        var result = _validator.ContainsContradiction(aricDead, lyraAlive);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsContradiction_DestroyedVsIntact_ReturnsTrue()
    {
        // Arrange
        var towerDestroyed = Fact.Create(
            content: "Tower is destroyed",
            factType: FactType.LocationState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Tower" }
        );

        var towerStanding = Fact.Create(
            content: "Tower is standing",
            factType: FactType.LocationState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Tower" }
        );

        // Act
        var result = _validator.ContainsContradiction(towerDestroyed, towerStanding);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsContradiction_NoSharedEntities_ReturnsFalse()
    {
        // Arrange
        var fact1 = Fact.Create(
            content: "Aric is dead",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        var fact2 = Fact.Create(
            content: "Lyra is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Lyra" }
        );

        // Act
        var result = _validator.ContainsContradiction(fact1, fact2);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ValidateFact Tests

    [Fact]
    public void ValidateFact_EmptyContent_ReturnsViolation()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>(),
            Confidence: 0.8
        );

        // Act
        var violation = _validator.ValidateFact(fact);

        // Assert
        Assert.NotNull(violation);
        Assert.Equal(CoherenceSeverity.Error, violation.Severity);
    }

    [Fact]
    public void ValidateFact_ConfidenceBelowZero_ReturnsViolation()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "Test",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>(),
            Confidence: -0.5
        );

        // Act
        var violation = _validator.ValidateFact(fact);

        // Assert
        Assert.NotNull(violation);
    }

    [Fact]
    public void ValidateFact_ConfidenceAboveOne_ReturnsViolation()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "Test",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>(),
            Confidence: 1.5
        );

        // Act
        var violation = _validator.ValidateFact(fact);

        // Assert
        Assert.NotNull(violation);
    }

    [Fact]
    public void ValidateFact_ValidFact_ReturnsNull()
    {
        // Arrange
        var fact = Fact.Create(
            content: "Aric is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" }
        );

        // Act
        var violation = _validator.ValidateFact(fact);

        // Assert
        Assert.Null(violation);
    }

    #endregion

    #region ValidateFacts Tests

    [Fact]
    public void ValidateFacts_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var facts = new List<Fact>();

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFacts_SingleValidFact_ReturnsEmpty()
    {
        // Arrange
        var facts = new List<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            )
        };

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFacts_ContradictoryFacts_ReturnsViolation()
    {
        // Arrange
        var facts = new List<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Aric is dead",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            )
        };

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.Single(violations);
        Assert.Equal(CoherenceViolationType.StatementContradiction, violations[0].ViolationType);
    }

    [Fact]
    public void ValidateFacts_MultipleContradictions_ReturnsAllViolations()
    {
        // Arrange
        var facts = new List<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Aric is dead",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Tower is destroyed",
                factType: FactType.LocationState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Tower" }
            ),
            Fact.Create(
                content: "Tower is standing",
                factType: FactType.LocationState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Tower" }
            )
        };

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.Equal(2, violations.Count(v => v.ViolationType == CoherenceViolationType.StatementContradiction));
    }

    [Fact]
    public void ValidateFacts_MixedValidAndInvalid_ReturnsOnlyInvalid()
    {
        // Arrange
        var facts = new List<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { "Aric" }
            ),
            new Fact(
                Id: Guid.NewGuid(),
                Content: "",
                FactType: FactType.Event,
                MemoryLevel: MemoryLevel.Event,
                EntityReferences: new HashSet<string>(),
                Confidence: 0.8
            )
        };

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.Single(violations);
    }

    #endregion

    #region ValidateState Tests

    [Fact]
    public void ValidateState_EmptyState_ReturnsEmpty()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.World);

        // Act
        var violations = _validator.ValidateState(state);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateState_ValidFacts_ReturnsEmpty()
    {
        // Arrange
        var facts = new HashSet<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Tower is safe",
                factType: FactType.LocationState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Tower" }
            )
        };

        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(facts.First())
            .AddFact(facts.Last());

        // Act
        var violations = _validator.ValidateState(state);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateState_ContradictoryFacts_ReturnsViolation()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ))
            .AddFact(Fact.Create(
                content: "Aric is dead",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ));

        // Act
        var violations = _validator.ValidateState(state);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.ViolationType == CoherenceViolationType.StatementContradiction);
    }

    #endregion

    #region ValidateTransition Tests

    [Fact]
    public void ValidateTransition_AliveToDeadValid_ReturnsEmpty()
    {
        // Arrange
        var fact1 = Fact.Create(
            content: "Aric is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Chapter,
            entityReferences: new[] { "Aric" }
        );

        var fact2 = Fact.Create(
            content: "Aric is dead",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Chapter,
            entityReferences: new[] { "Aric" }
        );

        var prevState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(fact1);

        var newState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(fact2);

        // Act
        var violations = _validator.ValidateTransition(prevState, newState);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateTransition_DeadToAliveInvalid_ReturnsViolation()
    {
        // Arrange
        var fact1 = Fact.Create(
            content: "Aric is dead",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Chapter,
            entityReferences: new[] { "Aric" }
        );

        var fact2 = Fact.Create(
            content: "Aric is alive",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Chapter,
            entityReferences: new[] { "Aric" }
        );

        var prevState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(fact1);

        var newState = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(fact2);

        // Act
        var violations = _validator.ValidateTransition(prevState, newState);

        // Assert
        Assert.NotEmpty(violations);
        Assert.All(violations, v => Assert.Equal(CoherenceViolationType.SequenceViolation, v.ViolationType));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ValidateState_DeterministicResults()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Chapter)
            .AddFact(Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ));

        // Act
        var violations1 = _validator.ValidateState(state);
        var violations2 = _validator.ValidateState(state);

        // Assert
        Assert.Equal(violations1.Count, violations2.Count);
    }

    [Fact]
    public void ValidateFacts_LargeDataset_PerformsWell()
    {
        // Arrange
        var facts = new List<Fact>();
        for (int i = 0; i < 100; i++)
        {
            facts.Add(Fact.Create(
                content: $"Event {i}",
                factType: FactType.Event,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { $"Entity{i}" }
            ));
        }

        // Act
        var startTime = DateTime.UtcNow;
        var violations = _validator.ValidateFacts(facts);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.Empty(violations);
        Assert.True(elapsed.TotalMilliseconds < 1000, $"Performance check failed: {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public void ValidateFacts_ComplexScenario_AllViolationsDetected()
    {
        // Arrange - Scénario complexe avec plusieurs types de violations
        var facts = new List<Fact>
        {
            Fact.Create(
                content: "Aric is alive",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Aric is dead",
                factType: FactType.CharacterState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Aric" }
            ),
            Fact.Create(
                content: "Tower is destroyed",
                factType: FactType.LocationState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Tower" }
            ),
            Fact.Create(
                content: "Tower is intact",
                factType: FactType.LocationState,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Tower" }
            ),
            Fact.Create(
                content: "Lyra trusts Aric",
                factType: FactType.Relationship,
                memoryLevel: MemoryLevel.Chapter,
                entityReferences: new[] { "Lyra", "Aric" }
            )
        };

        // Act
        var violations = _validator.ValidateFacts(facts);

        // Assert
        Assert.NotEmpty(violations);
        var contradictionCount = violations.Count(v => v.ViolationType == CoherenceViolationType.StatementContradiction);
        Assert.Equal(2, contradictionCount);
    }

    #endregion
}
