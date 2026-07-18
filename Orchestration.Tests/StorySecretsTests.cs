using FluentAssertions;

using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;

using Xunit;

namespace Narratum.Orchestration.Tests;

public sealed class StorySecretsTests
{
    [Fact]
    public void Clean_DropsBlankContent_AndTrims()
    {
        var input = new[]
        {
            new StorySecret("  Le pont est piégé  ", "plot", true),
            new StorySecret("   ", "plot", false),
        };

        var result = StorySecrets.Clean(input);

        result.Should().ContainSingle();
        result[0].Content.Should().Be("Le pont est piégé");
        result[0].IsRevealed.Should().BeTrue();
    }

    [Theory]
    [InlineData("PLOT", "plot")]
    [InlineData("Character", "character")]
    [InlineData("location", "location")]
    [InlineData("weird", "plot")]   // unknown category defaults to plot
    [InlineData("", "plot")]
    public void Clean_NormalizesCategory(string input, string expected)
    {
        var result = StorySecrets.Clean([new StorySecret("un secret", input, false)]);

        result.Should().ContainSingle().Which.Category.Should().Be(expected);
    }

    [Fact]
    public void Clean_NullYieldsEmpty()
    {
        StorySecrets.Clean(null).Should().BeEmpty();
    }

    [Fact]
    public void SecretSet_DeserializesFromModelJson()
    {
        const string json =
            "{\"secrets\":[{\"content\":\"Le gardien ment\",\"category\":\"character\",\"isRevealed\":false}]}";

        var ok = StructuredLlm.TryDeserialize<SecretSet>(json, out var set);

        ok.Should().BeTrue();
        set!.Secrets.Should().ContainSingle();
        set.Secrets[0].IsRevealed.Should().BeFalse();
        set.Secrets[0].Category.Should().Be("character");
    }
}
