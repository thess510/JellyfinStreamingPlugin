using JellyfinStreamingPlugin.Channels;
using JellyfinStreamingPlugin.Models;
using JellyfinStreamingPlugin.Services;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JellyfinStreamingPlugin.Api;

/// <summary>
/// Custom REST API endpoint that exposes streaming provider and deep-link data per item.
///
/// This is the Phase 2 hook: Neptune (and any other client) calls this endpoint
/// when a user taps Play on a streaming item, reads deepLinkUrl, and fires it
/// instead of attempting video playback.
///
/// Base route: /StreamingPlugin/
/// </summary>
[ApiController]
[Route("StreamingPlugin")]
[Authorize(Policy = "DefaultAuthorization")]
public class StreamingController : ControllerBase
{
    private readonly ILogger<StreamingController> _logger;

    public StreamingController(ILogger<StreamingController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// GET /StreamingPlugin/providers/{tmdbId}
    ///
    /// Returns streaming provider and deep-link info for a given TMDB ID.
    /// Neptune calls this on Play tap to get the deep-link URL.
    ///
    /// Response shape matches the handoff spec exactly.
    /// </summary>
    [HttpGet("providers/{tmdbId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetProviders(int tmdbId, [FromQuery] string? mediaType = null)
    {
        _logger.LogDebug("GetProviders called for TMDB ID {TmdbId}", tmdbId);

        var cachedItems = StreamingChannel.GetCachedItems();
        StreamingItem? item;

        if (mediaType is not null)
        {
            item = cachedItems.FirstOrDefault(x => x.TmdbId == tmdbId && x.MediaType == mediaType);
        }
        else
        {
            item = cachedItems.FirstOrDefault(x => x.TmdbId == tmdbId);
        }

        if (item is null)
        {
            return NotFound(new { error = $"No streaming item found for TMDB ID {tmdbId}" });
        }

        var config = Plugin.Instance?.Configuration;
        var enabledIds = config?.EnabledProviderIds() ?? new HashSet<int>();

        var providers = item.Providers
            .Where(p => enabledIds.Contains(p.ProviderId))
            .Select(p => new ProviderResponse
            {
                Name = p.Name,
                LogoUrl = p.FullLogoUrl,
                DeepLinkUrl = p.DeepLinkUrl ?? string.Empty,
                WebUrl = p.WebUrl ?? string.Empty,
                Type = p.AvailabilityType
            })
            .ToList();

        return Ok(new ItemProvidersResponse
        {
            TmdbId = item.TmdbId,
            Title = item.Title,
            MediaType = item.MediaType,
            Providers = providers
        });
    }

    /// <summary>
    /// GET /StreamingPlugin/providers/jellyfin/{jellyfinItemId}
    ///
    /// Alternate lookup by Jellyfin item ID (the "streaming-{tmdbId}-{mediaType}" ID
    /// we assign in StreamingChannel). This is what Neptune will most naturally use
    /// since it knows the Jellyfin item ID, not the TMDB ID.
    /// </summary>
    [HttpGet("providers/jellyfin/{jellyfinItemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetProvidersByJellyfinId(string jellyfinItemId)
    {
        _logger.LogDebug("GetProvidersByJellyfinId called for {JellyfinItemId}", jellyfinItemId);

        // Parse our ID format: "streaming-{tmdbId}-{mediaType}"
        // e.g. "streaming-79744-tv"
        var parts = jellyfinItemId.Split('-');
        if (parts.Length < 3 || parts[0] != "streaming" || !int.TryParse(parts[1], out var tmdbId))
        {
            return BadRequest(new { error = "Invalid Jellyfin item ID format. Expected: streaming-{tmdbId}-{mediaType}" });
        }
        var mediaType = parts[2];

        return GetProviders(tmdbId, mediaType);
    }

    /// <summary>
    /// GET /StreamingPlugin/catalog
    ///
    /// Returns the full cached streaming catalog (lightweight — titles, IDs, providers only).
    /// Useful for clients that want to pre-load the catalog or build a custom UI.
    /// Supports optional filtering by mediaType ("movie" or "tv").
    /// </summary>
    [HttpGet("catalog")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCatalog([FromQuery] string? mediaType = null, [FromQuery] int? limit = null)
    {
        var cachedItems = StreamingChannel.GetCachedItems();
        var config = Plugin.Instance?.Configuration;
        var enabledIds = config?.EnabledProviderIds() ?? new HashSet<int>();

        var query = cachedItems.AsEnumerable();

        if (mediaType is not null)
            query = query.Where(x => x.MediaType == mediaType);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var results = query
            .Select(item => new CatalogItemResponse
            {
                TmdbId = item.TmdbId,
                JellyfinItemId = $"streaming-{item.TmdbId}-{item.MediaType}",
                Title = item.Title,
                MediaType = item.MediaType,
                PosterUrl = item.FullPosterUrl,
                ReleaseYear = item.ReleaseYear,
                VoteAverage = item.VoteAverage,
                Providers = item.Providers
                    .Where(p => enabledIds.Contains(p.ProviderId))
                    .Select(p => p.Name)
                    .ToList()
            })
            .ToList();

        return Ok(new { totalCount = results.Count, items = results });
    }

    /// <summary>
    /// GET /StreamingPlugin/status
    ///
    /// Health check / config status. Useful for debugging during setup.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        var cachedItems = StreamingChannel.GetCachedItems();

        return Ok(new
        {
            pluginVersion = Plugin.Instance?.Version?.ToString() ?? "unknown",
            configured = config is not null && !string.IsNullOrWhiteSpace(config.TmdbApiKey),
            countryCode = config?.CountryCode ?? "not set",
            cachedItemCount = cachedItems.Count,
            syncIntervalHours = config?.SyncIntervalHours ?? 0,
            enabledServices = config?.EnabledProviderIds().Select(WatchProviderService.GetProviderName).ToList()
                              ?? new List<string>()
        });
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    public class ItemProvidersResponse
    {
        public int TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public List<ProviderResponse> Providers { get; set; } = new();
    }

    public class ProviderResponse
    {
        public string Name { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string DeepLinkUrl { get; set; } = string.Empty;
        public string WebUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class CatalogItemResponse
    {
        public int TmdbId { get; set; }
        public string JellyfinItemId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public int? ReleaseYear { get; set; }
        public double VoteAverage { get; set; }
        public List<string> Providers { get; set; } = new();
    }
}
