using System.Reflection;
using Jellyfin.Plugin.JellyTag.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Service for adding quality badge overlays to images.
/// </summary>
public class ImageOverlayService : IImageOverlayService, IDisposable
{
    private readonly ILogger<ImageOverlayService> _logger;
    private readonly Dictionary<VideoQuality, Image<Rgba32>?> _badgeCache = new();
    private readonly SemaphoreSlim _badgeLock = new(1, 1);
    private bool _badgesLoaded;
    private bool _disposed;

    // Default safe values for configuration
    private const int DefaultBadgeSizePercent = 15;
    private const int DefaultBadgeMargin = 10;
    private const int DefaultJpegQuality = 90;
    private const int MinBadgeSizePercent = 5;
    private const int MaxBadgeSizePercent = 50;
    private const int MinBadgeMargin = 0;
    private const int MaxBadgeMargin = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageOverlayService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ImageOverlayService(ILogger<ImageOverlayService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Stream> AddBadgeOverlayAsync(Stream originalImage, VideoQuality quality, ImageTypeSettings settings)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        // Validate and clamp configuration values
        var badgeSizePercent = Math.Clamp(settings.BadgeSizePercent, MinBadgeSizePercent, MaxBadgeSizePercent);
        var badgeMargin = Math.Clamp(settings.BadgeMargin, MinBadgeMargin, MaxBadgeMargin);
        var jpegQuality = Math.Clamp(config.JpegQuality, 50, 100);

        using var image = await Image.LoadAsync<Rgba32>(originalImage).ConfigureAwait(false);

        var badge = await GetBadgeAsync(quality).ConfigureAwait(false);

        if (badge == null)
        {
            originalImage.Position = 0;
            var output = new MemoryStream();
            await originalImage.CopyToAsync(output).ConfigureAwait(false);
            output.Position = 0;
            return output;
        }

        // Clone the badge so we can resize it without affecting the cached version
        using var badgeCopy = badge.Clone();

        // Resize badge to configured percentage of image width
        var badgeWidth = Math.Max(1, (int)(image.Width * (badgeSizePercent / 100.0)));
        var badgeHeight = Math.Max(1, (int)(badgeCopy.Height * ((double)badgeWidth / badgeCopy.Width)));
        badgeCopy.Mutate(x => x.Resize(badgeWidth, badgeHeight));

        // Calculate position based on per-image-type settings
        var position = CalculateBadgePosition(image.Width, image.Height, badgeWidth, badgeHeight, settings.BadgePosition, badgeMargin);

        // Manual alpha compositing - more reliable than DrawImage which can produce gray rectangles
        image.ProcessPixelRows(badgeCopy, (imageAccessor, badgeAccessor) =>
        {
            for (int y = 0; y < badgeAccessor.Height; y++)
            {
                int destY = position.Y + y;
                if (destY < 0 || destY >= imageAccessor.Height)
                {
                    continue;
                }

                var destRow = imageAccessor.GetRowSpan(destY);
                var srcRow = badgeAccessor.GetRowSpan(y);

                for (int x = 0; x < badgeAccessor.Width; x++)
                {
                    int destX = position.X + x;
                    if (destX < 0 || destX >= imageAccessor.Width)
                    {
                        continue;
                    }

                    var src = srcRow[x];
                    if (src.A == 0)
                    {
                        continue;
                    }

                    if (src.A == 255)
                    {
                        destRow[destX] = src;
                    }
                    else
                    {
                        var dst = destRow[destX];
                        float srcA = src.A / 255f;
                        float invA = 1f - srcA;
                        destRow[destX] = new Rgba32(
                            (byte)(src.R * srcA + dst.R * invA),
                            (byte)(src.G * srcA + dst.G * invA),
                            (byte)(src.B * srcA + dst.B * invA),
                            255);
                    }
                }
            }
        });

        // Save to output stream
        var outputStream = new MemoryStream();
        var encoder = new JpegEncoder
        {
            Quality = jpegQuality
        };
        await image.SaveAsync(outputStream, encoder).ConfigureAwait(false);
        outputStream.Position = 0;

        return outputStream;
    }

    /// <inheritdoc />
    public bool ShouldShowBadge(VideoQuality quality)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.Enabled)
        {
            return false;
        }

        return quality switch
        {
            VideoQuality.UHD4K => config.Show4K,
            VideoQuality.FHD1080p => config.Show1080p,
            VideoQuality.HD720p => config.Show720p,
            VideoQuality.SD => config.ShowSD,
            _ => false
        };
    }

    private async Task<Image<Rgba32>?> GetBadgeAsync(VideoQuality quality)
    {
        await _badgeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_badgesLoaded)
            {
                await LoadBadgesAsync().ConfigureAwait(false);
                _badgesLoaded = true;
            }

            return _badgeCache.GetValueOrDefault(quality);
        }
        finally
        {
            _badgeLock.Release();
        }
    }

    private async Task LoadBadgesAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        _logger.LogInformation("[JellyTag] Loading badges. Available resources: {Resources}", string.Join(", ", resourceNames));

        var badgeMapping = new Dictionary<VideoQuality, string>
        {
            { VideoQuality.UHD4K, "badge-4k.png" },
            { VideoQuality.FHD1080p, "badge-1080p.png" },
            { VideoQuality.HD720p, "badge-720p.png" },
            { VideoQuality.SD, "badge-sd.png" }
        };

        foreach (var (videoQuality, fileName) in badgeMapping)
        {
            var resourceName = resourceNames.FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                _logger.LogInformation("[JellyTag] Badge resource not found: {FileName}", fileName);
                continue;
            }

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _logger.LogInformation("[JellyTag] Loading badge {FileName}, stream length: {Length}", fileName, stream.Length);
                    var badge = await Image.LoadAsync<Rgba32>(stream).ConfigureAwait(false);
                    _badgeCache[videoQuality] = badge;
                    _logger.LogInformation("[JellyTag] Loaded badge for {Quality}: {Width}x{Height}", videoQuality, badge.Width, badge.Height);
                }
                else
                {
                    _logger.LogInformation("[JellyTag] Stream is null for resource: {Resource}", resourceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JellyTag] Failed to load badge: {FileName}", fileName);
            }
        }

        _logger.LogInformation("[JellyTag] Badge loading complete. Loaded {Count} badges", _badgeCache.Count);
    }

    private static Point CalculateBadgePosition(int imageWidth, int imageHeight, int badgeWidth, int badgeHeight, BadgePosition position, int margin)
    {
        // Ensure position stays within image bounds
        var maxX = Math.Max(0, imageWidth - badgeWidth - margin);
        var maxY = Math.Max(0, imageHeight - badgeHeight - margin);

        return position switch
        {
            BadgePosition.TopLeft => new Point(margin, margin),
            BadgePosition.TopRight => new Point(maxX, margin),
            BadgePosition.BottomLeft => new Point(margin, maxY),
            BadgePosition.BottomRight => new Point(maxX, maxY),
            _ => new Point(margin, margin)
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose cached badge images
            foreach (var badge in _badgeCache.Values)
            {
                badge?.Dispose();
            }

            _badgeCache.Clear();
            _badgeLock.Dispose();
        }

        _disposed = true;
    }
}
