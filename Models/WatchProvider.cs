namespace JellyfinStreamingPlugin.Models;

/// <summary>
/// A single streaming service that carries a piece of content.
/// </summary>
public class WatchProvider
{
    /// <summary>
    /// TMDB provider ID (e.g. 15 = Hulu).
    /// </summary>
    public int ProviderId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// TMDB logo path — prefix with image base URL for display.
    /// </summary>
    public string? LogoPath { get; set; }

    public string FullLogoUrl =>
        LogoPath is not null
            ? $"https://image.tmdb.org/t/p/w92{LogoPath}"
            : string.Empty;

    /// <summary>
    /// tvOS/iOS URL scheme for deep-linking into the native app.
    /// e.g. "hulu://" or "nflx://"
    /// Phase 2: will include content-specific path when provider IDs are mapped.
    /// </summary>
    public string? DeepLinkUrl { get; set; }

    /// <summary>
    /// Web fallback URL for the content on this service.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// Availability type: "flatrate", "rent", or "buy".
    /// We primarily care about flatrate (subscription).
    /// </summary>
    public string AvailabilityType { get; set; } = "flatrate";
}
