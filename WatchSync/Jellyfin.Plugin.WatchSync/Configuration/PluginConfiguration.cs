using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchSync.Configuration;

/// <summary>
/// Configuration for the WatchSync plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the configuration with default values.
    /// </summary>
    public PluginConfiguration()
    {
        IsEnabled = true;
        CompletionThreshold = 90;
        SyncWatchedStatus = true;
        SyncPlaybackPosition = true;
        SyncPlayCount = true;
        SyncLastPlayedDate = true;
        UseImdbId = true;
        UseTmdbId = true;
        UseTvdbId = true;
        ProviderPriority = "IMDB,TMDB,TVDB";
        UseTitleYearFallback = false;
        ExcludedLibraryIds = new List<string>();
        ExcludedUserIds = new List<string>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is globally enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the playback percentage threshold to consider media as "watched" (0-100).
    /// </summary>
    public int CompletionThreshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync watched/unwatched status.
    /// </summary>
    public bool SyncWatchedStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync playback resume position.
    /// </summary>
    public bool SyncPlaybackPosition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync play count.
    /// </summary>
    public bool SyncPlayCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync last played date.
    /// </summary>
    public bool SyncLastPlayedDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use IMDB ID for matching.
    /// </summary>
    public bool UseImdbId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use TMDB ID for matching.
    /// </summary>
    public bool UseTmdbId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use TVDB ID for matching.
    /// </summary>
    public bool UseTvdbId { get; set; }

    /// <summary>
    /// Gets or sets the provider priority order (comma-separated).
    /// </summary>
    public string ProviderPriority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use title + year as fallback when no provider ID matches.
    /// </summary>
    public bool UseTitleYearFallback { get; set; }

    /// <summary>
    /// Gets or sets the list of library IDs excluded from synchronization.
    /// </summary>
    public List<string> ExcludedLibraryIds { get; set; }

    /// <summary>
    /// Gets or sets the list of user IDs excluded from synchronization.
    /// </summary>
    public List<string> ExcludedUserIds { get; set; }
}
