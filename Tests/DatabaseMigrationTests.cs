using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Narratum.Persistence;
using Xunit;

namespace Narratum.Tests;

/// <summary>
/// Vérifie que <see cref="DatabaseInitializer.InitializeNarratumDatabase"/> :
/// (1) applique toutes les migrations sur une base neuve ; et
/// (2) « baseline » une base héritée d'EnsureCreated() (schéma initial, sans historique de
/// migrations) sans perdre les données, en appliquant les migrations ultérieures par-dessus.
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
    public void InitializeNarratumDatabase_OnFreshDatabase_AppliesAllMigrations()
    {
        using var db = new NarrativumDbContext(this.Options());

        db.InitializeNarratumDatabase();

        db.Database.GetPendingMigrations().Should().BeEmpty("a fresh database is migrated to the latest schema");
        db.Database.GetAppliedMigrations().Should().NotBeEmpty();
        db.PageSnapshots.Add(NewPage("slot-a", 0));
        db.SaveChanges();
        db.PageSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public void InitializeNarratumDatabase_OnLegacyEnsureCreatedDatabase_BaselinesAndKeepsData()
    {
        // Build a faithful legacy database: only the INITIAL migration's schema (what the old
        // EnsureCreated() produced), with real data and no __EFMigrationsHistory table.
        using (var legacy = new NarrativumDbContext(this.Options()))
        {
            var initialMigration = legacy.Database.GetMigrations().First();
            legacy.GetService<IMigrator>().Migrate(initialMigration);
            // Insert via raw SQL using only InitialCreate columns — the entity model now has
            // SerializedChoices, which this legacy schema does not yet have.
            legacy.Database.ExecuteSqlRaw(
                "INSERT INTO \"PageSnapshots\" (\"Id\", \"SlotName\", \"PageIndex\", \"GeneratedAt\", \"SerializedState\") " +
                "VALUES ({0}, {1}, {2}, {3}, {4});",
                "11111111-1111-1111-1111-111111111111", "legacy-slot", 0, "2026-01-01 00:00:00", "{}");
            legacy.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"__EFMigrationsHistory\";");
        }

        using (var pre = new NarrativumDbContext(this.Options()))
        {
            pre.Database.GetAppliedMigrations().Should().BeEmpty(
                "an EnsureCreated database has no migration history yet");
        }

        using var db = new NarrativumDbContext(this.Options());
        var act = () => db.InitializeNarratumDatabase();

        act.Should().NotThrow("baselining must not recreate existing tables");
        db.Database.GetPendingMigrations().Should().BeEmpty(
            "later migrations must apply on top of the baselined initial migration");
        db.PageSnapshots.Count().Should().Be(1, "existing data must be preserved");
        // The column added by a later migration must now be usable.
        db.Database.ExecuteSqlRaw(
            "UPDATE \"PageSnapshots\" SET \"SerializedChoices\" = '[]' WHERE \"SlotName\" = 'legacy-slot';");
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
