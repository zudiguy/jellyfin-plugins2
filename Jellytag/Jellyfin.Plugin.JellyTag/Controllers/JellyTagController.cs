using Jellyfin.Plugin.JellyTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyTag.Controllers;

/// <summary>
/// Controller for JellyTag plugin admin and debug endpoints.
/// Image badge overlays are handled by the ImageOverlayMiddleware.
/// </summary>
[ApiController]
[Route("JellyTag")]
public class JellyTagController : ControllerBase
{
    private readonly IImageCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyTagController"/> class.
    /// </summary>
    public JellyTagController(IImageCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// Clears the image cache.
    /// </summary>
    /// <returns>A status indicating the result.</returns>
    [HttpPost("ClearCache")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearCache()
    {
        _cacheService.ClearCache();
        return NoContent();
    }

    /// <summary>
    /// Gets the plugin status.
    /// </summary>
    /// <returns>Plugin status information.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        return Ok(new
        {
            Enabled = config?.Enabled ?? false,
            Show4K = config?.Show4K ?? false,
            Show1080p = config?.Show1080p ?? false,
            Show720p = config?.Show720p ?? false,
            ShowSD = config?.ShowSD ?? false
        });
    }

    /// <summary>
    /// Debug endpoint to list embedded resources.
    /// </summary>
    [HttpGet("Debug/Resources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetResources()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames();
        return Ok(new
        {
            AssemblyName = assembly.FullName,
            Resources = resources
        });
    }

    /// <summary>
    /// Debug endpoint to get a raw badge image.
    /// </summary>
    [HttpGet("Debug/Badge/{quality}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetBadge(string quality)
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        var fileName = quality.ToLower() switch
        {
            "4k" => "badge-4k.png",
            "1080p" => "badge-1080p.png",
            "720p" => "badge-720p.png",
            "sd" => "badge-sd.png",
            _ => null
        };

        if (fileName == null)
            return NotFound("Invalid quality");

        var resourceName = resourceNames.FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
            return NotFound($"Resource not found: {fileName}");

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return NotFound("Stream is null");

        return File(stream, "image/png");
    }
}
