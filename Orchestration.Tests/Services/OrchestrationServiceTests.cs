using FluentAssertions;
using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Services;
using Xunit;

namespace Narratum.Orchestration.Tests.Services;

/// <summary>
/// Tests unitaires pour OrchestrationService.
/// Ces tests valident le principe Phase 3 : "Le système fonctionne même avec un LLM stupide."
/// </summary>
public class OrchestrationServiceTests
{
    private readonly StoryState _testState;
    private readonly NarrativeIntent _testIntent;

    public OrchestrationServiceTests()
    {
        _testState = StoryState.Create(
            worldId: Id.New(),
            worldName: "Test World");
        _testIntent = NarrativeIntent.Continue();
    }

    [Fact]
    public void Constructor_WithMockClient_ShouldCreateService()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);

        // Act
        var service = new OrchestrationService(client);

        // Assert
        service.Should().NotBeNull();
        service.Config.Should().Be(OrchestrationConfig.Default);
    }

    [Fact]
    public void Constructor_WithConfig_ShouldUseConfig()
    {
        // Arrange
        var config = OrchestrationConfig.ForTesting;
        var client = new MockLlmClient();

        // Act
        var service = new OrchestrationService(client, config);

        // Assert
        service.Config.Should().Be(config);
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act
        var action = () => new OrchestrationService(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithMockLlm_ShouldSucceed()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var service = new OrchestrationService(client, OrchestrationConfig.ForTesting);

        // Act
        var result = await service.ExecuteCycleAsync(_testState, _testIntent);

        // Assert
        result.Should().BeOfType<Result<PipelineResult>.Success>();
        var pipelineResult = ((Result<PipelineResult>.Success)result).Value;
        pipelineResult.IsSuccess.Should().BeTrue();
        pipelineResult.Output.Should().NotBeNull();
        pipelineResult.Output!.NarrativeText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithStupidLlm_ShouldStillWork()
    {
        // Arrange - Le test crucial de Phase 3
        var stupidClient = new StupidLlmClient();
        var service = new OrchestrationService(stupidClient, OrchestrationConfig.ForTesting);

        // Act
        var result = await service.ExecuteCycleAsync(_testState, _testIntent);

        // Assert - Le pipeline DOIT fonctionner même avec un LLM "stupide"
        result.Should().BeOfType<Result<PipelineResult>.Success>();
        var pipelineResult = ((Result<PipelineResult>.Success)result).Value;
        pipelineResult.IsSuccess.Should().BeTrue();
        pipelineResult.Output!.NarrativeText.Should().Be("TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE");
    }

    [Fact]
    public async Task ExecuteCycleAsync_ShouldRecordStageResults()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var service = new OrchestrationService(client, OrchestrationConfig.ForTesting);

        // Act
        var result = await service.ExecuteCycleAsync(_testState, _testIntent);

        // Assert
        var pipelineResult = ((Result<PipelineResult>.Success)result).Value;
        pipelineResult.StageResults.Should().NotBeEmpty();
        pipelineResult.StageResults.Should().Contain(s => s.StageName == "BuildContext");
        pipelineResult.StageResults.Should().Contain(s => s.StageName == "PreparePrompt");
        pipelineResult.StageResults.Should().Contain(s => s.StageName == "Generate");
    }

    [Fact]
    public async Task ExecuteCycleAsync_ShouldRecordDuration()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var service = new OrchestrationService(client, OrchestrationConfig.ForTesting);

        // Act
        var result = await service.ExecuteCycleAsync(_testState, _testIntent);

        // Assert
        var pipelineResult = ((Result<PipelineResult>.Success)result).Value;
        pipelineResult.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithNullState_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);

        // Act
        var action = async () => await service.ExecuteCycleAsync(null!, _testIntent);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithNullIntent_ShouldThrow()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);

        // Act
        var action = async () => await service.ExecuteCycleAsync(_testState, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildContextAsync_ShouldCreateValidContext()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);

        // Act
        var result = await service.BuildContextAsync(_testState, _testIntent);

        // Assert
        result.Should().BeOfType<Result<PipelineContext>.Success>();
        var context = ((Result<PipelineContext>.Success)result).Value;
        context.StoryState.Should().Be(_testState);
        context.Intent.Should().Be(_testIntent);
    }

    [Fact]
    public async Task BuildContextAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);

        // Act
        var result = await service.BuildContextAsync(_testState, _testIntent);

        // Assert
        var context = ((Result<PipelineContext>.Success)result).Value;
        context.Metadata.Should().ContainKey("builtAt");
        context.Metadata.Should().ContainKey("hasMemory");
    }

    [Fact]
    public void ValidateOutput_WithValidOutput_ShouldSucceed()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);
        var output = new NarrativeOutput("This is a valid narrative text that is long enough.");
        var context = PipelineContext.CreateMinimal(_testState, _testIntent);

        // Act
        var result = service.ValidateOutput(output, context);

        // Assert
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void ValidateOutput_WithEmptyText_ShouldFail()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);
        var output = new NarrativeOutput("");
        var context = PipelineContext.CreateMinimal(_testState, _testIntent);

        // Act
        var result = service.ValidateOutput(output, context);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
    }

    [Fact]
    public void ValidateOutput_WithTooShortText_ShouldFail()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);
        var output = new NarrativeOutput("Short");
        var context = PipelineContext.CreateMinimal(_testState, _testIntent);

        // Act
        var result = service.ValidateOutput(output, context);

        // Assert
        result.Should().BeOfType<Result<Unit>.Failure>();
        var failure = (Result<Unit>.Failure)result;
        failure.Message.Should().Contain("short");
    }

    [Fact]
    public async Task IsReadyAsync_WithHealthyClient_ShouldReturnTrue()
    {
        // Arrange
        var client = new MockLlmClient();
        var service = new OrchestrationService(client);

        // Act
        var isReady = await service.IsReadyAsync();

        // Assert
        isReady.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCycleAsync_OutputShouldIncludeLlmMetadata()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var service = new OrchestrationService(client, OrchestrationConfig.ForTesting);

        // Act
        var result = await service.ExecuteCycleAsync(_testState, _testIntent);

        // Assert
        var pipelineResult = ((Result<PipelineResult>.Success)result).Value;
        pipelineResult.Output!.Metadata.Should().ContainKey("isMock");
        pipelineResult.Output.Metadata["isMock"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithDifferentIntents_ShouldWork()
    {
        // Arrange
        var client = new MockLlmClient(MockLlmConfig.ForTesting);
        var service = new OrchestrationService(client, OrchestrationConfig.ForTesting);

        var intents = new[]
        {
            NarrativeIntent.Continue(),
            new NarrativeIntent(IntentType.GenerateDialogue),
            new NarrativeIntent(IntentType.DescribeScene),
            new NarrativeIntent(IntentType.CreateTension)
        };

        // Act & Assert
        foreach (var intent in intents)
        {
            var result = await service.ExecuteCycleAsync(_testState, intent);
            result.Should().BeOfType<Result<PipelineResult>.Success>();
        }
    }
}

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
