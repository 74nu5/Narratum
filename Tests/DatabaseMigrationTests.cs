using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Narratum.Persistence;
using Xunit;

namespace Narratum.Tests;

/// <summary>
/// Vérifie que <see cref="DatabaseInitializer.InitializeNarratumDatabase"/> :
/// (1) crée le schéma via migrations sur une base neuve ; et
/// (2) « baseline » une base héritée d'EnsureCreated() sans perdre les données ni planter.
/// </summary>
public sealed class DatabaseMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"narratum_mig_{Guid.NewGuid():N}.db");

    private DbContextOptions<NarrativumDbContext> Options()
        => new DbContextOptionsBuilder<NarrativumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

    [Fact]
    public void InitializeNarratumDatabase_OnFreshDatabase_AppliesInitialMigration()
    {
        using var db = new NarrativumDbContext(this.Options());

        db.InitializeNarratumDatabase();

        db.Database.GetAppliedMigrations().Should().ContainSingle(
            "the initial migration must be recorded on a fresh database");
        db.PageSnapshots.Add(NewPage("slot-a", 0));
        db.SaveChanges();
        db.PageSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public void InitializeNarratumDatabase_OnLegacyEnsureCreatedDatabase_BaselinesAndKeepsData()
    {
        // Simulate a database produced by the old Database.EnsureCreated() path:
        // full schema, real data, but no __EFMigrationsHistory table.
        using (var legacy = new NarrativumDbContext(this.Options()))
        {
            legacy.Database.EnsureCreated();
            legacy.PageSnapshots.Add(NewPage("legacy-slot", 0));
            legacy.SaveChanges();
        }

        using (var pre = new NarrativumDbContext(this.Options()))
        {
            pre.Database.GetAppliedMigrations().Should().BeEmpty(
                "an EnsureCreated database has no migration history yet");
        }

        using var db = new NarrativumDbContext(this.Options());
        var act = () => db.InitializeNarratumDatabase();

        act.Should().NotThrow("baselining must not attempt to recreate existing tables");
        db.Database.GetAppliedMigrations().Should().ContainSingle(
            "the initial migration must be marked as applied after baselining");
        db.PageSnapshots.Count().Should().Be(1, "existing data must be preserved");
    }

    private static PageSnapshotEntity NewPage(string slot, int index) => new()
    {
        Id = Guid.NewGuid(),
        SlotName = slot,
        PageIndex = index,
        GeneratedAt = DateTime.UtcNow,
        SerializedState = "{}",
    };

    public void Dispose()
    {
        // SQLite keeps a handle briefly; ignore if the file is momentarily locked.
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(this._dbPath))
                File.Delete(this._dbPath);
        }
        catch (IOException)
        {
        }
    }
}
