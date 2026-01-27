using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.WatchSync.Services;

/// <summary>
/// Interface for synchronization conflict resolution.
/// </summary>
public interface IConflictResolver
{
    /// <summary>
    /// Resolves the data to synchronize between two items.
    /// </summary>
    /// <param name="sourceItem">Source item (the one just watched).</param>
    /// <param name="targetItem">Target item (the one to sync).</param>
    /// <param name="user">User.</param>
    /// <returns>Resolved data to apply.</returns>
    SyncData ResolveSyncData(BaseItem sourceItem, BaseItem targetItem, User user);
}

/// <summary>
/// Resolved synchronization data.
/// </summary>
public class SyncData
{
    /// <summary>
    /// Gets or sets a value indicating whether synchronization should occur.
    /// </summary>
    public bool ShouldSync { get; set; }

    /// <summary>
    /// Gets or sets the new "played" status to apply.
    /// </summary>
    public bool? Played { get; set; }

    /// <summary>
    /// Gets or sets the new playback position in ticks.
    /// </summary>
    public long? PlaybackPositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the new play count.
    /// </summary>
    public int? PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the new last played date.
    /// </summary>
    public System.DateTime? LastPlayedDate { get; set; }
}
