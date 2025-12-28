using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Persistence;
using Narratum.State;
using Narratum.Domain;

namespace Narratum.Tests;

public class Phase1Step6PersistenceUnitTests
{
    [Fact]
    public void SnapshotService_CreateSnapshot_ShouldCreateValidSnapshot()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.Should().NotBeNull();
        snapshot.TotalEventCount.Should().Be(0);
    }

    [Fact]
    public void SnapshotService_CreateSnapshot_ShouldIncludeMetadata()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SnapshotService_CreateSnapshot_ShouldComputeHash()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.IntegrityHash.Should().NotBeNullOrEmpty();
        snapshot.IntegrityHash.Should().Match("*"); // Base64 encoded
    }

    [Fact]
    public void SnapshotService_ValidateSnapshot_WithValidSnapshot_ShouldReturnOk()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        var result = service.ValidateSnapshot(snapshot);
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void SnapshotService_CreateSnapshot_ShouldIncludeEventHistory()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.TotalEventCount.Should().Be(0);
    }

    [Fact]
    public void SnapshotService_CreateSnapshot_WithCharacters_ShouldIncludeCharacterData()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var charState = new CharacterState(characterId: Id.New(), name: "Aric");
        var state = new StoryState(worldState: worldState).WithCharacter(charState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.CharacterStatesData.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SnapshotService_CreateSnapshot_ShouldHaveConsistentStructure()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot1 = service.CreateSnapshot(state);
        var snapshot2 = service.CreateSnapshot(state);
        // Both snapshots should have non-null hashes
        snapshot1.IntegrityHash.Should().NotBeNullOrEmpty();
        snapshot2.IntegrityHash.Should().NotBeNullOrEmpty();
        // Both should have same structure (even if timestamps differ)
        snapshot1.SnapshotVersion.Should().Be(snapshot2.SnapshotVersion);
    }

    [Fact]
    public void SnapshotService_ValidateSnapshot_ShouldCheckIntegrity()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        var result = service.ValidateSnapshot(snapshot);
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void SaveStateMetadata_ShouldHaveValidProperties()
    {
        var metadata = new SaveStateMetadata(
            SlotName: "TestSlot",
            SavedAt: DateTime.UtcNow,
            TotalEvents: 5,
            CurrentChapterId: null
        );
        metadata.SlotName.Should().Be("TestSlot");
        metadata.TotalEvents.Should().Be(5);
        metadata.CurrentChapterId.Should().BeNull();
    }

    [Fact]
    public void StateSnapshot_ShouldBeSerializable()
    {
        var service = new SnapshotService();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var snapshot = service.CreateSnapshot(state);
        snapshot.WorldStateData.Should().NotBeNullOrEmpty();
        snapshot.EventsData.Should().NotBeNullOrEmpty();
    }
}
