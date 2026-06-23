using JellyfinStreamingPlugin.Channels;
using JellyfinStreamingPlugin.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinStreamingPlugin.Tasks;

/// <summary>
/// Jellyfin scheduled task that syncs trending content from TMDB on a configurable interval.
/// Jellyfin's task scheduler discovers this via DI — it must be registered as IScheduledTask.
///
/// Flow:
///   1. Fetch trending movies and/or TV from TMDB
///   2. For each item, fetch watch provider data
///   3. Update StreamingChannel's in-memory cache
///   4. Jellyfin will pick up the changes next time the channel is browsed
/// </summary>
public class SyncStreamingContentTask : IScheduledTask
{
    private readonly TmdbService _tmdbService;
    private readonly ILogger<SyncStreamingContentTask> _logger;

    public SyncStreamingContentTask(TmdbService tmdbService, ILogger<SyncStreamingContentTask> logger)
    {
        _tmdbService = tmdbService;
        _logger = logger;
    }

    // ── IScheduledTask identity ───────────────────────────────────────────────

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
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
            }
        };
    }

    // ── Task execution ────────────────────────────────────────────────────────

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting streaming content sync");

        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("Streaming sync skipped — TMDB API key not configured. Set it in Dashboard → Plugins → Streaming Discovery.");
            return;
        }

        var apiKey = config.TmdbApiKey;
        var country = config.CountryCode;
        var maxItems = config.MaxItemsPerCategory;
        var enabledProviders = config.EnabledProviderIds();

        var allItems = new List<JellyfinStreamingPlugin.Models.StreamingItem>();

        // ── Step 1: Fetch trending content (40% of progress) ─────────────────

        progress.Report(5);

        if (config.IncludeMovies)
        {
            _logger.LogInformation("Fetching trending movies (max {Max})...", maxItems);
            var movies = await _tmdbService.GetTrendingMoviesAsync(apiKey, maxItems, cancellationToken);
            allItems.AddRange(movies);
            _logger.LogInformation("Got {Count} trending movies", movies.Count);
        }

        progress.Report(20);

        if (config.IncludeTvShows)
        {
            _logger.LogInformation("Fetching trending TV shows (max {Max})...", maxItems);
            var tv = await _tmdbService.GetTrendingTvAsync(apiKey, maxItems, cancellationToken);
            allItems.AddRange(tv);
            _logger.LogInformation("Got {Count} trending TV shows", tv.Count);
        }

        progress.Report(40);

        // ── Step 2: Enrich with watch providers (remaining 55% of progress) ──

        _logger.LogInformation("Enriching {Count} items with watch provider data...", allItems.Count);

        for (var i = 0; i < allItems.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _tmdbService.EnrichWithProvidersAsync(
                allItems[i], apiKey, country, enabledProviders, cancellationToken);

            // Progress: 40% → 95% over all items
            var pct = 40 + (55.0 * (i + 1) / allItems.Count);
            progress.Report(pct);
        }

        // ── Step 3: Filter to only items with at least one enabled provider ──

        var itemsWithProviders = allItems
            .Where(x => x.Providers.Count > 0)
            .ToList();

        _logger.LogInformation(
            "Sync complete: {Total} fetched, {WithProviders} have enabled streaming providers",
            allItems.Count, itemsWithProviders.Count);

        // ── Step 4: Update the channel cache ─────────────────────────────────

        StreamingChannel.UpdateCache(itemsWithProviders);
        progress.Report(100);
    }
}
