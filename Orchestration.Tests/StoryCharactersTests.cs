using FluentAssertions;

using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Models;

using Xunit;

namespace Narratum.Orchestration.Tests;

public sealed class StoryCharactersTests
{
    [Fact]
    public void Clean_DropsBlankNames_AndTrims()
    {
        var input = new[]
        {
            new CharacterProfile("  Maelis  ", " Gardienne ", " Lit les marées ", "", ["  fait A  ", "  "]),
            new CharacterProfile("   ", "role", "desc", "evo", []),
        };

        var result = StoryCharacters.Clean(input);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Maelis");
        result[0].Role.Should().Be("Gardienne");
        result[0].Description.Should().Be("Lit les marées");
        result[0].KeyFacts.Should().Equal("fait A");
    }

    [Fact]
    public void Clean_DeduplicatesByNameCaseInsensitive()
    {
        var input = new[]
        {
            new CharacterProfile("Aric", "héros", "", "", []),
            new CharacterProfile("aric", "double", "", "", []),
        };

        StoryCharacters.Clean(input).Should().ContainSingle().Which.Role.Should().Be("héros");
    }

    [Fact]
    public void Clean_NullYieldsEmpty()
    {
        StoryCharacters.Clean(null).Should().BeEmpty();
    }

    [Fact]
    public void CharacterRoster_DeserializesFromModelJson()
    {
        const string json =
            "{\"characters\":[{\"name\":\"Le Gardien\",\"role\":\"mentor\",\"description\":\"vieux sage\"," +
            "\"evolution\":\"se révèle\",\"keyFacts\":[\"connaît le secret\"]}]}";

        var ok = StructuredLlm.TryDeserialize<CharacterRoster>(json, out var roster);

        ok.Should().BeTrue();
        roster!.Characters.Should().ContainSingle();
        roster.Characters[0].Name.Should().Be("Le Gardien");
        roster.Characters[0].KeyFacts.Should().Equal("connaît le secret");
    }
}
