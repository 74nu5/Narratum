using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Narratum.Persistence;
using Xunit;

namespace Narratum.Web.Tests.Services;

/// <summary>
/// Regression tests for StoryRepository backed by a REAL relational provider (SQLite).
/// EF Core's InMemory provider does not translate LINQ to SQL, so it silently accepts
/// queries that throw at runtime against a real database (e.g. an untranslatable GroupBy).
/// These tests exercise the actual SQL translation path.
/// </summary>
public sealed class StoryRepositorySqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NarrativumDbContext _dbContext;

    public StoryRepositorySqliteTests()
    {
        // A shared in-memory SQLite database lives as long as the connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NarrativumDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new NarrativumDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListStoriesAsync_WithRealSqliteProvider_TranslatesAndGroupsPerSlot()
    {
        // Arrange — two stories, several pages each, out-of-order timestamps
        _dbContext.PageSnapshots.AddRange(
            NewPage("slot-a", 0, "Fantasy", new DateTime(2026, 1, 1)),
            NewPage("slot-a", 1, "Fantasy", new DateTime(2026, 1, 3)),
            NewPage("slot-b", 0, "Sci-Fi", new DateTime(2026, 1, 2)));
        await _dbContext.SaveChangesAsync();

        var repository = new StoryRepository(_dbContext, Mock.Of<ISnapshotService>());

        // Act — previously threw InvalidOperationException (GroupBy could not be translated)
        var stories = await repository.ListStoriesAsync();

        // Assert
        stories.Should().HaveCount(2);

        var slotA = stories.Single(s => s.SlotName == "slot-a");
        slotA.PageCount.Should().Be(2);
        slotA.GenreStyle.Should().Be("Fantasy");
        slotA.LastUpdated.Should().Be(new DateTime(2026, 1, 3));

        var slotB = stories.Single(s => s.SlotName == "slot-b");
        slotB.PageCount.Should().Be(1);
        slotB.GenreStyle.Should().Be("Sci-Fi");

        // Ordered most-recently-updated first (slot-a's latest page is Jan 3 > slot-b's Jan 2)
        stories.First().SlotName.Should().Be("slot-a");
    }

    [Fact]
    public async Task ListStoriesAsync_WithNoStories_ReturnsEmptyList()
    {
        var repository = new StoryRepository(_dbContext, Mock.Of<ISnapshotService>());

        var stories = await repository.ListStoriesAsync();

        stories.Should().BeEmpty();
    }

    private static PageSnapshotEntity NewPage(string slot, int index, string genre, DateTime generatedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            SlotName = slot,
            PageIndex = index,
            GeneratedAt = generatedAt,
            SerializedState = "{}",
            GenreStyle = genre
        };
}
