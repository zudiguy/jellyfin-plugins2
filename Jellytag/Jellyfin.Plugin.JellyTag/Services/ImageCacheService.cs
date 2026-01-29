using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Service for caching modified images.
/// </summary>
public class ImageCacheService : IImageCacheService
{
    private readonly ILogger<ImageCacheService> _logger;
    private readonly string _cachePath;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCacheService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ImageCacheService(ILogger<ImageCacheService> logger)
    {
        _logger = logger;
        _cachePath = Plugin.Instance?.CacheFolderPath ?? Path.Combine(Path.GetTempPath(), "JellyTag", "cache");
        EnsureCacheDirectoryExists();
    }

    /// <inheritdoc />
    public Task<Stream?> GetCachedImageAsync(Guid itemId, VideoQuality quality, string imageTag)
    {
        var cacheKey = GenerateCacheKey(itemId, quality, imageTag);
        var cacheFilePath = GetCachePath(cacheKey);

        if (!File.Exists(cacheFilePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        // Check if cache is expired
        var config = Plugin.Instance?.Configuration;
        var cacheHours = config?.CacheDurationHours ?? 24;
        var fileInfo = new FileInfo(cacheFilePath);

        if (fileInfo.LastWriteTimeUtc.AddHours(cacheHours) < DateTime.UtcNow)
        {
            _logger.LogDebug("Cache expired for item {ItemId}", itemId);
            try
            {
                File.Delete(cacheFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete expired cache file: {Path}", cacheFilePath);
            }

            return Task.FromResult<Stream?>(null);
        }

        _logger.LogDebug("Cache hit for item {ItemId}", itemId);
        Stream stream = new FileStream(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public async Task CacheImageAsync(Guid itemId, VideoQuality quality, string imageTag, Stream imageStream)
    {
        var cacheKey = GenerateCacheKey(itemId, quality, imageTag);
        var cachePath = GetCachePath(cacheKey);
        var tempPath = cachePath + ".tmp";

        try
        {
            EnsureCacheDirectoryExists();

            // Write to temporary file first to avoid partial reads
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await imageStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            // Atomic rename to final path
            File.Move(tempPath, cachePath, overwrite: true);

            _logger.LogDebug("Cached image for item {ItemId} at {Path}", itemId, cachePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache image for item {ItemId}", itemId);

            // Clean up temp file if it exists
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        lock (_lock)
        {
            try
            {
                if (Directory.Exists(_cachePath))
                {
                    var files = Directory.GetFiles(_cachePath, "*.jpg");
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete cache file: {Path}", file);
                        }
                    }

                    _logger.LogInformation("Cleared {Count} cached images", files.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache");
            }
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(Guid itemId)
    {
        try
        {
            var pattern = $"{itemId}_*.jpg";
            var files = Directory.GetFiles(_cachePath, pattern);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogDebug("Invalidated cache for item {ItemId}: {File}", itemId, file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cache file: {Path}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for item {ItemId}", itemId);
        }
    }

    private string GenerateCacheKey(Guid itemId, VideoQuality quality, string imageTag)
    {
        var config = Plugin.Instance?.Configuration;
        // Include all config values that affect the output image
        var configHash = $"{config?.PosterSettings?.BadgePosition}_{config?.PosterSettings?.BadgeSizePercent}_{config?.PosterSettings?.BadgeMargin}_{config?.ThumbnailSettings?.BadgePosition}_{config?.ThumbnailSettings?.BadgeSizePercent}_{config?.ThumbnailSettings?.BadgeMargin}_{config?.BackdropSettings?.BadgePosition}_{config?.BackdropSettings?.BadgeSizePercent}_{config?.BackdropSettings?.BadgeMargin}_{config?.JpegQuality}";
        var input = $"{itemId}_{quality}_{imageTag}_{configHash}";

        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hash = Convert.ToHexString(hashBytes)[..16];

        return $"{itemId}_{quality}_{hash}";
    }

    private string GetCachePath(string cacheKey)
    {
        return Path.Combine(_cachePath, $"{cacheKey}.jpg");
    }

    private void EnsureCacheDirectoryExists()
    {
        if (!Directory.Exists(_cachePath))
        {
            Directory.CreateDirectory(_cachePath);
        }
    }
}
