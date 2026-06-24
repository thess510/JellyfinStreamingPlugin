using JellyfinStreamingPlugin.Channels;
using JellyfinStreamingPlugin.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinStreamingPlugin.Tasks;

public class SyncStreamingContentTask : IScheduledTask
{
    private readonly TmdbService _tmdbService;
    private readonly ILogger<SyncStreamingContentTask> _logger;

    public SyncStreamingContentTask(TmdbService tmdbService, ILogger<SyncStreamingContentTask> logger)
    {
        _tmdbService = tmdbService;
        _logger = logger;
    }

    public string Name => "Sync Streaming Content";
    public string Key => "StreamingDiscoverySync";
    public string Description => "Fetches trending movies and TV shows from TMDB and updates the Streaming Discovery library with current watch provider data.";
    public string Category => "Streaming Discovery";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var config = Plugin.Instance?.Configuration;
        var intervalHours = config?.SyncIntervalHours ?? 6;

        return new[]
        {
            new TaskTriggerInfo
            {
                // Use the string literal — TriggerInterval constant was removed in 10.11
                Type = "Interval",
                IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
            }
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting streaming content sync");

        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("Streaming sync skipped — TMDB API key not configured.");
            return;
        }

        var apiKey = config.TmdbApiKey;
        var country = config.CountryCode;
        var maxItems = config.MaxItemsPerCategory;
        var enabledProviders = config.EnabledProviderIds();

        var allItems = new List<JellyfinStreamingPlugin.Models.StreamingItem>();

        progress.Report(5);

        if (config.IncludeMovies)
        {
            var movies = await _tmdbService.GetTrendingMoviesAsync(apiKey, maxItems, cancellationToken);
            allItems.AddRange(movies);
            _logger.LogInformation("Got {Count} trending movies", movies.Count);
        }

        progress.Report(20);

        if (config.IncludeTvShows)
        {
            var tv = await _tmdbService.GetTrendingTvAsync(apiKey, maxItems, cancellationToken);
            allItems.AddRange(tv);
            _logger.LogInformation("Got {Count} trending TV shows", tv.Count);
        }

        progress.Report(40);

        _logger.LogInformation("Enriching {Count} items with watch provider data...", allItems.Count);

        for (var i = 0; i < allItems.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _tmdbService.EnrichWithProvidersAsync(
                allItems[i], apiKey, country, enabledProviders, cancellationToken);

            var pct = 40 + (55.0 * (i + 1) / allItems.Count);
            progress.Report(pct);
        }

        var itemsWithProviders = allItems
            .Where(x => x.Providers.Count > 0)
            .ToList();

        _logger.LogInformation(
            "Sync complete: {Total} fetched, {WithProviders} have enabled streaming providers",
            allItems.Count, itemsWithProviders.Count);

        StreamingChannel.UpdateCache(itemsWithProviders);
        progress.Report(100);
    }
}
