# Streaming Discovery

A Jellyfin plugin that adds a browsable streaming library to your server — movies and TV shows from Hulu, Netflix, Disney+, and more, with posters, descriptions, and ratings pulled from TMDB. When you find something you want to watch, it links you directly to the service.

Powered by the [TMDB Watch Providers API](https://developer.themoviedb.org/docs/watch-providers). No scraping involved.

---

## Installation

1. In the Jellyfin dashboard, go to **Plugins → Repositories → Add** and paste:
   ```
   https://raw.githubusercontent.com/thess510/JellyfinStreamingPlugin/main/manifest.json
   ```
2. Go to **Plugins → Catalog**, find **Streaming Discovery**, and install it
3. Restart Jellyfin
4. Open the plugin settings and add your TMDB API key (see below)

---

## Setup

You'll need a free TMDB API key — grab one at [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api).

| Setting | Description | Default |
|---|---|---|
| TMDB API Key | Required | — |
| Country Code | For watch provider availability (e.g. `US`, `GB`) | `US` |
| Sync Interval | How often to refresh content, in hours | `6` |
| Max Items Per Category | How many movies/shows to sync | `100` |
| Content Types | Movies, TV, or both | Both |
| Streaming Services | Toggle each service on/off | Netflix, Hulu, Disney+, Prime, Apple TV+, Max, Peacock |

After saving, kick off the first sync manually: **Dashboard → Scheduled Tasks → Sync Streaming Content → Run**.

---

## Building from Source

Requires .NET 8 SDK.

```bash
git clone https://github.com/thess510/JellyfinStreamingPlugin
cd JellyfinStreamingPlugin
dotnet build -c Release
```

---

*This product uses the TMDB API but is not endorsed or certified by TMDB.*
