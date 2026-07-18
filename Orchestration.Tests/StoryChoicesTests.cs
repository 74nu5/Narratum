using System.Text.Json;

using FluentAssertions;

using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;

using Xunit;

namespace Narratum.Orchestration.Tests;

public sealed class StoryChoicesTests
{
    [Fact]
    public void NormalizeToThree_PadsWhenTooFew()
    {
        var input = new[] { new StoryChoice("Avancer", "Aller de l'avant") };

        var result = StoryChoices.NormalizeToThree(input);

        result.Should().HaveCount(3);
        result[0].Text.Should().Be("Avancer");
        result[1].Should().Be(StoryChoices.Fallback[1]);
        result[2].Should().Be(StoryChoices.Fallback[2]);
    }

    [Fact]
    public void NormalizeToThree_TruncatesWhenTooMany()
    {
        var input = Enumerable.Range(1, 5)
            .Select(i => new StoryChoice($"Action {i}", $"Conséquence {i}"))
            .ToList();

        var result = StoryChoices.NormalizeToThree(input);

        result.Should().HaveCount(3);
        result.Select(c => c.Text).Should().Equal("Action 1", "Action 2", "Action 3");
    }

    [Fact]
    public void NormalizeToThree_DropsBlankAndTrims()
    {
        var input = new[]
        {
            new StoryChoice("  Fuir  ", "  Prendre la fuite  "),
            new StoryChoice("   ", "sans action"),
        };

        var result = StoryChoices.NormalizeToThree(input);

        result.Should().HaveCount(3);
        result[0].Should().Be(new StoryChoice("Fuir", "Prendre la fuite"));
        result[1].Should().Be(StoryChoices.Fallback[1], "the blank entry is dropped and padded");
    }

    [Fact]
    public void NormalizeToThree_NullYieldsThreeFallbacks()
    {
        StoryChoices.NormalizeToThree(null).Should().Equal(StoryChoices.Fallback);
    }

    [Fact]
    public void ProposedChoices_DeserializesFromModelJson()
    {
        const string modelJson =
            "{\"choices\":[{\"text\":\"Ouvrir la porte\",\"description\":\"Découvrir la pièce\"}]}";

        var ok = StructuredLlm.TryDeserialize<ProposedChoices>(modelJson, out var proposed);

        ok.Should().BeTrue();
        proposed!.Choices.Should().ContainSingle();
        proposed.Choices[0].Text.Should().Be("Ouvrir la porte");
    }

    [Fact]
    public void StoryChoice_RoundTripsThroughDefaultSerialization()
    {
        // The persistence path serializes/deserializes with default (PascalCase) options.
        IReadOnlyList<StoryChoice> original = StoryChoices.NormalizeToThree(
            new[] { new StoryChoice("Attendre", "Patienter un tour") });

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<List<StoryChoice>>(json);

        restored.Should().Equal(original);
    }
}
