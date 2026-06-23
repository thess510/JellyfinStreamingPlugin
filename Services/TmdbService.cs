using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JellyfinStreamingPlugin.Configuration;
using JellyfinStreamingPlugin.Models;
using Microsoft.Extensions.Logging;

namespace JellyfinStreamingPlugin.Services;

/// <summary>
/// Wraps all TMDB API calls. Handles rate limiting (40 req/10 sec on free tier)
/// by inserting a small delay between requests.
/// </summary>
public class TmdbService
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(300); // safe under 40/10s

    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbService> _logger;

    public TmdbService(IHttpClientFactory httpClientFactory, ILogger<TmdbService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TmdbClient");
        _logger = logger;
    }

    // ── Trending ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the weekly trending movies. Max 20 per page; we page through up to maxItems.
    /// </summary>
    public async Task<List<StreamingItem>> GetTrendingMoviesAsync(string apiKey, int maxItems, CancellationToken ct)
    {
        return await FetchTrendingAsync(apiKey, "movie", maxItems, ct);
    }

    /// <summary>
    /// Returns the weekly trending TV shows.
    /// </summary>
    public async Task<List<StreamingItem>> GetTrendingTvAsync(string apiKey, int maxItems, CancellationToken ct)
    {
        return await FetchTrendingAsync(apiKey, "tv", maxItems, ct);
    }

    private async Task<List<StreamingItem>> FetchTrendingAsync(
        string apiKey, string mediaType, int maxItems, CancellationToken ct)
    {
        var results = new List<StreamingItem>();
        var page = 1;

        while (results.Count < maxItems)
        {
            var url = $"{BaseUrl}/trending/{mediaType}/week?api_key={apiKey}&page={page}";
            var response = await SafeGetAsync<TmdbPagedResponse<TmdbMediaResult>>(url, ct);
            if (response?.Results is null || response.Results.Count == 0) break;

            foreach (var r in response.Results)
            {
                if (results.Count >= maxItems) break;
                results.Add(MapToStreamingItem(r, mediaType));
            }

            if (page >= response.TotalPages) break;
            page++;
            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("Fetched {Count} trending {MediaType} items from TMDB", results.Count, mediaType);
        return results;
    }

    // ── Watch Providers ───────────────────────────────────────────────────────

    /// <summary>
    /// Fetches watch provider data for an item and attaches it to the StreamingItem.
    /// Only populates flatrate (subscription) providers in the target country.
    /// </summary>
    public async Task EnrichWithProvidersAsync(
        StreamingItem item,
        string apiKey,
        string countryCode,
        HashSet<int> enabledProviderIds,
        CancellationToken ct)
    {
        var endpoint = item.MediaType == "movie" ? "movie" : "tv";
        var url = $"{BaseUrl}/{endpoint}/{item.TmdbId}/watch/providers?api_key={apiKey}";

        var response = await SafeGetAsync<TmdbWatchProviderResponse>(url, ct);
        if (response?.Results is null) return;

        if (!response.Results.TryGetValue(countryCode, out var countryData)) return;

        var flatrate = countryData.Flatrate ?? new List<TmdbProviderEntry>();
        item.Providers = flatrate
            .Where(p => enabledProviderIds.Contains(p.ProviderId))
            .Select(p => new WatchProvider
            {
                ProviderId = p.ProviderId,
                Name = p.ProviderName,
                LogoPath = p.LogoPath,
                DeepLinkUrl = WatchProviderService.GetDeepLink(p.ProviderId),
                WebUrl = WatchProviderService.GetWebUrl(p.ProviderId, item),
                AvailabilityType = "flatrate"
            })
            .ToList();

        await Task.Delay(RateLimitDelay, ct);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-search across movies and TV shows. Used to back the custom API endpoint
    /// for on-demand lookups (not the background sync).
    /// </summary>
    public async Task<List<StreamingItem>> SearchAsync(string apiKey, string query, CancellationToken ct)
    {
        var url = $"{BaseUrl}/search/multi?api_key={apiKey}&query={Uri.EscapeDataString(query)}";
        var response = await SafeGetAsync<TmdbPagedResponse<TmdbMediaResult>>(url, ct);
        if (response?.Results is null) return new List<StreamingItem>();

        return response.Results
            .Where(r => r.MediaType is "movie" or "tv")
            .Select(r => MapToStreamingItem(r, r.MediaType ?? "movie"))
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<T?> SafeGetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB request failed: {Status} for {Url}", response.StatusCode, url);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling TMDB API: {Url}", url);
            return default;
        }
    }

    private static StreamingItem MapToStreamingItem(TmdbMediaResult r, string mediaType)
    {
        // TMDB returns 'name' for TV, 'title' for movies
        var title = r.Title ?? r.Name ?? "Unknown";
        var releaseDate = r.ReleaseDate ?? r.FirstAirDate;

        return new StreamingItem
        {
            TmdbId = r.Id,
            MediaType = mediaType,
            Title = title,
            Overview = r.Overview ?? string.Empty,
            PosterPath = r.PosterPath,
            BackdropPath = r.BackdropPath,
            ReleaseDate = releaseDate,
            VoteAverage = r.VoteAverage,
            VoteCount = r.VoteCount,
            GenreIds = r.GenreIds ?? new List<int>()
        };
    }

    // ── TMDB DTO models (private — only used inside this service) ─────────────

    private class TmdbPagedResponse<T>
    {
        [JsonPropertyName("results")] public List<T>? Results { get; set; }
        [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
        [JsonPropertyName("total_results")] public int TotalResults { get; set; }
    }

    private class TmdbMediaResult
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("media_type")] public string? MediaType { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
        [JsonPropertyName("vote_count")] public int VoteCount { get; set; }
        [JsonPropertyName("genre_ids")] public List<int>? GenreIds { get; set; }
    }

    private class TmdbWatchProviderResponse
    {
        [JsonPropertyName("results")]
        public Dictionary<string, TmdbCountryProviders>? Results { get; set; }
    }

    private class TmdbCountryProviders
    {
        [JsonPropertyName("flatrate")] public List<TmdbProviderEntry>? Flatrate { get; set; }
        [JsonPropertyName("rent")] public List<TmdbProviderEntry>? Rent { get; set; }
        [JsonPropertyName("buy")] public List<TmdbProviderEntry>? Buy { get; set; }
    }

    private class TmdbProviderEntry
    {
        [JsonPropertyName("provider_id")] public int ProviderId { get; set; }
        [JsonPropertyName("provider_name")] public string ProviderName { get; set; } = string.Empty;
        [JsonPropertyName("logo_path")] public string? LogoPath { get; set; }
    }
}
