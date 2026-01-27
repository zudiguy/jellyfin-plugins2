using System;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WatchSync.Services;

/// <summary>
/// Conflict resolution service for synchronization.
/// </summary>
public class ConflictResolver : IConflictResolver
{
    private readonly IUserDataManager _userDataManager;
    private readonly ILogger<ConflictResolver> _logger;

    /// <summary>
    /// Initializes the conflict resolution service.
    /// </summary>
    /// <param name="userDataManager">User data manager.</param>
    /// <param name="logger">Logger.</param>
    public ConflictResolver(IUserDataManager userDataManager, ILogger<ConflictResolver> logger)
    {
        _userDataManager = userDataManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public SyncData ResolveSyncData(BaseItem sourceItem, BaseItem targetItem, User user)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.IsEnabled)
        {
            return new SyncData { ShouldSync = false };
        }

        var sourceData = _userDataManager.GetUserData(user, sourceItem);
        var targetData = _userDataManager.GetUserData(user, targetItem);

        if (sourceData == null)
        {
            _logger.LogDebug("No source data for {ItemName}", sourceItem.Name);
            return new SyncData { ShouldSync = false };
        }

        var syncData = new SyncData { ShouldSync = false };

        // Sync watched/unwatched status
        if (config.SyncWatchedStatus)
        {
            if (sourceData.Played != (targetData?.Played ?? false))
            {
                syncData.Played = sourceData.Played;
                syncData.ShouldSync = true;
                _logger.LogDebug(
                    "Sync played status: {SourceName} ({Played}) -> {TargetName}",
                    sourceItem.Name,
                    sourceData.Played,
                    targetItem.Name);
            }
        }

        // Sync playback resume position
        if (config.SyncPlaybackPosition)
        {
            var sourcePosition = sourceData.PlaybackPositionTicks;
            var targetPosition = targetData?.PlaybackPositionTicks ?? 0;

            // Only sync if source position is more advanced
            // or if syncing an item not marked as watched (resume)
            if (sourcePosition > 0 && !sourceData.Played)
            {
                // Calculate percentage based on target media duration
                if (targetItem.RunTimeTicks.HasValue && targetItem.RunTimeTicks.Value > 0)
                {
                    // Adjust position proportionally if durations differ
                    var sourceRuntime = sourceItem.RunTimeTicks ?? targetItem.RunTimeTicks.Value;

                    // Prevent division by zero if source runtime is 0
                    if (sourceRuntime <= 0)
                    {
                        sourceRuntime = targetItem.RunTimeTicks.Value;
                    }

                    var adjustedPosition = (long)((double)sourcePosition / sourceRuntime * targetItem.RunTimeTicks.Value);

                    if (adjustedPosition != targetPosition)
                    {
                        syncData.PlaybackPositionTicks = adjustedPosition;
                        syncData.ShouldSync = true;
                        _logger.LogDebug(
                            "Sync position: {SourceName} ({Position} ticks) -> {TargetName}",
                            sourceItem.Name,
                            adjustedPosition,
                            targetItem.Name);
                    }
                }
            }
            else if (sourceData.Played && sourcePosition == 0)
            {
                // If marked as watched, reset position
                if (targetPosition > 0)
                {
                    syncData.PlaybackPositionTicks = 0;
                    syncData.ShouldSync = true;
                }
            }
        }

        // Sync play count
        if (config.SyncPlayCount)
        {
            var sourcePlayCount = sourceData.PlayCount;
            var targetPlayCount = targetData?.PlayCount ?? 0;

            // Use maximum of both counters (bidirectional)
            var maxPlayCount = Math.Max(sourcePlayCount, targetPlayCount);
            if (targetPlayCount < maxPlayCount)
            {
                syncData.PlayCount = maxPlayCount;
                syncData.ShouldSync = true;
                _logger.LogDebug(
                    "Sync play count: {SourceName} ({SourceCount}) -> {TargetName} (max: {MaxCount})",
                    sourceItem.Name,
                    sourcePlayCount,
                    targetItem.Name,
                    maxPlayCount);
            }
        }

        // Sync last played date
        if (config.SyncLastPlayedDate)
        {
            var sourceLastPlayed = sourceData.LastPlayedDate;
            var targetLastPlayed = targetData?.LastPlayedDate;

            // Use most recent date
            if (sourceLastPlayed.HasValue)
            {
                if (!targetLastPlayed.HasValue || sourceLastPlayed.Value > targetLastPlayed.Value)
                {
                    syncData.LastPlayedDate = sourceLastPlayed;
                    syncData.ShouldSync = true;
                    _logger.LogDebug(
                        "Sync last played date: {SourceName} ({Date}) -> {TargetName}",
                        sourceItem.Name,
                        sourceLastPlayed,
                        targetItem.Name);
                }
            }
        }

        return syncData;
    }
}
