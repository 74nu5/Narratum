namespace Narratum.Web.Services;

using System.Text.Json;

using Narratum.Core;
using Narratum.Web.Models;

/// <summary>
/// Universes as the Web layer sees them: typed cast and places rather than JSON blobs.
/// A universe is the reusable setting; each story is one run of it.
/// </summary>
public sealed class UniverseService
{
    private readonly IUniverseRepository _repository;
    private readonly ILogger<UniverseService> _logger;

    public UniverseService(IUniverseRepository repository, ILogger<UniverseService> logger)
    {
        this._repository = repository;
        this._logger = logger;
    }

    public async Task<IReadOnlyList<UniverseInfo>> ListAsync(CancellationToken ct = default)
        => [.. (await this._repository.ListAsync(ct)).Select(ToInfo)];

    public async Task<UniverseInfo?> GetAsync(string universeId, CancellationToken ct = default)
    {
        var universe = await this._repository.GetAsync(universeId, ct);
        return universe is null ? null : ToInfo(universe);
    }

    /// <summary>Creates a universe from what the wizard collected.</summary>
    public async Task<UniverseInfo> CreateAsync(
        string name,
        string genreStyle,
        string? description,
        string? narrativeStyle,
        IReadOnlyList<WorldCharacter> characters,
        IReadOnlyList<WorldPlace> locations,
        string? openingAction,
        string? defaultModel,
        CancellationToken ct = default)
    {
        var universe = new Universe(
            $"univers-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            name,
            genreStyle,
            description,
            narrativeStyle,
            JsonSerializer.Serialize(characters),
            JsonSerializer.Serialize(locations),
            openingAction,
            defaultModel,
            DateTime.UtcNow);

        var created = await this._repository.CreateAsync(universe, ct);
        this._logger.LogInformation("Created universe {UniverseId} ({Name})", created.UniverseId, created.Name);

        return ToInfo(created);
    }

    public Task UpdateAsync(UniverseInfo universe, CancellationToken ct = default)
        => this._repository.UpdateAsync(new Universe(
            universe.UniverseId,
            universe.Name,
            universe.GenreStyle,
            universe.Description,
            universe.NarrativeStyle,
            JsonSerializer.Serialize(universe.Characters),
            JsonSerializer.Serialize(universe.Locations),
            universe.OpeningAction,
            universe.DefaultModel,
            universe.CreatedAt), ct);

    public Task DeleteAsync(string universeId, CancellationToken ct = default)
        => this._repository.DeleteAsync(universeId, ct);

    /// <summary>
    /// Promotes a character a run invented into the universe's cast, so the <em>next</em> runs
    /// start with it. Runs in progress are untouched — they already carry it in their own page
    /// roster, and their snapshot is deliberately frozen. Matching on the name keeps this
    /// idempotent: adding the same character twice is a no-op rather than a duplicate.
    /// </summary>
    public async Task<bool> AddCharacterAsync(
        string universeId, WorldCharacter character, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(character.Name))
            return false;

        var universe = await this.GetAsync(universeId, ct);
        if (universe is null)
            return false;

        if (universe.Characters.Any(c => string.Equals(c.Name, character.Name, StringComparison.OrdinalIgnoreCase)))
            return false;

        await this.UpdateAsync(universe with { Characters = [.. universe.Characters, character] }, ct);

        this._logger.LogInformation(
            "Promoted character {Character} into universe {UniverseId}", character.Name, universeId);

        return true;
    }

    private static UniverseInfo ToInfo(Universe u) => new(
        u.UniverseId,
        u.Name,
        u.GenreStyle,
        u.Description,
        u.NarrativeStyle,
        Deserialize<WorldCharacter>(u.SerializedCharacters),
        Deserialize<WorldPlace>(u.SerializedLocations),
        u.OpeningAction,
        u.DefaultModel,
        u.CreatedAt);

    private static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
