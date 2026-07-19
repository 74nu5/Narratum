namespace Narratum.Web.Services;

/// <summary>
/// Derives a per-story emoji and accent colour from its genre, so cards and
/// library rows get a little visual identity (the editorial redesign uses these).
/// </summary>
public static class StoryVisuals
{
    /// <summary>Genre → emoji. Falls back to a book.</summary>
    public static string Icon(string? genre) => Normalize(genre) switch
    {
        "scifi" => "🌌",
        "fantasy" => "🐉",
        "mystery" => "🕯️",
        "horror" => "🔦",
        "aventure" => "🌊",
        "historical" => "🏛️",
        "romance" => "🌹",
        _ => "📖",
    };

    /// <summary>Genre → accent colour (hex) for the card's top rule.</summary>
    public static string Accent(string? genre) => Normalize(genre) switch
    {
        "scifi" => "#4a7fb5",
        "fantasy" => "#b5651d",
        "mystery" => "#8e5aa8",
        "horror" => "#7a3b3b",
        "aventure" => "#3f8f8a",
        "historical" => "#9a7b3f",
        "romance" => "#b5556f",
        _ => "#b5651d",
    };

    private static string Normalize(string? genre)
    {
        var g = genre?.Trim().ToLowerInvariant() ?? string.Empty;
        return g switch
        {
            "science-fiction" or "sci-fi" or "sf" => "scifi",
            "mystère" or "mystere" => "mystery",
            "horreur" => "horror",
            "adventure" => "aventure",
            "historique" => "historical",
            _ => g,
        };
    }
}
