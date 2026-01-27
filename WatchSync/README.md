# WatchSync

Jellyfin plugin to automatically synchronize watch history between libraries of different qualities (4K/HD).

![WatchSync Logo](Jellyfin.Plugin.WatchSync/WatchSync.png)

## Features

- **Automatic synchronization**: When media is watched, all matching versions are updated
- **Smart identification**: Uses IMDB, TMDB and TVDB to match media
- **Full support**: Movies and TV series (episodes)
- **Granular configuration**:
  - Customizable completion threshold
  - Choice of data to sync (status, position, play count, date)
  - Exclude specific libraries or users
- **Full sync task**: Scheduled task to synchronize all history

## Installation

See the [main repository README](../README.md) for installation via Jellyfin plugin repository.

## Configuration

After installation, configure the plugin in: **Administration → Plugins → WatchSync**

### Media Identification

The plugin uses external identifiers in this priority order:
1. **IMDB ID**
2. **TMDB ID**
3. **TVDB ID**
4. **Title + Year** (fallback, disabled by default)

## Building from Source

```bash
cd WatchSync
./build.sh
```

Or manually:

```bash
cd Jellyfin.Plugin.WatchSync
dotnet build -c Release
```

The DLL will be generated in `bin/Release/net9.0/`.

## How It Works

The plugin listens to the `PlaybackStopped` event via `ISessionManager`. When media playback ends:

1. Checks if the plugin is enabled and user is not excluded
2. Calculates completion percentage
3. Searches for matching media via `MediaMatcher`
4. Resolves data to sync via `ConflictResolver`
5. Updates `UserData` for matching media

## Architecture

```
Jellyfin.Plugin.WatchSync/
├── Plugin.cs                    # Entry point
├── PluginServiceRegistrator.cs  # Dependency injection
├── Configuration/
│   ├── PluginConfiguration.cs   # Configuration model
│   └── configPage.html          # Admin interface
├── Services/
│   ├── WatchSyncService.cs      # Main service (listens to PlaybackStopped)
│   ├── MediaMatcher.cs          # Matching logic
│   └── ConflictResolver.cs      # Data resolution
├── Tasks/
│   └── FullSyncTask.cs          # Scheduled task
└── Utils/
    └── TitleUtils.cs            # Title normalization
```

---

*This plugin was developed with the assistance of AI (Claude by Anthropic).*
