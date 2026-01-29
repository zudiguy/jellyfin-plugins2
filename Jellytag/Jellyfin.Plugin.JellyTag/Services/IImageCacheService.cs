namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Interface for image cache service.
/// </summary>
public interface IImageCacheService
{
    /// <summary>
    /// Gets a cached image if available and not expired.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <param name="quality">The video quality.</param>
    /// <param name="imageTag">The image tag/etag for cache invalidation.</param>
    /// <returns>The cached image stream, or null if not cached.</returns>
    Task<Stream?> GetCachedImageAsync(Guid itemId, VideoQuality quality, string imageTag);

    /// <summary>
    /// Caches an image.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <param name="quality">The video quality.</param>
    /// <param name="imageTag">The image tag/etag.</param>
    /// <param name="imageStream">The image stream to cache.</param>
    /// <returns>A task representing the async operation.</returns>
    Task CacheImageAsync(Guid itemId, VideoQuality quality, string imageTag, Stream imageStream);

    /// <summary>
    /// Clears all cached images.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Invalidates cache for a specific item.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    void InvalidateCache(Guid itemId);
}
