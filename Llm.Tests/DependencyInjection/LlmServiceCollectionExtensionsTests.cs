using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Narratum.Llm.Configuration;
using Narratum.Llm.DependencyInjection;
using Narratum.Llm.Factory;
using Xunit;

namespace Narratum.Llm.Tests.DependencyInjection;

/// <summary>
/// Tests unitaires pour les extensions d'enregistrement DI.
/// </summary>
public sealed class LlmServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddNarratumLlm_RegistersConfigAndFactory()
    {
        var services = new ServiceCollection();
        var config = LlmClientConfig.DefaultFoundryLocal;

        services.AddNarratumLlm(config);

        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<LlmClientConfig>().Should().BeSameAs(config);
        provider.GetRequiredService<ILlmClientFactory>().Should().BeOfType<LlmClientFactory>();
    }

    [Fact]
    public void AddNarratumLlm_NullConfig_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddNarratumLlm(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddNarratumFoundryLocal_RegistersFoundryLocalConfig()
    {
        var services = new ServiceCollection();

        services.AddNarratumFoundryLocal("test-model", "narrator-model");

        await using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<LlmClientConfig>();

        config.Provider.Should().Be(LlmProviderType.FoundryLocal);
        config.DefaultModel.Should().Be("test-model");
        config.NarratorModel.Should().Be("narrator-model");
        config.FoundryLocal.ModelAlias.Should().Be("test-model");
    }

    [Fact]
    public async Task AddNarratumFoundryLocal_DefaultParams_UsesPhiMini()
    {
        var services = new ServiceCollection();

        services.AddNarratumFoundryLocal();

        await using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<LlmClientConfig>();

        config.DefaultModel.Should().Be("phi-4-mini");
        config.NarratorModel.Should().BeNull();
    }

    [Fact]
    public async Task AddNarratumOllama_RegistersOllamaConfig()
    {
        var services = new ServiceCollection();

        services.AddNarratumOllama("http://custom:1234", "llama3", "gpt-narrator");

        await using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<LlmClientConfig>();

        config.Provider.Should().Be(LlmProviderType.Ollama);
        config.BaseUrl.Should().Be("http://custom:1234");
        config.DefaultModel.Should().Be("llama3");
        config.NarratorModel.Should().Be("gpt-narrator");
    }

    [Fact]
    public async Task AddNarratumOllama_DefaultParams_UsesLocalhost()
    {
        var services = new ServiceCollection();

        services.AddNarratumOllama();

        await using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<LlmClientConfig>();

        config.BaseUrl.Should().Be("http://localhost:11434");
        config.DefaultModel.Should().Be("phi4-mini");
    }

    [Fact]
    public void AddNarratumLlm_CalledTwice_DoesNotDuplicateFactory()
    {
        var services = new ServiceCollection();

        services.AddNarratumLlm(LlmClientConfig.DefaultFoundryLocal);
        services.AddNarratumLlm(LlmClientConfig.DefaultOllama);

        var factoryRegistrations = services
            .Where(s => s.ServiceType == typeof(ILlmClientFactory))
            .ToList();
        factoryRegistrations.Should().HaveCount(1);
    }

    [Fact]
    public void AddNarratumLlm_FactoryIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddNarratumLlm(LlmClientConfig.DefaultFoundryLocal);

        var factoryDescriptor = services
            .First(s => s.ServiceType == typeof(ILlmClientFactory));

        factoryDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
