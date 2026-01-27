using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WatchSync.Services;

/// <summary>
/// Main service that listens for playback events and synchronizes state between libraries.
/// </summary>
public class WatchSyncService : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly IMediaMatcher _mediaMatcher;
    private readonly IConflictResolver _conflictResolver;
    private readonly ILogger<WatchSyncService> _logger;

    // To prevent infinite loops during synchronization
    private readonly ConcurrentDictionary<string, DateTime> _syncInProgress = new();
    private static readonly TimeSpan SyncLockDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes the synchronization service.
    /// </summary>
    public WatchSyncService(
        ISessionManager sessionManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        IMediaMatcher mediaMatcher,
        IConflictResolver conflictResolver,
        ILogger<WatchSyncService> logger)
    {
        _sessionManager = sessionManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _mediaMatcher = mediaMatcher;
        _conflictResolver = conflictResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WatchSync started");
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WatchSync stopped");
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        return Task.CompletedTask;
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            ProcessPlaybackStopped(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PlaybackStopped event");
        }
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        try
        {
            ProcessUserDataSaved(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserDataSaved event");
        }
    }

    private void ProcessUserDataSaved(UserDataSaveEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.IsEnabled)
        {
            return;
        }

        // Only process watched status changes (TogglePlayed)
        if (e.SaveReason != UserDataSaveReason.TogglePlayed)
        {
            return;
        }

        var item = e.Item;
        var userId = e.UserId;

        if (item == null || userId == Guid.Empty)
        {
            return;
        }

        // Check if user is excluded
        if (config.ExcludedUserIds.Contains(userId.ToString()))
        {
            return;
        }

        // Check if sync is already in progress for this item (prevent infinite loop)
        var syncKey = $"{userId}:{item.Id}";
        if (IsSyncInProgress(syncKey))
        {
            _logger.LogDebug("Sync already in progress for {ItemName}, skipping", item.Name);
            return;
        }

        // Find matching media
        var matchingItems = _mediaMatcher.FindMatchingItems(item);

        if (matchingItems.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Manual toggle detected: {ItemName} (Played: {Played}), syncing to {Count} item(s)",
            item.Name,
            e.UserData?.Played,
            matchingItems.Count);

        // Sync to each matching item
        foreach (var targetItem in matchingItems)
        {
            var targetSyncKey = $"{userId}:{targetItem.Id}";
            MarkSyncInProgress(targetSyncKey);

            try
            {
                SyncUserDataToItem(item, targetItem, userId, e.UserData);
            }
            finally
            {
                // Lock will be automatically released after SyncLockDuration
            }
        }
    }

    private bool IsSyncInProgress(string key)
    {
        if (_syncInProgress.TryGetValue(key, out var timestamp))
        {
            if (DateTime.UtcNow - timestamp < SyncLockDuration)
            {
                return true;
            }

            // Lock has expired, remove it
            _syncInProgress.TryRemove(key, out _);
        }

        return false;
    }

    private void MarkSyncInProgress(string key)
    {
        // Purge expired entries to prevent memory accumulation
        var now = DateTime.UtcNow;
        var expiredKeys = _syncInProgress
            .Where(kvp => now - kvp.Value >= SyncLockDuration)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var expiredKey in expiredKeys)
        {
            _syncInProgress.TryRemove(expiredKey, out _);
        }

        _syncInProgress[key] = now;
    }

    private void SyncUserDataToItem(BaseItem sourceItem, BaseItem targetItem, Guid userId, UserItemData? sourceUserData)
    {
        try
        {
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return;
            }

            if (sourceUserData == null)
            {
                return;
            }

            var targetUserData = _userDataManager.GetUserData(user, targetItem);
            if (targetUserData == null)
            {
                return;
            }

            var config = Plugin.Instance?.Configuration;

            // Sync watched status
            if (config?.SyncWatchedStatus == true && targetUserData.Played != sourceUserData.Played)
            {
                targetUserData.Played = sourceUserData.Played;

                // If marked as watched, reset position
                if (sourceUserData.Played)
                {
                    targetUserData.PlaybackPositionTicks = 0;
                }

                // Also sync last played date
                if (config.SyncLastPlayedDate && sourceUserData.LastPlayedDate.HasValue)
                {
                    targetUserData.LastPlayedDate = sourceUserData.LastPlayedDate;
                }

                // Sync play count
                if (config.SyncPlayCount && sourceUserData.PlayCount > targetUserData.PlayCount)
                {
                    targetUserData.PlayCount = sourceUserData.PlayCount;
                }

                _userDataManager.SaveUserData(
                    user,
                    targetItem,
                    targetUserData,
                    UserDataSaveReason.UpdateUserData,
                    CancellationToken.None);

                _logger.LogInformation(
                    "Synced (manual toggle) {SourceName} -> {TargetName} (Played: {Played})",
                    sourceItem.Name,
                    targetItem.Name,
                    sourceUserData.Played);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing to {TargetName}", targetItem.Name);
        }
    }

    private void ProcessPlaybackStopped(PlaybackStopEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.IsEnabled)
        {
            _logger.LogDebug("Plugin disabled, sync skipped");
            return;
        }

        var item = e.Item;
        var userId = e.Session?.UserId ?? Guid.Empty;

        if (item == null || userId == Guid.Empty)
        {
            _logger.LogDebug("Item or user not found, sync skipped");
            return;
        }

        // Check if user is excluded
        if (config.ExcludedUserIds.Contains(userId.ToString()))
        {
            _logger.LogDebug("User {UserId} excluded, sync skipped", userId);
            return;
        }

        // Check completion threshold
        var percentComplete = CalculatePercentComplete(e);
        var isMarkedAsPlayed = percentComplete >= config.CompletionThreshold || e.PlayedToCompletion;

        _logger.LogDebug(
            "Playback stopped: {ItemName}, Progress: {Percent}%, MarkedAsPlayed: {Played}",
            item.Name,
            percentComplete,
            isMarkedAsPlayed);

        // Find matching media
        var matchingItems = _mediaMatcher.FindMatchingItems(item);

        if (matchingItems.Count == 0)
        {
            _logger.LogDebug("No matching media found for {ItemName}", item.Name);
            return;
        }

        _logger.LogInformation(
            "Syncing {ItemName} to {Count} matching item(s)",
            item.Name,
            matchingItems.Count);

        // Sync to each matching item
        foreach (var targetItem in matchingItems)
        {
            SyncToItem(item, targetItem, userId);
        }
    }

    private void SyncToItem(BaseItem sourceItem, BaseItem targetItem, Guid userId)
    {
        try
        {
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return;
            }

            var syncData = _conflictResolver.ResolveSyncData(sourceItem, targetItem, user);

            if (!syncData.ShouldSync)
            {
                _logger.LogDebug("No sync needed for {TargetName}", targetItem.Name);
                return;
            }

            var targetUserData = _userDataManager.GetUserData(user, targetItem);
            if (targetUserData == null)
            {
                _logger.LogWarning("No user data for {TargetName}", targetItem.Name);
                return;
            }

            // Apply changes
            if (syncData.Played.HasValue)
            {
                targetUserData.Played = syncData.Played.Value;
            }

            if (syncData.PlaybackPositionTicks.HasValue)
            {
                targetUserData.PlaybackPositionTicks = syncData.PlaybackPositionTicks.Value;
            }

            if (syncData.PlayCount.HasValue)
            {
                targetUserData.PlayCount = syncData.PlayCount.Value;
            }

            if (syncData.LastPlayedDate.HasValue)
            {
                targetUserData.LastPlayedDate = syncData.LastPlayedDate;
            }

            // Mark sync in progress to prevent loops
            var targetSyncKey = $"{userId}:{targetItem.Id}";
            MarkSyncInProgress(targetSyncKey);

            // Save changes
            _userDataManager.SaveUserData(
                user,
                targetItem,
                targetUserData,
                UserDataSaveReason.UpdateUserData,
                CancellationToken.None);

            _logger.LogInformation(
                "Synced {SourceName} -> {TargetName} (Played: {Played}, Position: {Position})",
                sourceItem.Name,
                targetItem.Name,
                syncData.Played,
                syncData.PlaybackPositionTicks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing to {TargetName}", targetItem.Name);
        }
    }

    private static double CalculatePercentComplete(PlaybackStopEventArgs e)
    {
        var positionTicks = e.PlaybackPositionTicks ?? 0;
        var runtimeTicks = e.Item?.RunTimeTicks ?? 0;

        if (runtimeTicks <= 0)
        {
            return 0;
        }

        return (double)positionTicks / runtimeTicks * 100;
    }
}
