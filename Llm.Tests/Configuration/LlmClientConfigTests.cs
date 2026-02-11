using FluentAssertions;
using Narratum.Llm.Configuration;
using Narratum.Orchestration.Stages;
using Xunit;

namespace Narratum.Llm.Tests.Configuration;

/// <summary>
/// Tests unitaires pour LlmClientConfig et le routing de modèle par agent.
/// </summary>
public sealed class LlmClientConfigTests
{
    [Fact]
    public void DefaultFoundryLocal_ShouldHaveCorrectDefaults()
    {
        var config = LlmClientConfig.DefaultFoundryLocal;

        config.Provider.Should().Be(LlmProviderType.FoundryLocal);
        config.DefaultModel.Should().Be("phi-4-mini");
        config.NarratorModel.Should().BeNull();
        config.AgentModelMapping.Should().BeEmpty();
        config.BaseUrl.Should().BeNull();
    }

    [Fact]
    public void DefaultOllama_ShouldHaveCorrectDefaults()
    {
        var config = LlmClientConfig.DefaultOllama;

        config.Provider.Should().Be(LlmProviderType.Ollama);
        config.DefaultModel.Should().Be("phi4-mini");
        config.BaseUrl.Should().Be("http://localhost:11434/v1");
        config.NarratorModel.Should().BeNull();
    }

    [Fact]
    public void ResolveModel_WithNoMappingOrOverride_ReturnsDefaultModel()
    {
        var config = new LlmClientConfig { DefaultModel = "test-model" };

        config.ResolveModel(AgentType.Summary).Should().Be("test-model");
        config.ResolveModel(AgentType.Narrator).Should().Be("test-model");
        config.ResolveModel(AgentType.Character).Should().Be("test-model");
        config.ResolveModel(AgentType.Consistency).Should().Be("test-model");
    }

    [Fact]
    public void ResolveModel_WithAgentMapping_ReturnsAgentSpecificModel()
    {
        var config = new LlmClientConfig
        {
            DefaultModel = "default-model",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Summary] = "summary-model",
                [AgentType.Character] = "character-model"
            }
        };

        config.ResolveModel(AgentType.Summary).Should().Be("summary-model");
        config.ResolveModel(AgentType.Character).Should().Be("character-model");
        config.ResolveModel(AgentType.Narrator).Should().Be("default-model");
        config.ResolveModel(AgentType.Consistency).Should().Be("default-model");
    }

    [Fact]
    public void ResolveModel_NarratorModel_OverridesAgentMapping()
    {
        var config = new LlmClientConfig
        {
            DefaultModel = "default-model",
            NarratorModel = "narrator-override",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Narrator] = "should-be-ignored"
            }
        };

        config.ResolveModel(AgentType.Narrator).Should().Be("narrator-override");
    }

    [Fact]
    public void ResolveModel_NarratorModelEmpty_FallsToAgentMapping()
    {
        var config = new LlmClientConfig
        {
            DefaultModel = "default-model",
            NarratorModel = "",
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Narrator] = "narrator-from-mapping"
            }
        };

        config.ResolveModel(AgentType.Narrator).Should().Be("narrator-from-mapping");
    }

    [Fact]
    public void ResolveModel_NarratorModelNull_FallsToAgentMapping()
    {
        var config = new LlmClientConfig
        {
            DefaultModel = "default-model",
            NarratorModel = null,
            AgentModelMapping = new Dictionary<AgentType, string>
            {
                [AgentType.Narrator] = "narrator-from-mapping"
            }
        };

        config.ResolveModel(AgentType.Narrator).Should().Be("narrator-from-mapping");
    }

    [Fact]
    public void ResolveModel_NarratorModelDoesNotAffectOtherAgents()
    {
        var config = new LlmClientConfig
        {
            DefaultModel = "default-model",
            NarratorModel = "narrator-special"
        };

        config.ResolveModel(AgentType.Summary).Should().Be("default-model");
        config.ResolveModel(AgentType.Character).Should().Be("default-model");
        config.ResolveModel(AgentType.Consistency).Should().Be("default-model");
    }

    [Fact]
    public void TimeoutSeconds_HasReasonableDefault()
    {
        var config = new LlmClientConfig();
        config.TimeoutSeconds.Should().BeGreaterOrEqualTo(30);
    }

    [Fact]
    public void FoundryLocalConfig_HasReasonableDefaults()
    {
        var config = new FoundryLocalConfig();

        config.ModelAlias.Should().Be("phi-4-mini");
        config.AutoDownload.Should().BeTrue();
        config.CacheDirectory.Should().BeNull();
        config.ServiceTtl.Should().BeNull();
    }
}
