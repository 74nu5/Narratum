using Narratum.Memory;

namespace Narratum.Memory.Tests;

public class MemorandumTests
{
    [Fact]
    public void CreateEmpty_ShouldInitializeAllMemoryLevels()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var title = "World Memory";
        var description = "Complete world state";

        // Act
        var memorandum = Memorandum.CreateEmpty(worldId, title, description);

        // Assert
        Assert.NotEqual(Guid.Empty, memorandum.Id);
        Assert.Equal(worldId, memorandum.WorldId);
        Assert.Equal(title, memorandum.Title);
        Assert.Equal(description, memorandum.Description);
        Assert.Equal(4, memorandum.CanonicalStates.Count);
        Assert.Empty(memorandum.Violations);
        Assert.NotEqual(default, memorandum.CreatedAt);
    }

    [Fact]
    public void AddFact_ShouldAddToCorrectLevel()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
        var fact = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });

        // Act
        var newMemorandum = memorandum.AddFact(MemoryLevel.Event, fact);

        // Assert
        Assert.Empty(memorandum.GetFacts(MemoryLevel.Event));
        Assert.Single(newMemorandum.GetFacts(MemoryLevel.Event));
        Assert.NotEqual(memorandum, newMemorandum);
    }

    [Fact]
    public void AddFact_ShouldIncrementVersion()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
        var initialTimestamp = memorandum.LastUpdated;
        var fact = Fact.Create("Tower destroyed", FactType.LocationState, MemoryLevel.Chapter, new[] { "Tower" });

        // Act
        var newMemorandum = memorandum.AddFact(MemoryLevel.Chapter, fact);

        // Assert
        Assert.True(newMemorandum.LastUpdated >= initialTimestamp);
    }

    [Fact]
    public void AddFacts_ShouldAddMultiple()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
        var facts = new[]
        {
            Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" }),
            Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" })
        };

        // Act
        var newMemorandum = memorandum.AddFacts(MemoryLevel.Event, facts);

        // Assert
        Assert.Equal(2, newMemorandum.GetFacts(MemoryLevel.Event).Count());
    }

    [Fact]
    public void AddViolation_ShouldTrackViolation()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Aric contradiction",
            new[] { Guid.NewGuid() }
        );

        // Act
        var newMemorandum = memorandum.AddViolation(violation);

        // Assert
        Assert.Empty(memorandum.Violations);
        Assert.Single(newMemorandum.Violations);
    }

    [Fact]
    public void ResolveViolation_ShouldMarkAsResolved()
    {
        // Arrange
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.EntityInconsistency,
            CoherenceSeverity.Warning,
            "State mismatch",
            new[] { Guid.NewGuid() }
        );
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddViolation(violation);

        // Act
        var newMemorandum = memorandum.ResolveViolation(violation.Id);

        // Assert
        Assert.Empty(newMemorandum.GetUnresolvedViolations());
        Assert.Single(newMemorandum.GetResolvedViolations());
    }

    [Fact]
    public void GetFactsForEntity_ShouldReturnEntityFacts()
    {
        // Arrange
        var fact1 = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var fact2 = Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" });
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddFacts(MemoryLevel.Event, new[] { fact1, fact2 });

        // Act
        var aricFacts = memorandum.GetFactsForEntity(MemoryLevel.Event, "Aric");

        // Assert
        Assert.Single(aricFacts);
        Assert.Contains(fact1, aricFacts);
    }

    [Fact]
    public void GetUnresolvedViolations_ShouldReturnOnlyUnresolved()
    {
        // Arrange
        var violation1 = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Violation 1",
            new[] { Guid.NewGuid() }
        );
        var violation2 = CoherenceViolation.Create(
            CoherenceViolationType.SequenceViolation,
            CoherenceSeverity.Warning,
            "Violation 2",
            new[] { Guid.NewGuid() }
        );
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddViolations(new[] { violation1, violation2 })
            .ResolveViolation(violation1.Id);

        // Act
        var unresolved = memorandum.GetUnresolvedViolations();

        // Assert
        Assert.Single(unresolved);
        Assert.Equal(violation2.Id, unresolved.First().Id);
    }

    [Fact]
    public void GetViolationsBySeverity_ShouldFilterByLevel()
    {
        // Arrange
        var errorViolation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Error violation",
            new[] { Guid.NewGuid() }
        );
        var warningViolation = CoherenceViolation.Create(
            CoherenceViolationType.EntityInconsistency,
            CoherenceSeverity.Warning,
            "Warning violation",
            new[] { Guid.NewGuid() }
        );
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddViolations(new[] { errorViolation, warningViolation });

        // Act
        var errors = memorandum.GetViolationsBySeverity(CoherenceSeverity.Error);

        // Assert
        Assert.Single(errors);
        Assert.Contains(errorViolation, errors);
    }

    [Fact]
    public void Memorandum_IsImmutable()
    {
        // Arrange
        var memorandum1 = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");
        var fact = Fact.Create("Test fact", FactType.Event, MemoryLevel.Event, new[] { "Entity" });

        // Act
        var memorandum2 = memorandum1.AddFact(MemoryLevel.Event, fact);

        // Assert
        Assert.Empty(memorandum1.GetFacts(MemoryLevel.Event));
        Assert.Single(memorandum2.GetFacts(MemoryLevel.Event));
    }

    [Fact]
    public void Validate_WithValidState_ShouldReturnTrue()
    {
        // Arrange
        var fact = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddFact(MemoryLevel.Event, fact);

        // Act
        var isValid = memorandum.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void GetCanonicalState_ShouldReturnStateForLevel()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");

        // Act
        var eventState = memorandum.GetCanonicalState(MemoryLevel.Event);
        var chapterState = memorandum.GetCanonicalState(MemoryLevel.Chapter);

        // Assert
        Assert.NotNull(eventState);
        Assert.NotNull(chapterState);
        Assert.Equal(MemoryLevel.Event, eventState.MemoryLevel);
        Assert.Equal(MemoryLevel.Chapter, chapterState.MemoryLevel);
    }

    [Fact]
    public void GetSummary_ShouldIncludeAllRelevantInfo()
    {
        // Arrange
        var fact = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var violation = CoherenceViolation.Create(
            CoherenceViolationType.StatementContradiction,
            CoherenceSeverity.Error,
            "Contradiction",
            new[] { Guid.NewGuid() }
        );
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test Memorandum", "Test description")
            .AddFact(MemoryLevel.Event, fact)
            .AddViolation(violation);

        // Act
        var summary = memorandum.GetSummary();

        // Assert
        Assert.Contains("Test Memorandum", summary);
        Assert.Contains("Test description", summary);
        Assert.Contains("Event: 1 fait", summary);
        Assert.Contains("1 non résolues", summary);
    }

    [Fact]
    public void MultiLevel_Operations_ShouldMaintainSeparation()
    {
        // Arrange
        var eventFact = Fact.Create("Combat", FactType.Event, MemoryLevel.Event, new[] { "Aric", "Tower" });
        var chapterFact = Fact.Create("Chapter arc summary", FactType.Knowledge, MemoryLevel.Chapter, new[] { "Summary" });
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test")
            .AddFact(MemoryLevel.Event, eventFact)
            .AddFact(MemoryLevel.Chapter, chapterFact);

        // Act
        var eventFacts = memorandum.GetFacts(MemoryLevel.Event);
        var chapterFacts = memorandum.GetFacts(MemoryLevel.Chapter);

        // Assert
        Assert.Single(eventFacts);
        Assert.Single(chapterFacts);
        Assert.Contains(eventFact, eventFacts);
        Assert.Contains(chapterFact, chapterFacts);
        Assert.DoesNotContain(chapterFact, eventFacts);
    }
}
