# Streaming Discovery — Jellyfin Plugin

Browse and search Hulu, Netflix, Disney+, Prime Video, and more directly inside Jellyfin. Powered by the [TMDB Watch Providers API](https://developer.themoviedb.org/docs/watch-providers) — no scraping, no DRM circumvention, no ToS violations.

**Phase 1 (this plugin):** Streaming content appears as a browsable Jellyfin library with posters, descriptions, ratings, and "Streaming on: Hulu, Netflix" labels. Tap Play in the web UI to open the service's website.

**Phase 2 (requires Neptune dev cooperation):** On Apple TV, tapping Play fires a native tvOS deep-link that opens the exact content in the streaming app directly.

---

## Install via Jellyfin Plugin Repository

This is the recommended install method — you'll get automatic updates.

1. In the Jellyfin admin dashboard, go to **Plugins → Repositories**
2. Click **Add** and paste this URL:
   ```
   https://raw.githubusercontent.com/thess510/JellyfinStreamingPlugin/main/manifest.json
   ```
3. Go to **Plugins → Catalog**, find **Streaming Discovery**, and click Install
4. Restart Jellyfin when prompted
5. Go to **Plugins → Streaming Discovery** and configure (see below)

---

## Configuration

| Setting | Description | Default |
|---|---|---|
| **TMDB API Key** | Required. Free key from [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api) | *(empty)* |
| **Country Code** | ISO 3166-1 alpha-2 code for watch provider availability | `US` |
| **Sync Interval** | How often to refresh content from TMDB (hours) | `6` |
| **Max Items Per Category** | Items fetched per sync (movies + TV separately) | `100` |
| **Content Types** | Toggle movies / TV shows | Both on |
| **Streaming Services** | Toggle each service | Netflix, Hulu, Disney+, Prime, Apple TV+, Max, Peacock on |

After saving, trigger the first sync manually: **Dashboard → Scheduled Tasks → Sync Streaming Content → Run**.

---

## API Endpoints

The plugin exposes a REST API for clients (and Phase 2 deep-link support):

### `GET /StreamingPlugin/status`
Health check. Confirms the plugin is running and configured.

### `GET /StreamingPlugin/providers/{tmdbId}?mediaType=movie|tv`
Returns streaming providers and deep-link URLs for a TMDB ID.

```json
{
  "tmdbId": 79744,
  "title": "The Rookie",
  "mediaType": "tv",
  "providers": [
    {
      "name": "Hulu",
      "logoUrl": "https://image.tmdb.org/t/p/w92/...",
      "deepLinkUrl": "hulu://",
      "webUrl": "https://www.hulu.com",
      "type": "flatrate"
    }
  ]
}
```

### `GET /StreamingPlugin/providers/jellyfin/{jellyfinItemId}`
Same as above but accepts the Jellyfin item ID (format: `streaming-{tmdbId}-{mediaType}`). This is what Neptune will call on Play tap.

### `GET /StreamingPlugin/catalog?mediaType=movie|tv&limit=50`
Returns the full cached catalog — useful for custom client UIs.

---

## Phase 2: Neptune Deep-Link Integration

When ready to contact the Neptune dev team, the ask is:

> "We've built a Jellyfin plugin that surfaces streaming catalog content via the Channels API. Items have a custom tag identifying them as external streaming content, and we expose a `/StreamingPlugin/providers/{id}` endpoint that returns deep-link URLs per provider. We'd like Neptune to: (1) detect these items via their `streaming-*` tag or item ID prefix, and (2) on Play tap, call our endpoint and fire the `deepLinkUrl` instead of attempting video playback."

Known tvOS deep-link schemes (not officially documented, may change):

| Service | Scheme |
|---|---|
| Hulu | `hulu://` |
| Netflix | `nflx://` |
| Disney+ | `disneyplus://` |
| Prime Video | `aiv://` |
| Apple TV+ | Handled natively by tvOS |
| Max | `max://` |
| Peacock | `peacocktv://` |

---

## Development

### Prerequisites
- .NET 8 SDK
- A free [TMDB API key](https://www.themoviedb.org/settings/api) for testing

### Build
```bash
git clone https://github.com/thess510/JellyfinStreamingPlugin
cd JellyfinStreamingPlugin
dotnet build -c Release
```

### Install locally
```bash
dotnet publish -c Release -o ./publish
docker cp ./publish/JellyfinStreamingPlugin.dll jellyfin:/config/plugins/JellyfinStreamingPlugin/
docker restart jellyfin
```

### Release a new version
```bash
git tag v1.1.0
git push origin v1.1.0
```
The GitHub Actions workflow will build, create a release, and update `manifest.json` automatically.

---

## Data Sources & Compliance

- **Metadata + Watch Providers:** [TMDB API](https://developer.themoviedb.org/) — free tier, 40 req/10s limit, explicitly permits use in third-party apps
- **No scraping:** All data comes from TMDB's official public API
- **No DRM circumvention:** The plugin only surfaces availability data; playback happens in the native licensed app
- **No ToS violations:** Deep-links open the official app at licensed content

*This product uses the TMDB API but is not endorsed or certified by TMDB.*

---

## Project Structure

```
JellyfinStreamingPlugin/
├── .github/workflows/build.yml     # CI: build → release → update manifest
├── manifest.json                   # Jellyfin plugin repository manifest
├── Plugin.cs                       # Plugin registration
├── PluginConfiguration.cs          # User settings
├── ServiceRegistrator.cs           # DI wiring
├── Channels/StreamingChannel.cs    # IChannel virtual library
├── Api/StreamingController.cs      # REST endpoints for deep-link data
├── Tasks/SyncStreamingContentTask.cs # Background TMDB sync
├── Services/
│   ├── TmdbService.cs              # TMDB API wrapper
│   └── WatchProviderService.cs     # Provider ID → deep-link mapping
├── Models/
│   ├── StreamingItem.cs
│   └── WatchProvider.cs
└── Configuration/configPage.html   # Admin UI config page
```
