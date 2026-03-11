using System.Reflection;
using Jellyfin.Plugin.JellyTag.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Service for applying Kometa-style overlays with gradients and combined badges.
/// </summary>
public class KometaOverlayService : IDisposable
{
    private readonly ILogger<KometaOverlayService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap?> _kometaCache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _assetsLoaded;
    private bool _disposed;

    // Kometa asset paths (embedded resources)
    private const string GradientsPath = "kometa.gradients";
    private const string ResolutionPath = "kometa.resolution";
    private const string CodecPath = "kometa.codec";
    private const string RatingsPath = "kometa.ratings";

    public KometaOverlayService(ILogger<KometaOverlayService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the appropriate gradient for the media type.
    /// </summary>
    /// <param name="isMovie">True for movies, false for TV episodes.</param>
    /// <param name="position">Top or Bottom position.</param>
    /// <returns>The gradient bitmap or null if not found.</returns>
    public async Task<SKBitmap?> GetGradientAsync(bool isMovie, bool isTop)
    {
        await EnsureAssetsLoaded().ConfigureAwait(false);

        var gradientName = isMovie
            ? (isTop ? "gradient_top.png" : "gradient_bottom.png")
            : (isTop ? "gradient_episode_top.png" : "gradient_episode_bottom.png");

        _kometaCache.TryGetValue($"gradients/{gradientName}", out var gradient);
        return gradient;
    }

    /// <summary>
    /// Gets the resolution badge for the given quality.
    /// </summary>
    public async Task<SKBitmap?> GetResolutionBadgeAsync(string resolution)
    {
        await EnsureAssetsLoaded().ConfigureAwait(false);

        var fileName = resolution.ToUpperInvariant() switch
        {
            "4K" or "2160P" => "Ultra-HD.png",
            "1080P" => "1080P.png",
            "720P" => "720P.png",
            "576P" => "576P.png",
            "480P" => "480P.png",
            "SD" => "SD.png",
            _ => null
        };

        if (fileName == null) return null;

        _kometaCache.TryGetValue($"resolution/{fileName}", out var badge);
        return badge;
    }

    /// <summary>
    /// Gets the combined codec/HDR/audio badge.
    /// Kometa badges combine multiple elements like "DV-HDR-TrueHD-Atmos.png".
    /// </summary>
    public async Task<SKBitmap?> GetCombinedCodecBadgeAsync(string? hdrType, string? audioCodec)
    {
        await EnsureAssetsLoaded().ConfigureAwait(false);

        // Try to find the best matching combined badge
        var candidates = new List<string>();

        // Build candidate names based on HDR and audio combination
        if (!string.IsNullOrEmpty(hdrType) && !string.IsNullOrEmpty(audioCodec))
        {
            var hdr = NormalizeHdrForKometa(hdrType);
            var audio = NormalizeAudioForKometa(audioCodec);

            if (hdr != null && audio != null)
            {
                candidates.Add($"{hdr}-{audio}.png");
            }
        }

        // Try HDR only
        if (!string.IsNullOrEmpty(hdrType))
        {
            var hdr = NormalizeHdrForKometa(hdrType);
            if (hdr != null)
            {
                candidates.Add($"{hdr}.png");
            }
        }

        // Try audio only
        if (!string.IsNullOrEmpty(audioCodec))
        {
            var audio = NormalizeAudioForKometa(audioCodec);
            if (audio != null)
            {
                candidates.Add($"{audio}.png");
            }
        }

        foreach (var candidate in candidates)
        {
            if (_kometaCache.TryGetValue($"codec/{candidate}", out var badge) && badge != null)
            {
                return badge;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the rating badge based on the score.
    /// </summary>
    /// <param name="rating">The rating score (0-10).</param>
    /// <returns>The rating badge bitmap.</returns>
    public async Task<SKBitmap?> GetRatingBadgeAsync(float? rating)
    {
        await EnsureAssetsLoaded().ConfigureAwait(false);

        var fileName = rating switch
        {
            null or 0 => "audience_score_none.png",
            >= 9.0f => "audience_score_highest.png",
            >= 7.5f => "audience_score_high.png",
            >= 6.5f => "audience_score_mid.png",
            >= 5.0f => "audience_score_mid_low.png",
            _ => "audience_score_low.png"
        };

        _kometaCache.TryGetValue($"ratings/{fileName}", out var badge);
        return badge;
    }

    /// <summary>
    /// Gets the rating color based on the score (for text overlay).
    /// </summary>
    public static SKColor GetRatingColor(float? rating)
    {
        return rating switch
        {
            null or 0 => SKColor.Parse("#808080"),    // Grey
            >= 9.0f => SKColor.Parse("#1B5E20"),      // Dark green
            >= 7.5f => SKColor.Parse("#4CAF50"),      // Light green
            >= 6.5f => SKColor.Parse("#FFC107"),      // Yellow
            >= 5.0f => SKColor.Parse("#FF9800"),      // Orange
            _ => SKColor.Parse("#F44336")             // Red
        };
    }

    /// <summary>
    /// Applies a Kometa-style overlay to an image.
    /// </summary>
    public async Task<SKBitmap> ApplyKometaOverlayAsync(
        SKBitmap sourceImage,
        bool isMovie,
        string? resolution,
        string? hdrType,
        string? audioCodec,
        float? rating,
        KometaOverlayConfig config)
    {
        var result = new SKBitmap(sourceImage.Width, sourceImage.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);

        // Draw original image
        canvas.DrawBitmap(sourceImage, 0, 0);

        // Apply gradient at bottom
        if (config.EnableGradient)
        {
            var gradient = await GetGradientAsync(isMovie, isTop: false).ConfigureAwait(false);
            if (gradient != null)
            {
                // Scale gradient to match image width, position at bottom
                var gradientHeight = (int)(sourceImage.Height * config.GradientHeightPercent / 100f);
                var destRect = SKRect.Create(0, sourceImage.Height - gradientHeight, sourceImage.Width, gradientHeight);

                using var paint = new SKPaint { IsAntialias = true };
                canvas.DrawBitmap(gradient, destRect, paint);
            }
        }

        // Calculate badge positions on the gradient
        var badgeY = sourceImage.Height - (int)(sourceImage.Height * config.BadgeBottomMarginPercent / 100f);
        var badgeHeight = (int)(sourceImage.Height * config.BadgeSizePercent / 100f);
        var badgeX = (int)(sourceImage.Width * config.BadgeLeftMarginPercent / 100f);
        var badgeGap = (int)(sourceImage.Width * config.BadgeGapPercent / 100f);

        using var badgePaint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };

        // Draw resolution badge
        if (config.EnableResolutionBadge && !string.IsNullOrEmpty(resolution))
        {
            var resBadge = await GetResolutionBadgeAsync(resolution).ConfigureAwait(false);
            if (resBadge != null)
            {
                var aspectRatio = (float)resBadge.Width / resBadge.Height;
                var badgeWidth = (int)(badgeHeight * aspectRatio);
                var destRect = SKRect.Create(badgeX, badgeY - badgeHeight, badgeWidth, badgeHeight);
                canvas.DrawBitmap(resBadge, destRect, badgePaint);
                badgeX += badgeWidth + badgeGap;
            }
        }

        // Draw combined codec/HDR badge
        if (config.EnableCodecBadge)
        {
            var codecBadge = await GetCombinedCodecBadgeAsync(hdrType, audioCodec).ConfigureAwait(false);
            if (codecBadge != null)
            {
                var aspectRatio = (float)codecBadge.Width / codecBadge.Height;
                var badgeWidth = (int)(badgeHeight * aspectRatio);
                var destRect = SKRect.Create(badgeX, badgeY - badgeHeight, badgeWidth, badgeHeight);
                canvas.DrawBitmap(codecBadge, destRect, badgePaint);
                badgeX += badgeWidth + badgeGap;
            }
        }

        // Draw rating badge (positioned on the right side)
        if (config.EnableRatingBadge && rating.HasValue && rating > 0)
        {
            var ratingBadge = await GetRatingBadgeAsync(rating).ConfigureAwait(false);
            if (ratingBadge != null)
            {
                var aspectRatio = (float)ratingBadge.Width / ratingBadge.Height;
                var ratingBadgeHeight = (int)(sourceImage.Height * config.RatingBadgeSizePercent / 100f);
                var ratingBadgeWidth = (int)(ratingBadgeHeight * aspectRatio);
                var ratingX = sourceImage.Width - ratingBadgeWidth - (int)(sourceImage.Width * config.BadgeRightMarginPercent / 100f);
                var destRect = SKRect.Create(ratingX, badgeY - ratingBadgeHeight, ratingBadgeWidth, ratingBadgeHeight);
                canvas.DrawBitmap(ratingBadge, destRect, badgePaint);

                // Draw rating number on top of the badge
                if (config.ShowRatingNumber)
                {
                    DrawRatingNumber(canvas, rating.Value, ratingX, badgeY - ratingBadgeHeight, ratingBadgeWidth, ratingBadgeHeight);
                }
            }
        }

        canvas.Flush();
        return result;
    }

    private static void DrawRatingNumber(SKCanvas canvas, float rating, float x, float y, float width, float height)
    {
        var text = rating.ToString("0.0");
        var fontSize = height * 0.5f;

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), fontSize);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            Style = SKPaintStyle.Fill
        };

        var textWidth = font.MeasureText(text);
        var textX = x + (width - textWidth) / 2;
        var textY = y + height * 0.65f;

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0, 0, 0, 128),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawText(text, textX + 2, textY + 2, font, shadowPaint);

        // Draw text
        canvas.DrawText(text, textX, textY, font, paint);
    }

    private static string? NormalizeHdrForKometa(string hdrType)
    {
        return hdrType.ToUpperInvariant() switch
        {
            "DV" or "DOLBYVISION" or "DOLBY VISION" => "DV",
            "HDR10+" or "HDR10PLUS" => "DV-Plus", // HDR10+ uses Plus in Kometa naming
            "HDR10" => "HDR",
            "HDR" => "HDR",
            "HLG" => "HDR",
            _ => null
        };
    }

    private static string? NormalizeAudioForKometa(string audioCodec)
    {
        return audioCodec.ToUpperInvariant() switch
        {
            "ATMOS" or "DOLBY ATMOS" => "Atmos",
            "TRUEHD" or "DOLBY TRUEHD" => "TrueHD",
            "TRUEHD ATMOS" => "TrueHD-Atmos",
            "DTS-HD MA" or "DTSHDMA" or "DTS-HD MASTER" => "DTS-HD",
            "DTS:X" or "DTSX" => "DTS-X",
            "DTS" => "DTS",
            "EAC3" or "E-AC-3" or "DD+" => "DigitalPlus",
            "AC3" or "AC-3" or "DD" => "Digital",
            "AAC" => "AAC",
            "FLAC" => "FLAC",
            "MP3" => "MP3",
            "OPUS" => "OPUS",
            _ => null
        };
    }

    private async Task EnsureAssetsLoaded()
    {
        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_assetsLoaded)
            {
                LoadKometaAssets();
                _assetsLoaded = true;
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void LoadKometaAssets()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        _logger.LogInformation("Loading Kometa overlay assets...");

        var kometaMarker = ".Assets.kometa.";
        var loadedCount = 0;

        foreach (var resourceName in resourceNames)
        {
            var markerIdx = resourceName.IndexOf(kometaMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIdx < 0) continue;

            var relativePath = resourceName[(markerIdx + kometaMarker.Length)..];
            // Convert dots to slashes for path, but keep the last segment (filename) intact
            var parts = relativePath.Split('.');
            if (parts.Length < 2) continue;

            // Reconstruct path: folder/filename.png
            var folder = parts[0];
            var fileName = string.Join(".", parts.Skip(1));
            var cacheKey = $"{folder}/{fileName}";

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        _kometaCache[cacheKey] = bitmap;
                        loadedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Kometa asset: {Resource}", resourceName);
            }
        }

        _logger.LogInformation("Loaded {Count} Kometa overlay assets", loadedCount);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            foreach (var bitmap in _kometaCache.Values)
            {
                bitmap?.Dispose();
            }
            _kometaCache.Clear();
            _loadLock.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Configuration for Kometa-style overlays.
/// </summary>
public class KometaOverlayConfig
{
    public bool EnableGradient { get; set; } = true;
    public float GradientHeightPercent { get; set; } = 25f;
    public bool EnableResolutionBadge { get; set; } = true;
    public bool EnableCodecBadge { get; set; } = true;
    public bool EnableRatingBadge { get; set; } = true;
    public float BadgeSizePercent { get; set; } = 8f;
    public float RatingBadgeSizePercent { get; set; } = 10f;
    public float BadgeBottomMarginPercent { get; set; } = 3f;
    public float BadgeLeftMarginPercent { get; set; } = 3f;
    public float BadgeRightMarginPercent { get; set; } = 3f;
    public float BadgeGapPercent { get; set; } = 2f;
    public bool ShowRatingNumber { get; set; } = true;
}
