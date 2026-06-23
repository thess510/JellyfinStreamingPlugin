using MediaBrowser.Model.Plugins;

namespace JellyfinStreamingPlugin.Configuration;

/// <summary>
/// All user-configurable settings for the Streaming Discovery plugin.
/// Jellyfin serializes this to XML automatically.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The user's own TMDB API key (free at themoviedb.org).
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 alpha-2 country code for watch provider availability.
    /// Defaults to US.
    /// </summary>
    public string CountryCode { get; set; } = "US";

    /// <summary>
    /// How often (in hours) the background sync task should run.
    /// </summary>
    public int SyncIntervalHours { get; set; } = 6;

    /// <summary>
    /// Maximum number of items to sync per category (trending movies, trending TV).
    /// Keeps API usage well within TMDB free-tier limits.
    /// </summary>
    public int MaxItemsPerCategory { get; set; } = 100;

    // ── Content type toggles ─────────────────────────────────────────────────

    public bool IncludeMovies { get; set; } = true;
    public bool IncludeTvShows { get; set; } = true;

    // ── Streaming service toggles ─────────────────────────────────────────────
    // Matching TMDB provider IDs defined in WatchProviderService.

    public bool IncludeNetflix { get; set; } = true;
    public bool IncludeHulu { get; set; } = true;
    public bool IncludeDisneyPlus { get; set; } = true;
    public bool IncludePrimeVideo { get; set; } = true;
    public bool IncludeAppleTvPlus { get; set; } = true;
    public bool IncludeMax { get; set; } = true;
    public bool IncludePeacock { get; set; } = true;
    public bool IncludeFubo { get; set; } = false;
    public bool IncludePhilo { get; set; } = false;

    /// <summary>
    /// Returns the set of TMDB provider IDs that are currently enabled.
    /// Used by WatchProviderService to filter results.
    /// </summary>
    public HashSet<int> EnabledProviderIds()
    {
        var ids = new HashSet<int>();
        if (IncludeNetflix)    ids.Add(8);
        if (IncludeHulu)       ids.Add(15);
        if (IncludeDisneyPlus) ids.Add(337);
        if (IncludePrimeVideo) ids.Add(9);
        if (IncludeAppleTvPlus) ids.Add(350);
        if (IncludeMax)        ids.Add(1899);
        if (IncludePeacock)    ids.Add(386);
        if (IncludeFubo)       ids.Add(257);
        if (IncludePhilo)      ids.Add(230);
        return ids;
    }
}
