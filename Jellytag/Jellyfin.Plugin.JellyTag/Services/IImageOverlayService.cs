using Jellyfin.Plugin.JellyTag.Configuration;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Interface for image overlay service.
/// </summary>
public interface IImageOverlayService
{
    /// <summary>
    /// Adds a quality badge overlay to an image using per-image-type settings.
    /// </summary>
    /// <param name="originalImage">The original image stream.</param>
    /// <param name="quality">The video quality for the badge.</param>
    /// <param name="settings">The image type settings (size, margin, position).</param>
    /// <returns>A stream containing the image with the badge overlay.</returns>
    Task<Stream> AddBadgeOverlayAsync(Stream originalImage, VideoQuality quality, ImageTypeSettings settings);

    /// <summary>
    /// Determines if a badge should be shown for the given quality.
    /// </summary>
    /// <param name="quality">The video quality.</param>
    /// <returns>True if the badge should be shown based on configuration.</returns>
    bool ShouldShowBadge(VideoQuality quality);
}
