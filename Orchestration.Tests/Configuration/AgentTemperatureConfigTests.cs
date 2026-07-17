using FluentAssertions;
using Narratum.Orchestration.Configuration;
using Narratum.Orchestration.Models;
using Xunit;

namespace Narratum.Orchestration.Tests.Configuration;

public class AgentTemperatureConfigTests
{
    [Fact]
    public void Default_ReturnsValidConfiguration()
    {
        // Act
        var config = AgentTemperatureConfig.Default;

        // Assert
        config.Should().NotBeNull();
        config.NarratorTemperature.Should().Be(0.7);
        config.CharacterTemperature.Should().Be(0.8);
        config.SummaryTemperature.Should().Be(0.3);
        config.ConsistencyTemperature.Should().Be(0.1);
    }

    [Fact]
    public void Conservative_ReturnsLowerTemperatures()
    {
        // Act
        var config = AgentTemperatureConfig.Conservative;

        // Assert
        config.NarratorTemperature.Should().BeLessThan(AgentTemperatureConfig.Default.NarratorTemperature);
        config.CharacterTemperature.Should().BeLessThan(AgentTemperatureConfig.Default.CharacterTemperature);
        config.SummaryTemperature.Should().BeLessThanOrEqualTo(AgentTemperatureConfig.Default.SummaryTemperature);
        config.ConsistencyTemperature.Should().Be(0.0);
    }

    [Fact]
    public void Creative_ReturnsHigherTemperatures()
    {
        // Act
        var config = AgentTemperatureConfig.Creative;

        // Assert
        config.NarratorTemperature.Should().BeGreaterThan(AgentTemperatureConfig.Default.NarratorTemperature);
        config.CharacterTemperature.Should().BeGreaterThan(AgentTemperatureConfig.Default.CharacterTemperature);
        config.SummaryTemperature.Should().BeGreaterThan(AgentTemperatureConfig.Default.SummaryTemperature);
    }

    [Fact]
    public void GetTemperature_WithNarratorType_ReturnsNarratorTemperature()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var temperature = config.GetTemperature(AgentType.Narrator);

        // Assert
        temperature.Should().Be(config.NarratorTemperature);
    }

    [Fact]
    public void GetTemperature_WithCharacterType_ReturnsCharacterTemperature()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var temperature = config.GetTemperature(AgentType.Character);

        // Assert
        temperature.Should().Be(config.CharacterTemperature);
    }

    [Fact]
    public void GetTemperature_WithSummaryType_ReturnsSummaryTemperature()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var temperature = config.GetTemperature(AgentType.Summary);

        // Assert
        temperature.Should().Be(config.SummaryTemperature);
    }

    [Fact]
    public void GetTemperature_WithConsistencyType_ReturnsConsistencyTemperature()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var temperature = config.GetTemperature(AgentType.Consistency);

        // Assert
        temperature.Should().Be(config.ConsistencyTemperature);
    }

    [Fact]
    public void IsValid_WithDefaultConfig_ReturnsTrue()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithValidCustomConfig_ReturnsTrue()
    {
        // Arrange
        var config = new AgentTemperatureConfig
        {
            NarratorTemperature = 0.5,
            CharacterTemperature = 1.0,
            SummaryTemperature = 0.2,
            ConsistencyTemperature = 0.0
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithNegativeTemperature_ReturnsFalse()
    {
        // Arrange
        var config = new AgentTemperatureConfig
        {
            NarratorTemperature = -0.5 // Invalid
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithTooHighTemperature_ReturnsFalse()
    {
        // Arrange
        var config = new AgentTemperatureConfig
        {
            CharacterTemperature = 2.5 // Invalid (>2.0)
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithZeroTemperatures_ReturnsTrue()
    {
        // Arrange
        var config = new AgentTemperatureConfig
        {
            NarratorTemperature = 0.0,
            CharacterTemperature = 0.0,
            SummaryTemperature = 0.0,
            ConsistencyTemperature = 0.0
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithMaxTemperatures_ReturnsTrue()
    {
        // Arrange
        var config = new AgentTemperatureConfig
        {
            NarratorTemperature = 2.0,
            CharacterTemperature = 2.0,
            SummaryTemperature = 2.0,
            ConsistencyTemperature = 2.0
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Record_IsImmutable()
    {
        // Arrange
        var config = AgentTemperatureConfig.Default;

        // Act
        var modified = config with { NarratorTemperature = 1.5 };

        // Assert
        config.NarratorTemperature.Should().Be(0.7); // Original unchanged
        modified.NarratorTemperature.Should().Be(1.5); // New instance modified
    }
}
