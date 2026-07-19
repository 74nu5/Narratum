using FluentAssertions;
using Narratum.Llm.Clients;
using Xunit;

namespace Narratum.Llm.Tests.Clients;

/// <summary>
/// The catalogue that decides which sampling parameters go into a request. Its whole point is to
/// drop only what a model refuses — dropping everything was the old behaviour and silently threw
/// away the per-agent temperature tuning.
/// </summary>
public sealed class ModelParameterCapabilitiesTests
{
    [Fact]
    public void UnknownModel_AssumesEverythingIsSupported()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.GetUnsupported("phi-4-mini").Should().Be(SamplingParameter.None);
    }

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5-mini")]
    [InlineData("my-gpt-5-prod")]
    [InlineData("o3-mini")]
    [InlineData("o1")]
    public void ReasoningFamilies_AreSeeded_SoTheFirstCallSkipsTheRejection(string modelId)
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.GetUnsupported(modelId)
            .Should().Be(SamplingParameter.Temperature | SamplingParameter.TopP);
    }

    [Fact]
    public void Seeding_LeavesTheTokenCapAlone_OnReasoningModels()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.GetUnsupported("gpt-5-mini")
            .HasFlag(SamplingParameter.MaxOutputTokens).Should().BeFalse();
    }

    [Fact]
    public void MistralFamily_IsSeededWithoutTheTokenCap()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.GetUnsupported("mistral-large-2411")
            .Should().Be(SamplingParameter.MaxOutputTokens);
    }

    [Fact]
    public void Learn_DropsOnlyTheNamedParameter()
    {
        var capabilities = new ModelParameterCapabilities();

        var learned = capabilities.Learn(
            "picky-model",
            "Unsupported value: 'temperature' does not support 0.7 with this model. Only the default (1) value is supported.");

        learned.Should().BeTrue();
        capabilities.GetUnsupported("picky-model").Should().Be(SamplingParameter.Temperature);
    }

    [Theory]
    [InlineData("max_tokens")]
    [InlineData("max_completion_tokens")]
    [InlineData("max_output_tokens")]
    public void Learn_MapsEveryTokenCapSpelling_ToTheSameKnob(string wireName)
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.Learn("some-model", $"Unsupported parameter: '{wireName}' is not supported with this model.");

        capabilities.GetUnsupported("some-model").Should().Be(SamplingParameter.MaxOutputTokens);
    }

    [Fact]
    public void Learn_Accumulates_WhenAModelRefusesOneParameterAtATime()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.Learn("fussy", "Unsupported value: 'temperature' ...").Should().BeTrue();
        capabilities.Learn("fussy", "Unsupported value: 'top_p' ...").Should().BeTrue();

        capabilities.GetUnsupported("fussy")
            .Should().Be(SamplingParameter.Temperature | SamplingParameter.TopP);
    }

    [Fact]
    public void Learn_ReturnsFalse_WhenItLearnsNothingNew()
    {
        // This is what stops the adapter retrying forever against a model that keeps saying 400.
        var capabilities = new ModelParameterCapabilities();
        capabilities.Learn("fussy", "Unsupported value: 'temperature' ...");

        capabilities.Learn("fussy", "Unsupported value: 'temperature' ...").Should().BeFalse();
    }

    [Fact]
    public void Learn_FallsBackToDroppingEverything_WhenTheMessageNamesNothing()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.Learn("cryptic", "Bad Request").Should().BeTrue();
        capabilities.GetUnsupported("cryptic").Should().Be(SamplingParameter.All);
    }

    [Fact]
    public void Learn_IgnoresUnquotedProse_SoStopIsNotMisread()
    {
        // "stop" is an ordinary word; only the quoted form names a parameter.
        var capabilities = new ModelParameterCapabilities();

        capabilities.Learn("chatty", "The model had to stop: 'temperature' is not supported.");

        capabilities.GetUnsupported("chatty").Should().Be(SamplingParameter.Temperature);
    }

    [Fact]
    public void Learn_CorrectsTheSeedTable_WhenADeploymentDisagrees()
    {
        var capabilities = new ModelParameterCapabilities();

        capabilities.Learn("gpt-5-mini", "Unsupported parameter: 'max_completion_tokens' ...").Should().BeTrue();

        capabilities.GetUnsupported("gpt-5-mini").Should().Be(
            SamplingParameter.Temperature | SamplingParameter.TopP | SamplingParameter.MaxOutputTokens);
    }
}
