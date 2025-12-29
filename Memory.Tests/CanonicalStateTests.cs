using Narratum.Memory;

namespace Narratum.Memory.Tests;

public class CanonicalStateTests
{
    [Fact]
    public void CreateEmpty_ShouldCreateValidState()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var memoryLevel = MemoryLevel.Event;

        // Act
        var state = CanonicalState.CreateEmpty(worldId, memoryLevel);

        // Assert
        Assert.NotEqual(Guid.Empty, state.Id);
        Assert.Equal(worldId, state.WorldId);
        Assert.Equal(memoryLevel, state.MemoryLevel);
        Assert.Empty(state.Facts);
        Assert.Equal(1, state.Version);
        Assert.NotNull(state.LastUpdated);
    }

    [Fact]
    public void AddFact_ShouldIncreaseVersionAndUpdateTimestamp()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);
        var fact = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var initialTimestamp = state.LastUpdated;

        // Act
        var newState = state.AddFact(fact);

        // Assert
        Assert.NotEqual(state, newState);
        Assert.Equal(state.Version + 1, newState.Version);
        Assert.True(newState.LastUpdated >= initialTimestamp);
        Assert.Single(newState.Facts);
    }

    [Fact]
    public void AddFact_WithInvalidFact_ShouldThrowException()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);
        var invalidFact = new Fact(
            Id: Guid.NewGuid(),
            Content: "",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>()
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => state.AddFact(invalidFact));
    }

    [Fact]
    public void AddFacts_ShouldAddMultipleFacts()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);
        var facts = new[]
        {
            Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" }),
            Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" }),
            Fact.Create("Lyra trusts Aric", FactType.Relationship, MemoryLevel.Event, new[] { "Lyra", "Aric" })
        };

        // Act
        var newState = state.AddFacts(facts);

        // Assert
        Assert.Equal(3, newState.Facts.Count);
        Assert.Equal(state.Version + 1, newState.Version);
    }

    [Fact]
    public void RemoveFact_ShouldRemoveSpecificFact()
    {
        // Arrange
        var fact1 = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var fact2 = Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" });
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event)
            .AddFacts(new[] { fact1, fact2 });

        // Act
        var newState = state.RemoveFact(fact1.Id);

        // Assert
        Assert.Single(newState.Facts);
        Assert.Contains(fact2, newState.Facts);
        Assert.DoesNotContain(fact1, newState.Facts);
    }

    [Fact]
    public void GetFactsForEntity_ShouldReturnOnlyEntityFacts()
    {
        // Arrange
        var fact1 = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var fact2 = Fact.Create("Lyra is alive", FactType.CharacterState, MemoryLevel.Event, new[] { "Lyra" });
        var fact3 = Fact.Create("Aric trusts Lyra", FactType.Relationship, MemoryLevel.Event, new[] { "Aric", "Lyra" });
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event)
            .AddFacts(new[] { fact1, fact2, fact3 });

        // Act
        var aricFacts = state.GetFactsForEntity("Aric");

        // Assert
        Assert.Equal(2, aricFacts.Count());
        Assert.Contains(fact1, aricFacts);
        Assert.Contains(fact3, aricFacts);
        Assert.DoesNotContain(fact2, aricFacts);
    }

    [Fact]
    public void GetFactsByType_ShouldReturnOnlyTypeFacts()
    {
        // Arrange
        var fact1 = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });
        var fact2 = Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" });
        var fact3 = Fact.Create("Combat occurred", FactType.Event, MemoryLevel.Event, new[] { "Aric", "Tower" });
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event)
            .AddFacts(new[] { fact1, fact2, fact3 });

        // Act
        var statesFacts = state.GetFactsByType(FactType.CharacterState);

        // Assert
        Assert.Single(statesFacts);
        Assert.Contains(fact1, statesFacts);
    }

    [Fact]
    public void CanonicalState_IsImmutable()
    {
        // Arrange
        var state1 = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event);
        var fact = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });

        // Act
        var state2 = state1.AddFact(fact);

        // Assert
        Assert.Empty(state1.Facts);
        Assert.Single(state2.Facts);
    }

    [Fact]
    public void Validate_WithValidState_ShouldReturnTrue()
    {
        // Arrange
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event)
            .AddFact(Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" }));

        // Act
        var isValid = state.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void FactCount_ShouldReturnCorrectNumber()
    {
        // Arrange
        var facts = new[]
        {
            Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" }),
            Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Event, new[] { "Tower" }),
            Fact.Create("Lyra trusts Aric", FactType.Relationship, MemoryLevel.Event, new[] { "Lyra", "Aric" })
        };
        var state = CanonicalState.CreateEmpty(Guid.NewGuid(), MemoryLevel.Event)
            .AddFacts(facts);

        // Act
        var count = state.FactCount;

        // Assert
        Assert.Equal(3, count);
    }
}
