using FluentAssertions;
using Narratum.Orchestration.Services;
using Xunit;

namespace Narratum.Orchestration.Tests.Services;

/// <summary>
/// Tests de configuration pour OrchestrationConfig.
/// </summary>
public class OrchestrationConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = OrchestrationConfig.Default;

        // Assert
        config.MaxRetries.Should().Be(3);
        config.StageTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.GlobalTimeout.Should().Be(TimeSpan.FromMinutes(2));
        config.EnableDetailedLogging.Should().BeFalse();
        config.UseMockAgents.Should().BeTrue();
    }

    [Fact]
    public void ForTesting_ShouldHaveTestingDefaults()
    {
        // Act
        var config = OrchestrationConfig.ForTesting;

        // Assert
        config.MaxRetries.Should().Be(1);
        config.StageTimeout.Should().Be(TimeSpan.FromSeconds(5));
        config.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.EnableDetailedLogging.Should().BeTrue();
        config.UseMockAgents.Should().BeTrue();
    }

    [Fact]
    public void Init_ShouldAllowCustomValues()
    {
        // Act
        var config = new OrchestrationConfig
        {
            MaxRetries = 5,
            StageTimeout = TimeSpan.FromSeconds(60),
            EnableDetailedLogging = true
        };

        // Assert
        config.MaxRetries.Should().Be(5);
        config.StageTimeout.Should().Be(TimeSpan.FromSeconds(60));
        config.EnableDetailedLogging.Should().BeTrue();
    }
}
