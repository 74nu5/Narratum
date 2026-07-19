namespace Narratum.Web.Services;

using System.Text.Json;

using Narratum.Core;
using Narratum.Web.Models;

/// <summary>
/// One-off reconciliation run at startup: stories created before universes existed carry their
/// bible as a per-slot snapshot. This promotes those bibles into real universes and attaches the
/// stories, so runs of the same setting group together instead of sitting flat and orphaned.
/// Identical bibles collapse into a single universe — that is what makes the grouping useful.
/// </summary>
public sealed class UniverseBackfillService
{
    /// <summary>Separator used to join fingerprint parts; cannot occur in authored text.</summary>
    private const char FingerprintSeparator = (char)0x1F;

    private readonly IStoryRepository _stories;
    private readonly IUniverseRepository _universes;
    private readonly ILogger<UniverseBackfillService> _logger;

    public UniverseBackfillService(
        IStoryRepository stories,
        IUniverseRepository universes,
        ILogger<UniverseBackfillService> logger)
    {
        this._stories = stories;
        this._universes = universes;
        this._logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var stories = await this._stories.ListStoriesAsync(ct);
        var orphans = stories.Where(s => string.IsNullOrWhiteSpace(s.UniverseId)).ToList();
        if (orphans.Count == 0)
            return;

        // Re-use universes already present so a restart doesn't duplicate them.
        var byFingerprint = (await this._universes.ListAsync(ct))
            .ToDictionary(Fingerprint, u => u.UniverseId, StringComparer.OrdinalIgnoreCase);

        var attached = 0;
        var created = 0;

        foreach (var story in orphans)
        {
            var world = await this.ReadWorldAsync(story.SlotName, ct);
            if (world is null)
                continue; // No bible to promote: leave it unattached rather than invent one.

            var opening = await this.ReadOpeningAsync(story.SlotName, ct);
            var fingerprint = Fingerprint(world);

            if (!byFingerprint.TryGetValue(fingerprint, out var universeId))
            {
                var universe = await this._universes.CreateAsync(
                    new Universe(
                        $"univers-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{created}",
                        world.WorldName,
                        world.GenreStyle,
                        world.WorldDescription,
                        world.NarrativeStyle,
                        JsonSerializer.Serialize(world.Characters ?? []),
                        JsonSerializer.Serialize(world.Locations ?? []),
                        opening,
                        null,
                        DateTime.UtcNow),
                    ct);

                universeId = universe.UniverseId;
                byFingerprint[fingerprint] = universeId;
                created++;
            }

            await this._stories.SetStoryUniverseAsync(story.SlotName, universeId, ct);
            attached++;
        }

        if (attached > 0)
            this._logger.LogInformation(
                "Universe backfill: attached {Attached} story/stories to {Created} new universe(s)",
                attached, created);
    }

    private async Task<StoryWorld?> ReadWorldAsync(string slotName, CancellationToken ct)
    {
        try
        {
            var json = await this._stories.GetStoryWorldAsync(slotName, ct);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<StoryWorld>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The opening action of a story: the intent recorded on its first written page.</summary>
    private async Task<string?> ReadOpeningAsync(string slotName, CancellationToken ct)
    {
        var page = await this._stories.LoadPageAsync(slotName, 1, ct);

        return page is Result<PageSnapshot>.Success success
            && !string.IsNullOrWhiteSpace(success.Value.IntentDescription)
                ? success.Value.IntentDescription
                : null;
    }

    /// <summary>Two runs of the same setting must land on the same universe.</summary>
    private static string Fingerprint(Universe u)
        => string.Join(FingerprintSeparator, u.Name, u.GenreStyle, u.Description ?? string.Empty);

    private static string Fingerprint(StoryWorld w)
        => string.Join(FingerprintSeparator, w.WorldName, w.GenreStyle, w.WorldDescription ?? string.Empty);
}
