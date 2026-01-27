using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.WatchSync.Services;

/// <summary>
/// Interface for the media matching service between libraries.
/// </summary>
public interface IMediaMatcher
{
    /// <summary>
    /// Finds all matching media items in other libraries.
    /// </summary>
    /// <param name="sourceItem">The source media item.</param>
    /// <returns>List of matching media items.</returns>
    IReadOnlyList<BaseItem> FindMatchingItems(BaseItem sourceItem);

    /// <summary>
    /// Checks if two media items match (same content, different quality).
    /// </summary>
    /// <param name="item1">First media item.</param>
    /// <param name="item2">Second media item.</param>
    /// <returns>True if the items match.</returns>
    bool AreItemsMatching(BaseItem item1, BaseItem item2);
}
