# Jellyfin Plugins by Atilili

A collection of plugins for Jellyfin media server.

## Installation

1. Open Jellyfin and go to **Administration → Dashboard → Plugins → Repositories**
2. Click **Add** and enter:
   - **Name:** `Atilili Plugins`
   - **URL:** `https://raw.githubusercontent.com/Atilil/jellyfin-plugins/main/manifest.json`
3. Click **Save**
4. Go to **Catalog** tab and install the plugins you want
5. Restart Jellyfin

## Available Plugins

### WatchSync

![WatchSync](WatchSync/Jellyfin.Plugin.WatchSync/WatchSync.png)

Automatically synchronizes watch history between libraries of different qualities (4K/HD). When a movie is watched in 4K, the HD version is also marked as watched (and vice versa).

**Features:**
- Automatic sync on playback stop
- Smart matching via IMDB, TMDB, TVDB
- Support for movies and TV series
- Configurable completion threshold
- Library and user exclusion
- Full sync scheduled task

[More details](WatchSync/README.md)

---

## For Developers

Each plugin has its own folder with build instructions. See individual plugin READMEs.

### Requirements

- Jellyfin 10.11.0 or higher
- .NET 9 SDK (for building)

## License

MIT License - see [LICENSE](LICENSE) file.

## Author

**Atilili**

---

## Disclaimer

This project was developed with the assistance of AI (Claude by Anthropic). The code has been reviewed, tested, and validated before publication.
