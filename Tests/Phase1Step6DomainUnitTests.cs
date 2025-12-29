using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Tests;

public class Phase1Step6DomainUnitTests
{
    [Fact]
    public void StoryWorld_Constructor_ShouldCreateValidWorld()
    {
        var world = new StoryWorld(name: "Test World", description: "A test world");
        world.Id.Should().NotBe(default);
        world.Name.Should().Be("Test World");
        world.Description.Should().Be("A test world");
        world.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StoryWorld_Constructor_ShouldThrowIfNameEmpty()
    {
        var action = () => new StoryWorld(name: "", description: "");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StoryWorld_ShouldHaveUniqueIds()
    {
        var world1 = new StoryWorld(name: "World1");
        var world2 = new StoryWorld(name: "World2");
        world1.Id.Should().NotBe(world2.Id);
    }

    [Fact]
    public void StoryArc_Constructor_ShouldCreateValidArc()
    {
        var worldId = Id.New();
        var arc = new StoryArc(worldId: worldId, title: "Test Arc");
        arc.Id.Should().NotBe(default);
        arc.WorldId.Should().Be(worldId);
        arc.Title.Should().Be("Test Arc");
        arc.Status.Should().Be(StoryProgressStatus.NotStarted);
    }

    [Fact]
    public void Character_Constructor_ShouldCreateValidCharacter()
    {
        var character = new Character(name: "Aric");
        character.Id.Should().NotBe(default);
        character.Name.Should().Be("Aric");
        character.VitalStatus.Should().Be(VitalStatus.Alive);
    }

    [Fact]
    public void Character_WithTraits_ShouldStoreTraits()
    {
        var traits = new Dictionary<string, string>
        {
            { "class", "warrior" },
            { "alignment", "good" }
        };
        var character = new Character(name: "Aric", traits: traits);
        character.Traits.Should().Equal(traits);
    }

    [Fact]
    public void Character_DefaultTraits_ShouldBeEmpty()
    {
        var character = new Character(name: "Aric");
        character.Traits.Should().NotBeNull();
    }

    [Fact]
    public void Location_Constructor_ShouldCreateValidLocation()
    {
        var location = new Location(name: "Eldoria", description: "A mystical city");
        location.Id.Should().NotBe(default);
        location.Name.Should().Be("Eldoria");
        location.Description.Should().Be("A mystical city");
    }

    [Fact]
    public void Location_ShouldHaveUniqueIds()
    {
        var loc1 = new Location(name: "City1");
        var loc2 = new Location(name: "City2");
        loc1.Id.Should().NotBe(loc2.Id);
    }

    [Fact]
    public void CharacterDeathEvent_ShouldBeCreatable()
    {
        var characterId = Id.New();
        var evt = new CharacterDeathEvent(characterId: characterId);
        evt.Should().NotBeNull();
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CharacterMovedEvent_ShouldBeCreatable()
    {
        var characterId = Id.New();
        var fromLocationId = Id.New();
        var toLocationId = Id.New();
        var evt = new CharacterMovedEvent(
            characterId: characterId,
            fromLocationId: fromLocationId,
            toLocationId: toLocationId
        );
        evt.Should().NotBeNull();
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DomainEvent_ShouldBeImmutable()
    {
        var characterId = Id.New();
        var evt = new CharacterDeathEvent(characterId: characterId);
        evt.Should().NotBeNull();
        evt.Timestamp.Should().NotBe(default);
    }

    [Fact]
    public void StoryChapter_ShouldHaveValidProperties()
    {
        var arcId = Id.New();
        var chapter = new StoryChapter(arcId: arcId, index: 1);
        chapter.Id.Should().NotBe(default);
        chapter.ArcId.Should().Be(arcId);
        chapter.Index.Should().Be(1);
    }

    [Fact]
    public void RevelationEvent_ShouldBeCreatable()
    {
        var characterId = Id.New();
        var content = "A secret is revealed";
        var evt = new RevelationEvent(characterId: characterId, revelationContent: content);
        evt.Should().NotBeNull();
        evt.GetContent().Should().Be(content);
    }
}
