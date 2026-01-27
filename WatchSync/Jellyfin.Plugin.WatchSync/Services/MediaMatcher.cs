using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.WatchSync.Utils;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WatchSync.Services;

/// <summary>
/// Media matching service between libraries using Provider IDs.
/// </summary>
public class MediaMatcher : IMediaMatcher
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MediaMatcher> _logger;

    /// <summary>
    /// Initializes the matching service.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="logger">Logger.</param>
    public MediaMatcher(ILibraryManager libraryManager, ILogger<MediaMatcher> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<BaseItem> FindMatchingItems(BaseItem sourceItem)
    {
        if (sourceItem == null)
        {
            return Array.Empty<BaseItem>();
        }

        var config = Plugin.Instance?.Configuration;
        var excludedLibraries = config?.ExcludedLibraryIds ?? new List<string>();

        var matchingItems = new List<BaseItem>();

        // Get all items of the same type
        var allItems = GetAllItemsOfSameType(sourceItem);

        foreach (var item in allItems)
        {
            // Skip the source item itself
            if (item.Id == sourceItem.Id)
            {
                continue;
            }

            // Skip items in excluded libraries
            var libraryId = GetLibraryId(item);
            if (libraryId != null && excludedLibraries.Contains(libraryId.Value.ToString()))
            {
                continue;
            }

            // Check if items match
            if (AreItemsMatching(sourceItem, item))
            {
                matchingItems.Add(item);
                _logger.LogDebug(
                    "Found match for {SourceName}: {MatchName} (Library: {LibraryId})",
                    sourceItem.Name,
                    item.Name,
                    libraryId);
            }
        }

        _logger.LogInformation(
            "Found {Count} match(es) for {ItemName}",
            matchingItems.Count,
            sourceItem.Name);

        return matchingItems;
    }

    /// <inheritdoc />
    public bool AreItemsMatching(BaseItem item1, BaseItem item2)
    {
        if (item1 == null || item2 == null)
        {
            return false;
        }

        // Items must be of the same type
        if (item1.GetType() != item2.GetType())
        {
            return false;
        }

        // Match by providers according to configuration
        var config = Plugin.Instance?.Configuration;
        var providerOrder = GetProviderOrder(config?.ProviderPriority ?? "IMDB,TMDB,TVDB");

        foreach (var provider in providerOrder)
        {
            switch (provider.ToUpperInvariant())
            {
                case "IMDB":
                    if (config?.UseImdbId != false && TryMatchByProviderId(item1, item2, MetadataProvider.Imdb))
                    {
                        return true;
                    }

                    break;
                case "TMDB":
                    if (config?.UseTmdbId != false && TryMatchByProviderId(item1, item2, MetadataProvider.Tmdb))
                    {
                        return true;
                    }

                    break;
                case "TVDB":
                    if (config?.UseTvdbId != false && TryMatchByProviderId(item1, item2, MetadataProvider.Tvdb))
                    {
                        return true;
                    }

                    break;
            }
        }

        // For episodes, also check parent series + episode number
        if (item1 is Episode episode1 && item2 is Episode episode2)
        {
            if (AreEpisodesMatching(episode1, episode2))
            {
                return true;
            }
        }

        // Optional fallback: Title + Year
        if (config?.UseTitleYearFallback == true)
        {
            if (TryMatchByTitleAndYear(item1, item2))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetProviderOrder(string priorityString)
    {
        if (string.IsNullOrWhiteSpace(priorityString))
        {
            return new[] { "IMDB", "TMDB", "TVDB" };
        }

        return priorityString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryMatchByProviderId(BaseItem item1, BaseItem item2, MetadataProvider provider)
    {
        var id1 = item1.GetProviderId(provider);
        var id2 = item2.GetProviderId(provider);

        if (string.IsNullOrEmpty(id1) || string.IsNullOrEmpty(id2))
        {
            return false;
        }

        return string.Equals(id1, id2, StringComparison.OrdinalIgnoreCase);
    }

    private bool AreEpisodesMatching(Episode episode1, Episode episode2)
    {
        // Check that parent series match
        var series1 = episode1.Series;
        var series2 = episode2.Series;

        if (series1 == null || series2 == null)
        {
            return false;
        }

        // Series must match
        if (!AreItemsMatching(series1, series2))
        {
            return false;
        }

        // Check season and episode numbers
        if (episode1.ParentIndexNumber != episode2.ParentIndexNumber)
        {
            return false;
        }

        if (episode1.IndexNumber != episode2.IndexNumber)
        {
            return false;
        }

        _logger.LogDebug(
            "Matching episodes: {Series1} S{Season}E{Episode}",
            series1.Name,
            episode1.ParentIndexNumber,
            episode1.IndexNumber);

        return true;
    }

    private static bool TryMatchByTitleAndYear(BaseItem item1, BaseItem item2)
    {
        // Normalize titles for comparison
        var title1 = TitleUtils.NormalizeTitle(item1.Name);
        var title2 = TitleUtils.NormalizeTitle(item2.Name);

        if (!string.Equals(title1, title2, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Compare years
        var year1 = item1.ProductionYear;
        var year2 = item2.ProductionYear;

        if (!year1.HasValue || !year2.HasValue)
        {
            return false;
        }

        return year1.Value == year2.Value;
    }

    private IEnumerable<BaseItem> GetAllItemsOfSameType(BaseItem sourceItem)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { sourceItem.GetBaseItemKind() },
            IsVirtualItem = false,
            Recursive = true
        };

        return _libraryManager.GetItemList(query);
    }

    private static Guid? GetLibraryId(BaseItem item)
    {
        var parent = item;
        while (parent != null)
        {
            if (parent is Folder folder && folder.IsTopParent)
            {
                return folder.Id;
            }

            parent = parent.GetParent();
        }

        return item.ParentId;
    }
}
