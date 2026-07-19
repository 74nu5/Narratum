namespace Narratum.Persistence;

using Microsoft.EntityFrameworkCore;

using Narratum.Core;

/// <summary>
/// EF Core implementation of <see cref="IUniverseRepository"/>. Uses the same short-lived
/// context-per-operation approach as <see cref="StoryRepository"/>, which keeps Blazor Server
/// circuits away from a long-lived context.
/// </summary>
public sealed class UniverseRepository : IUniverseRepository
{
    private readonly IDbContextFactory<NarrativumDbContext> _contextFactory;

    public UniverseRepository(IDbContextFactory<NarrativumDbContext> contextFactory)
        => this._contextFactory = contextFactory;

    public async Task<Universe> CreateAsync(Universe universe, CancellationToken ct = default)
    {
        await using var db = await this._contextFactory.CreateDbContextAsync(ct);

        var created = universe with
        {
            CreatedAt = universe.CreatedAt == default ? DateTime.UtcNow : universe.CreatedAt,
        };

        db.Universes.Add(ToEntity(created));
        await db.SaveChangesAsync(ct);

        return created;
    }

    public async Task<IReadOnlyList<Universe>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await this._contextFactory.CreateDbContextAsync(ct);

        var rows = await db.Universes
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        return [.. rows.Select(ToModel)];
    }

    public async Task<Universe?> GetAsync(string universeId, CancellationToken ct = default)
    {
        await using var db = await this._contextFactory.CreateDbContextAsync(ct);

        var row = await db.Universes
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UniverseId == universeId, ct);

        return row is null ? null : ToModel(row);
    }

    public async Task UpdateAsync(Universe universe, CancellationToken ct = default)
    {
        await using var db = await this._contextFactory.CreateDbContextAsync(ct);

        var row = await db.Universes.FirstOrDefaultAsync(u => u.UniverseId == universe.UniverseId, ct);
        if (row is null)
            return;

        db.Entry(row).CurrentValues.SetValues(row with
        {
            Name = universe.Name,
            GenreStyle = universe.GenreStyle,
            Description = universe.Description,
            NarrativeStyle = universe.NarrativeStyle,
            SerializedCharacters = universe.SerializedCharacters,
            SerializedLocations = universe.SerializedLocations,
            OpeningAction = universe.OpeningAction,
            DefaultModel = universe.DefaultModel,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string universeId, CancellationToken ct = default)
    {
        await using var db = await this._contextFactory.CreateDbContextAsync(ct);

        var row = await db.Universes.FirstOrDefaultAsync(u => u.UniverseId == universeId, ct);
        if (row is null)
            return;

        // Detach the runs rather than cascade: the setting is disposable, the stories are not.
        var runs = await db.SaveSlots.Where(s => s.UniverseId == universeId).ToListAsync(ct);
        foreach (var run in runs)
            db.Entry(run).CurrentValues.SetValues(run with { UniverseId = null });

        db.Universes.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private static UniverseEntity ToEntity(Universe u) => new()
    {
        UniverseId = u.UniverseId,
        Name = u.Name,
        GenreStyle = u.GenreStyle,
        Description = u.Description,
        NarrativeStyle = u.NarrativeStyle,
        SerializedCharacters = u.SerializedCharacters,
        SerializedLocations = u.SerializedLocations,
        OpeningAction = u.OpeningAction,
        DefaultModel = u.DefaultModel,
        CreatedAt = u.CreatedAt,
    };

    private static Universe ToModel(UniverseEntity e) => new(
        e.UniverseId,
        e.Name,
        e.GenreStyle,
        e.Description,
        e.NarrativeStyle,
        e.SerializedCharacters,
        e.SerializedLocations,
        e.OpeningAction,
        e.DefaultModel,
        e.CreatedAt);
}
