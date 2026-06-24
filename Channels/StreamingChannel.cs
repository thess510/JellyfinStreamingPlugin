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

    /// <summary>
    /// Required by IChannel in Jellyfin 10.11+.
    /// </summary>
    public string HomePageUrl => "https://www.themoviedb.org";

    // ── IHasCacheKey ──────────────────────────────────────────────────────────

    // Signature uses string? to match the interface definition in 10.11.
    public string GetCacheKey(string? userId) =>
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
                ChannelMediaType.Video
            },
            SupportsSortOrderToggle = true,
            SupportsLatestMedia = true,
            CanFilter = false
        };
    }

    // ── Channel items ─────────────────────────────────────────────────────────

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DynamicImageResponse { HasImage = false });
    }

    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return Enumerable.Empty<ImageType>();
    }

    public bool IsEnabledFor(string userId) => true;

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
            var matchingProviders = si.Providers.Where(p => enabledIds.Contains(p.ProviderId)).ToList();
            if (matchingProviders.Count == 0) continue;

            if (si.MediaType == "movie" && !config.IncludeMovies) continue;
            if (si.MediaType == "tv" && !config.IncludeTvShows) continue;

            items.Add(MapToChannelItem(si, matchingProviders));
        }

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

    // ── Cache management ──────────────────────────────────────────────────────

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
            Id = $"streaming-{si.TmdbId}-{si.MediaType}",
            Name = si.Title,
            Overview = si.Overview,
            Type = ChannelItemType.Media,
            ContentType = contentType,
            HomePageUrl = providers.FirstOrDefault()?.WebUrl,
            CommunityRating = si.VoteAverage > 0 ? (float)si.VoteAverage : null,
            Tagline = $"Streaming on: {providerSummary}",
            ProviderIds = new Dictionary<string, string>
            {
                ["tmdb"] = si.TmdbId.ToString()
            }
        };

        if (!string.IsNullOrEmpty(si.FullPosterUrl))
        {
            item.ImageUrl = si.FullPosterUrl;
            item.HasImage = true;
        }

        if (si.ReleaseYear.HasValue)
        {
            item.ProductionYear = si.ReleaseYear.Value;
        }

        item.Tags = new List<string>
        {
            $"streaming-providers:{string.Join(",", providers.Select(p => p.ProviderId))}",
            $"streaming-tmdb:{si.TmdbId}",
            $"streaming-type:{si.MediaType}"
        };

        return item;
    }
}
