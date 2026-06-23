namespace JellyfinStreamingPlugin.Models;

/// <summary>
/// Internal representation of a movie or TV show discovered via TMDB.
/// This is the working model used by TmdbService and passed to StreamingChannel.
/// </summary>
public class StreamingItem
{
    public int TmdbId { get; set; }

    /// <summary>
    /// "movie" or "tv" — as returned by TMDB.
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    /// TMDB poster path — needs image base URL prepended.
    /// e.g. "/abc123.jpg" → "https://image.tmdb.org/t/p/w500/abc123.jpg"
    /// </summary>
    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    /// <summary>
    /// Release date string as returned by TMDB (ISO 8601, may be partial).
    /// </summary>
    public string? ReleaseDate { get; set; }

    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }

    /// <summary>
    /// TMDB genre IDs — resolved to names at display time if needed.
    /// </summary>
    public List<int> GenreIds { get; set; } = new();

    /// <summary>
    /// Streaming providers where this item is available (flatrate/subscription only).
    /// Populated after a watch provider API call.
    /// </summary>
    public List<WatchProvider> Providers { get; set; } = new();

    // ── Computed helpers ──────────────────────────────────────────────────────

    public string FullPosterUrl =>
        PosterPath is not null
            ? $"https://image.tmdb.org/t/p/w500{PosterPath}"
            : string.Empty;

    public string FullBackdropUrl =>
        BackdropPath is not null
            ? $"https://image.tmdb.org/t/p/w1280{BackdropPath}"
            : string.Empty;

    public int? ReleaseYear =>
        DateTime.TryParse(ReleaseDate, out var dt) ? dt.Year : null;

    /// <summary>
    /// Comma-separated list of provider names for display in item descriptions.
    /// </summary>
    public string ProviderSummary =>
        Providers.Count > 0
            ? string.Join(", ", Providers.Select(p => p.Name))
            : "No streaming providers found";
}
