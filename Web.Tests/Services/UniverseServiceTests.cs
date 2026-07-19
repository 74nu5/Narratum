using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Narratum.Core;
using Narratum.Web.Models;
using Narratum.Web.Services;
using Xunit;

namespace Narratum.Web.Tests.Services;

/// <summary>
/// Promoting a character a run invented into the universe's cast — the way canon born mid-story
/// survives the story that invented it.
/// </summary>
public sealed class UniverseServiceTests
{
    private const string UniverseId = "univers-1";

    private readonly Mock<IUniverseRepository> _repository = new();
    private readonly UniverseService _service;

    public UniverseServiceTests()
        => _service = new UniverseService(_repository.Object, NullLogger<UniverseService>.Instance);

    private void GivenUniverseWithCast(string serializedCharacters)
        => _repository
            .Setup(r => r.GetAsync(UniverseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Universe(
                UniverseId, "Le Pont des Verriers", "Fantasy", null, null,
                serializedCharacters, "[]", null, null, DateTime.UtcNow));

    [Fact]
    public async Task AddCharacterAsync_AppendsToTheCast_KeepingWhoWasAlreadyThere()
    {
        GivenUniverseWithCast("""[{"Name":"Livia","Description":"Souffleuse"}]""");

        Universe? saved = null;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Universe>(), It.IsAny<CancellationToken>()))
            .Callback<Universe, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var added = await _service.AddCharacterAsync(UniverseId, new WorldCharacter("Maître Orvane", "Doyen"));

        added.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.SerializedCharacters.Should().Contain("Livia").And.Contain("Orvane");
    }

    [Theory]
    [InlineData("Maître Orvane")]
    [InlineData("maître orvane")]
    public async Task AddCharacterAsync_IsIdempotent_WhenTheCastAlreadyKnowsTheName(string name)
    {
        // Clicking twice must not duplicate the character.
        GivenUniverseWithCast("""[{"Name":"Maître Orvane","Description":"Doyen"}]""");

        var added = await _service.AddCharacterAsync(UniverseId, new WorldCharacter(name, "Doyen"));

        added.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Universe>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCharacterAsync_RefusesANamelessCharacter()
    {
        var added = await _service.AddCharacterAsync(UniverseId, new WorldCharacter("  ", "Sans nom"));

        added.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Universe>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCharacterAsync_ReturnsFalse_WhenTheUniverseIsGone()
    {
        _repository
            .Setup(r => r.GetAsync(UniverseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Universe?)null);

        (await _service.AddCharacterAsync(UniverseId, new WorldCharacter("Orvane"))).Should().BeFalse();
    }
}
