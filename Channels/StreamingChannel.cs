using System.Threading;
using JellyfinStreamingPlugin.Models;
using JellyfinStreamingPlugin.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyfinStreamingPlugin.Channels;

/// <summary>
/// Implements the Jellyfin Channels API to expose streaming catalog content
/// as a browsable virtual library. This is the correct primitive for non-local content —
/// it doesn't need filesystem paths like a virtual library would.
///
/// Jellyfin discovers this via DI — it must be registered as IChannel.
/// </summary>
public class StreamingChannel : IChannel, IHasCacheKey
{
    private readonly ILogger<StreamingChannel> _logger;
    private readonly TmdbService _tmdbService;

    // In-memory cache of the last sync result.
    // The background task (SyncStreamingContentTask) populates this.
    // On first load before any sync, we return empty or trigger a sync.
    private static List<StreamingItem> _cachedItems = new();
    private static DateTime _lastCacheUpdate = DateTime.MinValue;

    public StreamingChannel(ILogger<StreamingChannel> logger, TmdbService tmdbService)
    {
        _logger = logger;
        _tmdbService = tmdbService;
    }

    // ── IChannel identity ─────────────────────────────────────────────────────

    public string Name => "Streaming Discovery";

    public string Description =>
        "Browse streaming content from Hulu, Netflix, Disney+, and more — powered by TMDB.";

    public string DataVersion => "1";

    // ── IHasCacheKey ──────────────────────────────────────────────────────────

    // Cache key changes when the cached item list changes — Jellyfin uses this
    // to know when to invalidate its own channel item cache.
    public string GetCacheKey(string userId) =>
        $"streaming-channel-{_lastCacheUpdate:yyyyMMddHHmm}";

    // ── Channel capabilities ──────────────────────────────────────────────────

    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = new List<ChannelMediaContentType>
            {
                ChannelMediaContentType.Movie,
                ChannelMediaContentType.Episode
            },
            MediaTypes = new List<ChannelMediaType>
            {
                // We're not providing playable media — Phase 2 deep-links handle playback.
                // For now, declare as video so Jellyfin treats items correctly.
                ChannelMediaType.Video
            },
            // Allow Jellyfin to sort and filter our channel items
            SupportsSortOrderToggle = true,
            SupportsLatestMedia = true,
            CanFilter = false
        };
    }

    // ── Channel items ─────────────────────────────────────────────────────────

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        // No custom channel image for now — Jellyfin will use a default.
        return Task.FromResult(new DynamicImageResponse { HasImage = false });
    }

    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return Enumerable.Empty<ImageType>();
    }

    public bool IsEnabledFor(string userId) => true;

    /// <summary>
    /// Called by Jellyfin to get the list of items for browsing.
    /// We return our cached TMDB results, converted to ChannelItemInfo.
    /// </summary>
    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("StreamingChannel.GetChannelItems called, cache has {Count} items", _cachedItems.Count);

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Task.FromResult(new ChannelItemResult { Items = new List<ChannelItemInfo>() });
        }

        var enabledIds = config.EnabledProviderIds();
        var items = new List<ChannelItemInfo>();

        foreach (var si in _cachedItems)
        {
            // Filter to only items available on an enabled service
            var matchingProviders = si.Providers.Where(p => enabledIds.Contains(p.ProviderId)).ToList();
            if (matchingProviders.Count == 0) continue;

            // Filter by content type config
            if (si.MediaType == "movie" && !config.IncludeMovies) continue;
            if (si.MediaType == "tv" && !config.IncludeTvShows) continue;

            items.Add(MapToChannelItem(si, matchingProviders));
        }

        // Apply paging
        var paged = items
            .Skip(query.StartIndex ?? 0)
            .Take(query.Limit ?? items.Count)
            .ToList();

        return Task.FromResult(new ChannelItemResult
        {
            Items = paged,
            TotalRecordCount = items.Count
        });
    }

    // ── Cache management (called by SyncStreamingContentTask) ─────────────────

    public static void UpdateCache(List<StreamingItem> items)
    {
        _cachedItems = items;
        _lastCacheUpdate = DateTime.UtcNow;
    }

    public static IReadOnlyList<StreamingItem> GetCachedItems() => _cachedItems.AsReadOnly();

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static ChannelItemInfo MapToChannelItem(StreamingItem si, List<WatchProvider> providers)
    {
        var providerSummary = string.Join(", ", providers.Select(p => p.Name));
        var contentType = si.MediaType == "movie"
            ? ChannelMediaContentType.Movie
            : ChannelMediaContentType.Episode;

        var item = new ChannelItemInfo
        {
            // Use a stable ID: "streaming-{tmdbId}-{mediaType}"
            Id = $"streaming-{si.TmdbId}-{si.MediaType}",
            Name = si.Title,
            Overview = si.Overview,
            Type = ChannelItemType.Media,
            ContentType = contentType,
            HomePageUrl = providers.FirstOrDefault()?.WebUrl,

            // CommunityRating maps to TMDB vote average
            CommunityRating = si.VoteAverage > 0 ? (float)si.VoteAverage : null,

            // Store provider info in the item's tag line for display in clients
            // that show it (web UI does; Neptune may differ)
            Tagline = $"Streaming on: {providerSummary}",

            // Provider logos for display
            ProviderIds = new Dictionary<string, string>
            {
                ["tmdb"] = si.TmdbId.ToString()
            }
        };

        // Attach poster image
        if (!string.IsNullOrEmpty(si.FullPosterUrl))
        {
            item.ImageUrl = si.FullPosterUrl;
            item.HasImage = true;
        }

        // Attach production year
        if (si.ReleaseYear.HasValue)
        {
            item.ProductionYear = si.ReleaseYear.Value;
        }

        // Store the streaming provider list in a way the custom API endpoint can retrieve it.
        // We encode this as a special tag that StreamingController reads back.
        // Format: "streaming:15,8,337" (comma-separated provider IDs)
        item.Tags = new List<string>
        {
            $"streaming-providers:{string.Join(",", providers.Select(p => p.ProviderId))}",
            $"streaming-tmdb:{si.TmdbId}",
            $"streaming-type:{si.MediaType}"
        };

        return item;
    }
}
