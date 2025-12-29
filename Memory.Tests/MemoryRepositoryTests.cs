namespace Narratum.Memory.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Narratum.Memory;
using Narratum.Memory.Store;

/// <summary>
/// Integration tests for SQLiteMemoryRepository.
/// Tests CRUD operations and querying of memoranda from SQLite database.
/// </summary>
public class MemoryRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private MemoryDbContext _dbContext = null!;
    private SQLiteMemoryRepository _repository = null!;
    private readonly Guid _worldId = Guid.NewGuid();

    /// <summary>
    /// Initializes test context with in-memory SQLite database.
    /// The connection is kept open to preserve the in-memory database.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create and open a connection that stays open for the lifetime of the test
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MemoryDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
        _repository = new SQLiteMemoryRepository(_dbContext);
    }

    /// <summary>
    /// Cleans up test context after each test.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_ValidMemorandum_StoreAndRetrieve()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Test Memorandum", "Test Description");

        // Act
        await _repository.SaveAsync(memorandum);
        var retrieved = await _repository.GetByIdAsync(memorandum.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(memorandum.Id, retrieved.Id);
        Assert.Equal(memorandum.Title, retrieved.Title);
        Assert.Equal(memorandum.Description, retrieved.Description);
    }

    [Fact]
    public async Task SaveAsync_MultipleMemorandums_AllSaved()
    {
        // Arrange
        var memoria = new List<Memorandum>
        {
            Memorandum.CreateEmpty(_worldId, "Title 1", "Description 1"),
            Memorandum.CreateEmpty(_worldId, "Title 2", "Description 2"),
            Memorandum.CreateEmpty(_worldId, "Title 3", "Description 3")
        };

        // Act
        await _repository.SaveAsync(memoria);
        var all = await _repository.GetByWorldAsync(_worldId);

        // Assert
        Assert.Equal(3, all.Count);
        Assert.All(memoria, m => Assert.NotNull(all.FirstOrDefault(x => x.Id == m.Id)));
    }

    [Fact]
    public async Task SaveAsync_NullMemorandum_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync((Memorandum)null!));
    }

    [Fact]
    public async Task SaveAsync_NullList_DoesNotThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync((IReadOnlyList<Memorandum>)null!));
    }

    [Fact]
    public async Task SaveAsync_EmptyList_DoesNotCrash()
    {
        // Act
        await _repository.SaveAsync(new List<Memorandum>());

        // Assert - no exception thrown
        var all = await _repository.GetByWorldAsync(_worldId);
        Assert.Empty(all);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsMemorandum()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Find Me", "Description");
        await _repository.SaveAsync(memorandum);

        // Act
        var retrieved = await _repository.GetByIdAsync(memorandum.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(memorandum.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Act
        var retrieved = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetByIdAsync_DeletedMemorandum_ReturnsNull()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Delete Me", "Description");
        await _repository.SaveAsync(memorandum);
        await _repository.DeleteAsync(memorandum.Id);

        // Act
        var retrieved = await _repository.GetByIdAsync(memorandum.Id);

        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region GetByWorldAsync Tests

    [Fact]
    public async Task GetByWorldAsync_MultipleWorlds_ReturnsOnlyWorldMemorandum()
    {
        // Arrange
        var world2 = Guid.NewGuid();
        var mem1 = Memorandum.CreateEmpty(_worldId, "World 1 Title", "Desc");
        var mem2 = Memorandum.CreateEmpty(world2, "World 2 Title", "Desc");

        await _repository.SaveAsync(mem1);
        await _repository.SaveAsync(mem2);

        // Act
        var world1Result = await _repository.GetByWorldAsync(_worldId);
        var world2Result = await _repository.GetByWorldAsync(world2);

        // Assert
        Assert.Single(world1Result);
        Assert.Single(world2Result);
        Assert.Equal(mem1.Id, world1Result[0].Id);
        Assert.Equal(mem2.Id, world2Result[0].Id);
    }

    [Fact]
    public async Task GetByWorldAsync_EmptyWorld_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByWorldAsync(Guid.NewGuid());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByWorldAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var mem1 = Memorandum.CreateEmpty(_worldId, "Title 1", "Desc");
        var mem2 = Memorandum.CreateEmpty(_worldId, "Title 2", "Desc");
        var mem3 = Memorandum.CreateEmpty(_worldId, "Title 3", "Desc");

        await _repository.SaveAsync(mem1);
        await Task.Delay(10); // Ensure different timestamps
        await _repository.SaveAsync(mem2);
        await Task.Delay(10);
        await _repository.SaveAsync(mem3);

        // Act
        var result = await _repository.GetByWorldAsync(_worldId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(mem3.Id, result[0].Id);
        Assert.Equal(mem2.Id, result[1].Id);
        Assert.Equal(mem1.Id, result[2].Id);
    }

    #endregion

    #region GetByTitleAsync Tests

    [Fact]
    public async Task GetByTitleAsync_ExactMatchPattern_ReturnsMemorandum()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Exact Title", "Description");
        await _repository.SaveAsync(memorandum);

        // Act
        var result = await _repository.GetByTitleAsync(_worldId, "Exact Title");

        // Assert
        Assert.Single(result);
        Assert.Equal(memorandum.Id, result[0].Id);
    }

    [Fact]
    public async Task GetByTitleAsync_PartialPattern_ReturnsMatches()
    {
        // Arrange
        var mem1 = Memorandum.CreateEmpty(_worldId, "Chapter 1", "Desc");
        var mem2 = Memorandum.CreateEmpty(_worldId, "Chapter 2", "Desc");
        var mem3 = Memorandum.CreateEmpty(_worldId, "Other", "Desc");

        await _repository.SaveAsync(mem1);
        await _repository.SaveAsync(mem2);
        await _repository.SaveAsync(mem3);

        // Act
        var result = await _repository.GetByTitleAsync(_worldId, "Chapter");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(mem1.Id, result.Select(m => m.Id));
        Assert.Contains(mem2.Id, result.Select(m => m.Id));
    }

    [Fact]
    public async Task GetByTitleAsync_NullPattern_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByTitleAsync(_worldId, null!));
    }

    [Fact]
    public async Task GetByTitleAsync_EmptyPattern_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByTitleAsync(_worldId, ""));
    }

    [Fact]
    public async Task GetByTitleAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Title", "Description");
        await _repository.SaveAsync(memorandum);

        // Act
        var result = await _repository.GetByTitleAsync(_worldId, "NonExistent");

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingMemorandum_MarksSoftDelete()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Delete Test", "Desc");
        await _repository.SaveAsync(memorandum);

        // Act
        var deleted = await _repository.DeleteAsync(memorandum.Id);
        var retrieved = await _repository.GetByIdAsync(memorandum.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentMemorandum_ReturnsFalse()
    {
        // Act
        var deleted = await _repository.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_ReturnsFalse()
    {
        // Arrange
        var memorandum = Memorandum.CreateEmpty(_worldId, "Delete Twice", "Desc");
        await _repository.SaveAsync(memorandum);
        await _repository.DeleteAsync(memorandum.Id);

        // Act
        var deleted = await _repository.DeleteAsync(memorandum.Id);

        // Assert
        Assert.False(deleted);
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_FilterByWorld_ReturnsOnlyWorldMemorandum()
    {
        // Arrange
        var world2 = Guid.NewGuid();
        var mem1 = Memorandum.CreateEmpty(_worldId, "Title", "Desc");
        var mem2 = Memorandum.CreateEmpty(world2, "Title", "Desc");

        await _repository.SaveAsync(mem1);
        await _repository.SaveAsync(mem2);

        // Act
        var result = await _repository.QueryAsync(new MemoryQuery(WorldId: _worldId));

        // Assert
        Assert.Single(result);
        Assert.Equal(mem1.Id, result[0].Id);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ReturnsOnlyInRange()
    {
        // Arrange
        var before = DateTime.UtcNow.AddHours(-1);
        var after = DateTime.UtcNow.AddHours(1);

        var mem1 = Memorandum.CreateEmpty(_worldId, "Early", "Desc");
        await _repository.SaveAsync(mem1);
        await Task.Delay(100);

        var mem2 = Memorandum.CreateEmpty(_worldId, "Middle", "Desc");
        await _repository.SaveAsync(mem2);

        // Act
        var result = await _repository.QueryAsync(new MemoryQuery(
            WorldId: _worldId,
            FromDate: before.AddMinutes(1),
            ToDate: after
        ));

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, m => Assert.True(m.CreatedAt >= before.AddMinutes(1)));
    }

    [Fact]
    public async Task QueryAsync_FilterByTitle_ReturnsMatches()
    {
        // Arrange
        var mem1 = Memorandum.CreateEmpty(_worldId, "Chapter 1 Summary", "Desc");
        var mem2 = Memorandum.CreateEmpty(_worldId, "Chapter 2 Summary", "Desc");
        var mem3 = Memorandum.CreateEmpty(_worldId, "Other", "Desc");

        await _repository.SaveAsync(mem1);
        await _repository.SaveAsync(mem2);
        await _repository.SaveAsync(mem3);

        // Act
        var result = await _repository.QueryAsync(new MemoryQuery(
            WorldId: _worldId,
            TitleFilter: "Chapter"
        ));

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task QueryAsync_MultipleFilters_ReturnsIntersection()
    {
        // Arrange
        var world2 = Guid.NewGuid();
        var before = DateTime.UtcNow.AddHours(-1);

        var mem1 = Memorandum.CreateEmpty(_worldId, "Chapter 1", "Desc");
        var mem2 = Memorandum.CreateEmpty(world2, "Chapter 2", "Desc");

        await _repository.SaveAsync(mem1);
        await _repository.SaveAsync(mem2);

        // Act
        var result = await _repository.QueryAsync(new MemoryQuery(
            WorldId: _worldId,
            FromDate: before,
            TitleFilter: "Chapter"
        ));

        // Assert
        Assert.Single(result);
        Assert.Equal(mem1.Id, result[0].Id);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Persistence_RoundTrip_MaintainsCoreData()
    {
        // Arrange
        var original = Memorandum.CreateEmpty(_worldId, "Integration Test", "Full description");
        var memWithFact = original.AddFact(MemoryLevel.Event, 
            Fact.Create("Test fact", FactType.CharacterState, MemoryLevel.Event, new[] { "TestEntity" }));

        // Act
        await _repository.SaveAsync(memWithFact);
        var retrieved = await _repository.GetByIdAsync(memWithFact.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(memWithFact.Title, retrieved.Title);
        Assert.Equal(memWithFact.Description, retrieved.Description);
        Assert.NotEmpty(retrieved.CanonicalStates);
    }

    [Fact]
    public async Task Performance_BulkInsert_Completes()
    {
        // Arrange
        var memoranda = Enumerable.Range(1, 50)
            .Select(i => Memorandum.CreateEmpty(_worldId, $"Memo {i}", $"Description {i}"))
            .ToList();

        // Act
        var startTime = DateTime.UtcNow;
        await _repository.SaveAsync(memoranda);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        var all = await _repository.GetByWorldAsync(_worldId);
        Assert.Equal(50, all.Count);
        Assert.True(elapsed.TotalSeconds < 5, $"Bulk insert took {elapsed.TotalSeconds} seconds");
    }

    #endregion
}
