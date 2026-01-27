using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.WatchSync.Utils;

/// <summary>
/// Utilities for media title manipulation.
/// </summary>
public static class TitleUtils
{
    /// <summary>
    /// Normalizes a title by removing quality indicators (4K, UHD, etc.)
    /// to allow comparison between different versions of the same media.
    /// </summary>
    /// <param name="title">The title to normalize.</param>
    /// <returns>The normalized title in lowercase, without quality indicators.</returns>
    public static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        // Remove common quality indicators
        var normalized = title
            .Replace("4K", "", StringComparison.OrdinalIgnoreCase)
            .Replace("UHD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("HDR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Dolby Vision", "", StringComparison.OrdinalIgnoreCase)
            .Replace("DV", "", StringComparison.OrdinalIgnoreCase)
            .Replace("2160p", "", StringComparison.OrdinalIgnoreCase)
            .Replace("1080p", "", StringComparison.OrdinalIgnoreCase)
            .Replace("720p", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();

        // Remove empty parentheses or brackets
        normalized = Regex.Replace(normalized, @"\s*[\[\(]\s*[\]\)]\s*", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }
}
