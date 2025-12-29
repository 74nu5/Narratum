using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;

namespace Narratum.Tests;

public class Phase1Step6CoreUnitTests
{
    [Fact]
    public void Result_Ok_ShouldCreateSuccessResult()
    {
        var result = Result<int>.Ok(42);
        result.Should().BeOfType<Result<int>.Success>();
        var success = (Result<int>.Success)result;
        success.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Fail_ShouldCreateFailureResult()
    {
        var result = Result<int>.Fail("Error message");
        result.Should().BeOfType<Result<int>.Failure>();
        var failure = (Result<int>.Failure)result;
        failure.Message.Should().Be("Error message");
    }

    [Fact]
    public void Result_Match_ShouldExecuteSuccessPath()
    {
        var result = Result<int>.Ok(42);
        var output = result.Match(
            onSuccess: x => x * 2,
            onFailure: _ => 0
        );
        output.Should().Be(84);
    }

    [Fact]
    public void Result_Match_ShouldExecuteFailurePath()
    {
        var result = Result<int>.Fail("Error");
        var output = result.Match(
            onSuccess: _ => 99,
            onFailure: msg => msg.Length
        );
        output.Should().Be(5);
    }

    [Fact]
    public void Id_New_ShouldCreateUniqueIds()
    {
        var id1 = Id.New();
        var id2 = Id.New();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Id_From_ShouldCreateIdFromGuid()
    {
        var guid = Guid.NewGuid();
        var id = Id.From(guid);
        id.Should().NotBe(default);
    }

    [Fact]
    public void Id_Equality_ShouldWorkCorrectly()
    {
        var id1 = Id.New();
        var id2 = id1;
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Unit_Default_ShouldReturnUnit()
    {
        var unit = Unit.Default;
        unit.Should().NotBeNull();
    }

    [Fact]
    public void VitalStatus_ShouldHaveAllValues()
    {
        VitalStatus.Alive.Should().Be(VitalStatus.Alive);
        VitalStatus.Dead.Should().Be(VitalStatus.Dead);
    }

    [Fact]
    public void StoryProgressStatus_ShouldHaveAllValues()
    {
        StoryProgressStatus.NotStarted.Should().Be(StoryProgressStatus.NotStarted);
        StoryProgressStatus.InProgress.Should().Be(StoryProgressStatus.InProgress);
        StoryProgressStatus.Completed.Should().Be(StoryProgressStatus.Completed);
    }

    [Fact]
    public void DomainEvent_ShouldBeCreatable()
    {
        var characterId = Id.New();
        var evt = new CharacterDeathEvent(characterId: characterId);
        evt.Should().NotBeNull();
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
