using Narratum.Memory;

namespace Narratum.Memory.Tests;

public class FactTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateFact()
    {
        // Arrange
        var content = "Aric is dead";
        var factType = FactType.CharacterState;
        var memoryLevel = MemoryLevel.Event;
        var entities = new[] { "Aric" };

        // Act
        var fact = Fact.Create(content, factType, memoryLevel, entities);

        // Assert
        Assert.NotEqual(Guid.Empty, fact.Id);
        Assert.Equal(content, fact.Content);
        Assert.Equal(factType, fact.FactType);
        Assert.Equal(memoryLevel, fact.MemoryLevel);
        Assert.Contains("Aric", fact.EntityReferences);
        Assert.NotNull(fact.CreatedAt);
    }

    [Fact]
    public void Fact_IsImmutable_ShouldNotModifyOriginal()
    {
        // Arrange
        var fact1 = Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" });

        // Act
        var fact2 = fact1 with { Content = "Aric is alive", Id = Guid.NewGuid() };

        // Assert
        Assert.Equal("Aric is dead", fact1.Content);
        Assert.Equal("Aric is alive", fact2.Content);
        Assert.NotEqual(fact1.Id, fact2.Id);
    }

    [Fact]
    public void Validate_WithValidFact_ShouldReturnTrue()
    {
        // Arrange
        var fact = Fact.Create("Tower is destroyed", FactType.LocationState, MemoryLevel.Chapter, new[] { "Tower" });

        // Act
        var isValid = fact.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithEmptyContent_ShouldReturnFalse()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>()
        );

        // Act
        var isValid = fact.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithInvalidConfidence_ShouldReturnFalse()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "Valid content",
            FactType: FactType.Event,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>(),
            Confidence: 1.5
        );

        // Act
        var isValid = fact.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithCharacterStateAndNoEntities_ShouldReturnFalse()
    {
        // Arrange
        var fact = new Fact(
            Id: Guid.NewGuid(),
            Content: "Character state without entity",
            FactType: FactType.CharacterState,
            MemoryLevel: MemoryLevel.Event,
            EntityReferences: new HashSet<string>()
        );

        // Act
        var isValid = fact.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Create_WithMultipleEntities_ShouldIncludeAll()
    {
        // Arrange
        var entities = new[] { "Aric", "Lyra", "Tower" };

        // Act
        var fact = Fact.Create("Aric and Lyra are at the Tower", FactType.Event, MemoryLevel.Event, entities);

        // Assert
        Assert.Contains("Aric", fact.EntityReferences);
        Assert.Contains("Lyra", fact.EntityReferences);
        Assert.Contains("Tower", fact.EntityReferences);
    }

    [Fact]
    public void Fact_WithCustomConfidence_ShouldPreserveValue()
    {
        // Arrange
        const double confidence = 0.85;

        // Act
        var fact = Fact.Create("Uncertain fact", FactType.Knowledge, MemoryLevel.Event, new[] { "Crystal" }, confidence: confidence);

        // Assert
        Assert.Equal(confidence, fact.Confidence);
    }
}
