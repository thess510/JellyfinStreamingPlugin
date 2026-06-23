using JellyfinStreamingPlugin.Models;

namespace JellyfinStreamingPlugin.Services;

/// <summary>
/// Static lookup tables mapping TMDB provider IDs to deep-link URL schemes and web URLs.
/// Phase 2: content-specific deep-links will require per-provider ID mapping (separate problem).
/// For now, deep-links open the service's home screen — still useful for instant launch.
/// </summary>
public static class WatchProviderService
{
    // ── TMDB Provider ID → tvOS deep-link scheme ──────────────────────────────

    private static readonly Dictionary<int, string> DeepLinks = new()
    {
        [8]    = "nflx://",
        [15]   = "hulu://",
        [337]  = "disneyplus://",
        [9]    = "aiv://",       // Prime Video
        [350]  = "videos://",    // Apple TV+ — handled natively by tvOS
        [1899] = "max://",
        [386]  = "peacocktv://",
        [257]  = "fubo://",
        [230]  = "philo://",
    };

    // ── TMDB Provider ID → web base URL ──────────────────────────────────────

    private static readonly Dictionary<int, string> WebBaseUrls = new()
    {
        [8]    = "https://www.netflix.com",
        [15]   = "https://www.hulu.com",
        [337]  = "https://www.disneyplus.com",
        [9]    = "https://www.amazon.com/gp/video",
        [350]  = "https://tv.apple.com",
        [1899] = "https://www.max.com",
        [386]  = "https://www.peacocktv.com",
        [257]  = "https://www.fubo.tv",
        [230]  = "https://www.philo.com",
    };

    // ── Provider ID → human-readable name (fallback if TMDB name is missing) ─

    private static readonly Dictionary<int, string> ProviderNames = new()
    {
        [8]    = "Netflix",
        [15]   = "Hulu",
        [337]  = "Disney+",
        [9]    = "Prime Video",
        [350]  = "Apple TV+",
        [1899] = "Max",
        [386]  = "Peacock",
        [257]  = "Fubo",
        [230]  = "Philo",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static string? GetDeepLink(int providerId) =>
        DeepLinks.TryGetValue(providerId, out var link) ? link : null;

    public static string GetProviderName(int providerId) =>
        ProviderNames.TryGetValue(providerId, out var name) ? name : $"Provider {providerId}";

    /// <summary>
    /// Builds a web URL for the content on the given provider.
    /// Phase 1: returns the service home page.
    /// Phase 2: will append content-specific paths when provider content IDs are available.
    /// </summary>
    public static string? GetWebUrl(int providerId, StreamingItem item)
    {
        if (!WebBaseUrls.TryGetValue(providerId, out var baseUrl)) return null;

        // Phase 2 placeholder: content-specific URL construction will go here.
        // e.g. Hulu: $"https://www.hulu.com/series/{huluSeriesId}"
        //      Netflix: $"https://www.netflix.com/title/{netflixId}"
        // For now, return the service home — still useful as a web fallback.
        return baseUrl;
    }

    /// <summary>
    /// Returns all known TMDB provider IDs (for filtering purposes).
    /// </summary>
    public static IReadOnlyCollection<int> KnownProviderIds => DeepLinks.Keys;
}
