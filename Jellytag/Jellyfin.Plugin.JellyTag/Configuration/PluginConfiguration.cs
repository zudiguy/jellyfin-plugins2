using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// Badge position options.
/// </summary>
public enum BadgePosition
{
    /// <summary>
    /// Top left corner.
    /// </summary>
    TopLeft,

    /// <summary>
    /// Top right corner.
    /// </summary>
    TopRight,

    /// <summary>
    /// Bottom left corner.
    /// </summary>
    BottomLeft,

    /// <summary>
    /// Bottom right corner.
    /// </summary>
    BottomRight
}

/// <summary>
/// Settings for a specific image type (poster, thumbnail, backdrop).
/// </summary>
public class ImageTypeSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether badges are enabled for this image type.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the badge size as a percentage of the image width.
    /// </summary>
    public int BadgeSizePercent { get; set; }

    /// <summary>
    /// Gets or sets the badge margin in pixels.
    /// </summary>
    public int BadgeMargin { get; set; }

    /// <summary>
    /// Gets or sets the badge position.
    /// </summary>
    public BadgePosition BadgePosition { get; set; }
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Enabled = true;

        Show4K = true;
        Show1080p = true;
        Show720p = false;
        ShowSD = false;

        PosterSettings = new ImageTypeSettings
        {
            Enabled = true,
            BadgeSizePercent = 15,
            BadgeMargin = 10,
            BadgePosition = BadgePosition.TopLeft
        };

        ThumbnailSettings = new ImageTypeSettings
        {
            Enabled = false,
            BadgeSizePercent = 10,
            BadgeMargin = 5,
            BadgePosition = BadgePosition.TopRight
        };

        BackdropSettings = new ImageTypeSettings
        {
            Enabled = false,
            BadgeSizePercent = 8,
            BadgeMargin = 15,
            BadgePosition = BadgePosition.BottomRight
        };

        CacheDurationHours = 24;
        JpegQuality = 90;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show 4K badges.
    /// </summary>
    public bool Show4K { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show 1080p badges.
    /// </summary>
    public bool Show1080p { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show 720p badges.
    /// </summary>
    public bool Show720p { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show SD badges.
    /// </summary>
    public bool ShowSD { get; set; }

    /// <summary>
    /// Gets or sets the poster (Primary) image settings.
    /// </summary>
    public ImageTypeSettings PosterSettings { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail (Thumb) image settings.
    /// </summary>
    public ImageTypeSettings ThumbnailSettings { get; set; }

    /// <summary>
    /// Gets or sets the backdrop image settings.
    /// </summary>
    public ImageTypeSettings BackdropSettings { get; set; }

    /// <summary>
    /// Gets or sets the cache duration in hours.
    /// </summary>
    public int CacheDurationHours { get; set; }

    /// <summary>
    /// Gets or sets the JPEG quality for output images (1-100).
    /// </summary>
    public int JpegQuality { get; set; }
}
