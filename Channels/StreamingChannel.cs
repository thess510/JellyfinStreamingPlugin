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

    public string Name => "Streaming Discovery";

    public string Description =>
        "Browse streaming content from Hulu, Netflix, Disney+, and more — powered by TMDB.";

    public string DataVersion => "1";

    public string HomePageUrl => "https://www.themoviedb.org";

    public string GetCacheKey(string? userId) =>
        $"streaming-channel-{_lastCacheUpdate:yyyyMMddHHmm}";

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
            }
        };
    }

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

    public static void UpdateCache(List<StreamingItem> items)
    {
        _cachedItems = items;
        _lastCacheUpdate = DateTime.UtcNow;
    }

    public static IReadOnlyList<StreamingItem> GetCachedItems() => _cachedItems.AsReadOnly();

    private static ChannelItemInfo MapToChannelItem(StreamingItem si, List<WatchProvider> providers)
    {
        var providerSummary = string.Join(", ", providers.Select(p => p.Name));
        var contentType = si.MediaType == "movie"
            ? ChannelMediaContentType.Movie
            : ChannelMediaContentType.Episode;

        // Append streaming info to overview since Tagline was removed in 10.11
        var overview = string.IsNullOrEmpty(si.Overview)
            ? $"Streaming on: {providerSummary}"
            : $"{si.Overview}\n\nStreaming on: {providerSummary}";

        var item = new ChannelItemInfo
        {
            Id = $"streaming-{si.TmdbId}-{si.MediaType}",
            Name = si.Title,
            Overview = overview,
            Type = ChannelItemType.Media,
            ContentType = contentType,
            HomePageUrl = providers.FirstOrDefault()?.WebUrl,
            CommunityRating = si.VoteAverage > 0 ? (float)si.VoteAverage : null,
            ImageUrl = string.IsNullOrEmpty(si.FullPosterUrl) ? null : si.FullPosterUrl,
            ProviderIds = new Dictionary<string, string>
            {
                ["tmdb"] = si.TmdbId.ToString()
            },
            Tags = new List<string>
            {
                $"streaming-providers:{string.Join(",", providers.Select(p => p.ProviderId))}",
                $"streaming-tmdb:{si.TmdbId}",
                $"streaming-type:{si.MediaType}"
            }
        };

        if (si.ReleaseYear.HasValue)
        {
            item.ProductionYear = si.ReleaseYear.Value;
        }

        return item;
    }
}
