using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Service for detecting video quality from media items.
/// </summary>
public class QualityDetectionService : IQualityDetectionService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<QualityDetectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityDetectionService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public QualityDetectionService(
        ILibraryManager libraryManager,
        ILogger<QualityDetectionService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public VideoQuality GetQuality(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item == null)
        {
            _logger.LogDebug("Item not found: {ItemId}", itemId);
            return VideoQuality.Unknown;
        }

        return GetQualityFromItem(item);
    }

    /// <summary>
    /// Determines the video quality based on resolution.
    /// Uses the maximum dimension to handle various aspect ratios correctly.
    /// </summary>
    /// <param name="width">The video width.</param>
    /// <param name="height">The video height.</param>
    /// <returns>The determined video quality.</returns>
    public static VideoQuality DetermineQuality(int width, int height)
    {
        // Use max dimension to handle various aspect ratios (16:9, 21:9, vertical, etc.)
        var maxDimension = Math.Max(width, height);

        // 4K UHD: 2160p or higher
        if (maxDimension >= 2160)
        {
            return VideoQuality.UHD4K;
        }

        // Full HD 1080p
        if (maxDimension >= 1080)
        {
            return VideoQuality.FHD1080p;
        }

        // HD 720p
        if (maxDimension >= 720)
        {
            return VideoQuality.HD720p;
        }

        // SD: anything below 720p
        if (maxDimension > 0)
        {
            return VideoQuality.SD;
        }

        return VideoQuality.Unknown;
    }

    /// <inheritdoc />
    public VideoQuality GetQualityFromItem(BaseItem item)
    {
        // Direct video item (Movie, Episode, MusicVideo, etc.)
        if (item is Video video)
        {
            return GetQualityFromVideo(video);
        }

        // For Series/Season/Folder: find the best quality among child video items
        var query = new InternalItemsQuery
        {
            ParentId = item.Id,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode],
            Limit = 10 // Check first 10 children for performance
        };
        var children = _libraryManager.GetItemList(query);
        var bestQuality = VideoQuality.Unknown;
        foreach (var child in children)
        {
            if (child is Video childVideo)
            {
                var q = GetQualityFromVideo(childVideo);
                if (q != VideoQuality.Unknown && (bestQuality == VideoQuality.Unknown || q > bestQuality))
                {
                    bestQuality = q;
                    if (bestQuality == VideoQuality.UHD4K)
                    {
                        break;
                    }
                }
            }
        }

        if (bestQuality != VideoQuality.Unknown)
        {
            _logger.LogDebug("Resolved quality {Quality} for parent item: {ItemName}", bestQuality, item.Name);
        }

        return bestQuality;
    }

    private VideoQuality GetQualityFromVideo(Video video)
    {
        try
        {
            var mediaSources = video.GetMediaSources(false);
            var mediaSource = mediaSources?.FirstOrDefault();
            var videoStream = mediaSource?.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (videoStream == null)
            {
                return VideoQuality.Unknown;
            }

            var width = videoStream.Width ?? 0;
            var height = videoStream.Height ?? 0;
            return DetermineQuality(width, height);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get media sources for video item: {ItemName}", video.Name);
            return VideoQuality.Unknown;
        }
    }
}
