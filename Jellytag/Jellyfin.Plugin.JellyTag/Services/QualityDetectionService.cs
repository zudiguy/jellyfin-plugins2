using System.Collections.Concurrent;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using VideoRange = Jellyfin.Data.Enums.VideoRange;
using VideoRangeType = Jellyfin.Data.Enums.VideoRangeType;

namespace Jellyfin.Plugin.JellyTag.Services;

// .strm file support: FileNameParser for extracting metadata from filenames

/// <summary>
/// Service for detecting video quality from media items.
/// </summary>
public class QualityDetectionService : IQualityDetectionService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<QualityDetectionService> _logger;
    private readonly ConcurrentDictionary<Guid, (List<BadgeInfo> Badges, DateTime CachedAt)> _badgeCache = new();
    private static readonly TimeSpan BadgeCacheTtl = TimeSpan.FromMinutes(5);
    private DateTime _lastCacheCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CacheCleanupInterval = TimeSpan.FromMinutes(10);

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

    public static VideoQuality DetermineQuality(int width, int height)
    {
        var maxDimension = Math.Max(width, height);

        if (maxDimension >= 3800) return VideoQuality.UHD4K;
        if (maxDimension >= 1900) return VideoQuality.FHD1080p;
        if (maxDimension >= 1260) return VideoQuality.HD720p;
        if (maxDimension > 0) return VideoQuality.SD;
        return VideoQuality.Unknown;
    }

    /// <inheritdoc />
    public VideoQuality GetQualityFromItem(BaseItem item)
    {
        if (item is Video video)
        {
            return GetQualityFromVideo(video);
        }

        var query = new InternalItemsQuery
        {
            ParentId = item.Id,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode],
            Limit = 10
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
                    if (bestQuality == VideoQuality.UHD4K) break;
                }
            }
        }

        if (bestQuality != VideoQuality.Unknown)
        {
            _logger.LogDebug("Resolved quality {Quality} for parent item: {ItemName}", bestQuality, item.Name);
        }

        return bestQuality;
    }

    /// <inheritdoc />
    public List<BadgeInfo> DetectAllBadges(BaseItem item)
    {
        if (_badgeCache.TryGetValue(item.Id, out var cached) && DateTime.UtcNow - cached.CachedAt < BadgeCacheTtl)
        {
            return new List<BadgeInfo>(cached.Badges);
        }

        var badges = DetectAllBadgesInternal(item);
        _badgeCache[item.Id] = (badges, DateTime.UtcNow);

        // Periodically evict expired entries to prevent unbounded memory growth
        if (DateTime.UtcNow - _lastCacheCleanup > CacheCleanupInterval)
        {
            _lastCacheCleanup = DateTime.UtcNow;
            var expiredKeys = _badgeCache
                .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt > BadgeCacheTtl)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in expiredKeys)
            {
                _badgeCache.TryRemove(key, out _);
            }
        }
        return badges;
    }

    /// <inheritdoc />
    public void ClearBadgeCache()
    {
        _badgeCache.Clear();
    }

    private List<BadgeInfo> DetectAllBadgesInternal(BaseItem item)
    {
        var badges = new List<BadgeInfo>();

        if (item is Video video)
        {
            DetectBadgesFromVideo(video, badges);
        }
        else
        {
            var query = new InternalItemsQuery
            {
                ParentId = item.Id,
                Recursive = true,
                IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode],
                Limit = 10
            };
            var children = _libraryManager.GetItemList(query);

            var bestResolution = VideoQuality.Unknown;
            Video? bestVideo = null;

            foreach (var child in children)
            {
                if (child is Video childVideo)
                {
                    var q = GetQualityFromVideo(childVideo);
                    if (q != VideoQuality.Unknown && (bestResolution == VideoQuality.Unknown || q > bestResolution))
                    {
                        bestResolution = q;
                        bestVideo = childVideo;
                    }

                    bestVideo ??= childVideo;
                }
            }

            if (bestResolution != VideoQuality.Unknown)
            {
                badges.Add(CreateResolutionBadge(bestResolution));
            }

            if (bestVideo != null)
            {
                DetectHdrAndAudioBadges(bestVideo, badges);
            }
        }

        return badges;
    }

    private void DetectBadgesFromVideo(Video video, List<BadgeInfo> badges)
    {
        DetectBadgesFromVideo(video, badges, includeResolution: true);
    }

    private void DetectHdrAndAudioBadges(Video video, List<BadgeInfo> badges)
    {
        DetectBadgesFromVideo(video, badges, includeResolution: false);
    }

    private void DetectBadgesFromVideo(Video video, List<BadgeInfo> badges, bool includeResolution)
    {
        try
        {
            // Check if this is a .strm file - if so, try filename parsing first
            var filePath = video.Path;
            if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                var fileNameBadges = DetectBadgesFromStrmFileName(filePath, includeResolution);
                if (fileNameBadges.Count > 0)
                {
                    badges.AddRange(fileNameBadges);
                    _logger.LogDebug("Detected {Count} badges from .strm filename: {Path}", fileNameBadges.Count, filePath);
                    return; // Use filename-based badges for .strm files
                }
            }

            var mediaSources = video.GetMediaSources(false);
            var mediaSource = mediaSources?.FirstOrDefault();
            var videoStream = mediaSource?.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);

            if (videoStream != null)
            {
                if (includeResolution)
                {
                    var width = videoStream.Width ?? 0;
                    var height = videoStream.Height ?? 0;
                    var quality = DetermineQuality(width, height);
                    if (quality != VideoQuality.Unknown)
                    {
                        badges.Add(CreateResolutionBadge(quality));
                    }
                }

                // HDR detection - always detect, filtering happens in ShouldShowBadge
                var hdrBadge = DetectHdr(videoStream);
                if (hdrBadge != null)
                {
                    badges.Add(hdrBadge);
                }

                // Video codec detection
                var codec = videoStream.Codec?.ToLowerInvariant() ?? string.Empty;
                if (codec is "h264" or "avc")
                {
                    badges.Add(new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "h264", ResourceFileName = "badge-h264.svg" });
                }
                else if (codec is "hevc" or "h265")
                {
                    badges.Add(new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "hevc", ResourceFileName = "badge-hevc.svg" });
                }
                else if (codec == "av1")
                {
                    badges.Add(new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "av1", ResourceFileName = "badge-av1.svg" });
                }
                else if (codec == "vp9")
                {
                    badges.Add(new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "vp9", ResourceFileName = "badge-vp9.svg" });
                }
            }

            // 3D detection
            if (video.Video3DFormat.HasValue)
            {
                badges.Add(new BadgeInfo
                {
                    Category = BadgeCategory.ThreeD,
                    BadgeKey = "3d",
                    ResourceFileName = "badge-3d.svg"
                });
            }

            // Audio detection - prefer the default audio track
            var allAudioStreams = mediaSource?.MediaStreams?.Where(s => s.Type == MediaStreamType.Audio).ToList();
            if (allAudioStreams != null && allAudioStreams.Count > 0)
            {
                var defaultStream = allAudioStreams.FirstOrDefault(s => s.IsDefault);
                var streamsToAnalyze = defaultStream != null
                    ? new List<MediaStream> { defaultStream }
                    : new List<MediaStream> { allAudioStreams[0] };
                var audioBadges = DetectAudio(streamsToAnalyze);
                badges.AddRange(audioBadges);
            }

            // Language detection - always detect all, filtering by mode happens in ShouldShowBadge
            var allStreams = mediaSource?.MediaStreams;
            if (allStreams != null)
            {
                var langBadges = DetectLanguages(allStreams.ToList());
                badges.AddRange(langBadges);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect badges for video: {ItemName}", video.Name);
        }
    }

    private static readonly Dictionary<string, string> LangCodeToFlag = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fre", "fra" }, { "ger", "deu" }, { "dut", "nld" }, { "cze", "ces" }, { "rum", "ron" }, { "chi", "zho" },
        { "gre", "ell" }, { "may", "msa" }, { "tgl", "fil" }, { "slo", "slk" }, { "baq", "eus" }, { "wel", "cym" }
    };

    // Only include language codes that have a matching flag-{code}.svg asset
    private static readonly HashSet<string> KnownFlagCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fra", "eng", "jpn", "deu", "spa", "ita", "por", "kor", "zho", "rus",
        "nld", "ara", "hin", "tha", "pol", "tur", "swe", "dan", "nor", "fin",
        "ces", "hun", "ron", "ukr", "vie", "heb"
    };

    private static string GetFlagResourceFileName(string langCode)
    {
        var normalized = LangCodeToFlag.TryGetValue(langCode, out var mapped) ? mapped : langCode;
        return KnownFlagCodes.Contains(normalized) ? $"flag-{normalized.ToLowerInvariant()}.svg" : string.Empty;
    }

    /// <summary>
    /// Detects all language and subtitle badges. Always detects all languages;
    /// filtering by mode (DefaultOnly/All) is done in ShouldShowBadge.
    /// </summary>
    private static List<BadgeInfo> DetectLanguages(List<MediaStream> allStreams)
    {
        var badges = new List<BadgeInfo>();
        var audioStreams = allStreams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        if (audioStreams.Count == 0) return badges;

        var addedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Detect all audio languages
        foreach (var stream in audioStreams)
        {
            var lang = stream.Language;
            if (!string.IsNullOrEmpty(lang) && addedLanguages.Add(lang))
            {
                var langLower = lang.ToLowerInvariant();
                badges.Add(new BadgeInfo
                {
                    Category = BadgeCategory.Language,
                    BadgeKey = langLower,
                    ResourceFileName = GetFlagResourceFileName(langLower)
                });
            }
        }

        // VOST indicators - always detect, filtering happens in ShouldShowBadge
        var audioLanguages = new HashSet<string>(
            audioStreams.Where(s => !string.IsNullOrEmpty(s.Language)).Select(s => s.Language!.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var subtitleStreams = allStreams.Where(s => s.Type == MediaStreamType.Subtitle).ToList();
        foreach (var sub in subtitleStreams)
        {
            var subLang = sub.Language?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(subLang) && !audioLanguages.Contains(subLang))
            {
                var key = "vost" + subLang;
                if (addedLanguages.Add(key))
                {
                    badges.Add(new BadgeInfo
                    {
                        Category = BadgeCategory.Subtitle,
                        BadgeKey = key,
                        ResourceFileName = string.Empty
                    });
                }
            }
        }

        return badges;
    }

    private static BadgeInfo? DetectHdr(MediaStream videoStream)
    {
        var rangeType = videoStream.VideoRangeType;

        // Dolby Vision variants (highest priority)
        if (rangeType is VideoRangeType.DOVI
            or VideoRangeType.DOVIWithHDR10
            or VideoRangeType.DOVIWithHLG
            or VideoRangeType.DOVIWithSDR
            or VideoRangeType.DOVIWithEL
            or VideoRangeType.DOVIWithHDR10Plus
            or VideoRangeType.DOVIWithELHDR10Plus)
        {
            return new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "dv", ResourceFileName = "badge-dv.svg" };
        }

        if (rangeType == VideoRangeType.HDR10Plus)
        {
            return new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr10plus", ResourceFileName = "badge-hdr10plus.svg" };
        }

        if (rangeType == VideoRangeType.HLG)
        {
            return new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hlg", ResourceFileName = "badge-hlg.svg" };
        }

        if (rangeType == VideoRangeType.HDR10)
        {
            return new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr10", ResourceFileName = "badge-hdr10.svg" };
        }

        if (videoStream.VideoRange == VideoRange.HDR)
        {
            return new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr", ResourceFileName = "badge-hdr.svg" };
        }

        return null;
    }

    private static List<BadgeInfo> DetectAudio(IEnumerable<MediaStream> audioStreams)
    {
        var badges = new List<BadgeInfo>();
        BadgeInfo? codecBadge = null;
        int codecPriority = -1;
        int bestChannels = 0;

        foreach (var stream in audioStreams)
        {
            var codec = stream.Codec?.ToUpperInvariant() ?? string.Empty;
            var profile = stream.Profile?.ToUpperInvariant() ?? string.Empty;
            var channels = stream.Channels ?? 0;

            if (channels > bestChannels) bestChannels = channels;

            int priority = -1;
            BadgeInfo? candidate = null;

            if (profile.Contains("ATMOS"))
            {
                priority = 7;
                candidate = new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "atmos", ResourceFileName = "badge-atmos.svg" };
            }
            else if (codec == "TRUEHD")
            {
                priority = 6;
                candidate = new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "truehd", ResourceFileName = "badge-truehd.svg" };
            }
            else if (profile.Contains("DTS:X") || profile.Contains("DTS-X") || profile.Contains("DTSX"))
            {
                priority = 5;
                candidate = new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "dtsx", ResourceFileName = "badge-dtsx.svg" };
            }
            else if (profile.Contains("DTS-HD MA") || profile.Contains("DTS-HD MASTER") || (codec == "DTS" && profile.Contains("MA")))
            {
                priority = 4;
                candidate = new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "dtshdma", ResourceFileName = "badge-dtshdma.svg" };
            }

            if (candidate != null && priority > codecPriority)
            {
                codecPriority = priority;
                codecBadge = candidate;
            }
        }

        if (codecBadge != null) badges.Add(codecBadge);

        if (bestChannels >= 8)
            badges.Add(new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "7.1", ResourceFileName = "badge-7_1.svg" });
        else if (bestChannels >= 6)
            badges.Add(new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "5.1", ResourceFileName = "badge-5_1.svg" });
        else if (bestChannels >= 2)
            badges.Add(new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "stereo", ResourceFileName = "badge-stereo.svg" });

        return badges;
    }

    private static BadgeInfo CreateResolutionBadge(VideoQuality quality)
    {
        return quality switch
        {
            VideoQuality.UHD4K => new BadgeInfo { Category = BadgeCategory.Resolution, BadgeKey = "4k", ResourceFileName = "badge-4k.svg" },
            VideoQuality.FHD1080p => new BadgeInfo { Category = BadgeCategory.Resolution, BadgeKey = "1080p", ResourceFileName = "badge-1080p.svg" },
            VideoQuality.HD720p => new BadgeInfo { Category = BadgeCategory.Resolution, BadgeKey = "720p", ResourceFileName = "badge-720p.svg" },
            VideoQuality.SD => new BadgeInfo { Category = BadgeCategory.Resolution, BadgeKey = "sd", ResourceFileName = "badge-sd.svg" },
            _ => new BadgeInfo { Category = BadgeCategory.Resolution, BadgeKey = "unknown", ResourceFileName = string.Empty }
        };
    }

    private VideoQuality GetQualityFromVideo(Video video)
    {
        try
        {
            // Check if this is a .strm file - if so, try filename parsing first
            var filePath = video.Path;
            if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                var quality = GetQualityFromStrmFileName(filePath);
                if (quality != VideoQuality.Unknown)
                {
                    return quality;
                }
            }

            var mediaSources = video.GetMediaSources(false);
            var mediaSource = mediaSources?.FirstOrDefault();
            var videoStream = mediaSource?.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (videoStream == null) return VideoQuality.Unknown;

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

    /// <summary>
    /// Detects badges from .strm filename using FileNameParser.
    /// Supports filenames like: "Movie (2012) [Remux-2160p HEVC DV HDR10 10-bit TrueHD Atmos 7.1]-Group.strm"
    /// </summary>
    private List<BadgeInfo> DetectBadgesFromStrmFileName(string filePath, bool includeResolution)
    {
        var badges = new List<BadgeInfo>();
        var parser = new FileNameParser();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var metadata = parser.Parse(fileName);

        if (!metadata.HasMetadata())
        {
            return badges;
        }

        // Resolution badge
        if (includeResolution && metadata.Resolution != null)
        {
            var quality = metadata.Resolution.ToUpperInvariant() switch
            {
                "4K" or "2160P" => VideoQuality.UHD4K,
                "1080P" => VideoQuality.FHD1080p,
                "720P" => VideoQuality.HD720p,
                "480P" or "576P" or "360P" => VideoQuality.SD,
                _ => VideoQuality.Unknown
            };
            if (quality != VideoQuality.Unknown)
            {
                badges.Add(CreateResolutionBadge(quality));
            }
        }

        // HDR badges
        foreach (var hdr in metadata.HdrFormats)
        {
            var hdrBadge = hdr.ToUpperInvariant() switch
            {
                "DV" or "DOLBYVISION" => new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "dv", ResourceFileName = "badge-dv.svg" },
                "HDR10+" => new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr10plus", ResourceFileName = "badge-hdr10plus.svg" },
                "HDR10" => new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr10", ResourceFileName = "badge-hdr10.svg" },
                "HDR" => new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hdr", ResourceFileName = "badge-hdr.svg" },
                "HLG" => new BadgeInfo { Category = BadgeCategory.Hdr, BadgeKey = "hlg", ResourceFileName = "badge-hlg.svg" },
                _ => null
            };
            if (hdrBadge != null)
            {
                badges.Add(hdrBadge);
                break; // Only add first/highest priority HDR badge
            }
        }

        // Video codec badge
        if (metadata.VideoCodec != null)
        {
            var codecBadge = metadata.VideoCodec.ToUpperInvariant() switch
            {
                "HEVC" or "H265" or "H.265" or "X265" => new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "hevc", ResourceFileName = "badge-hevc.svg" },
                "H264" or "H.264" or "X264" => new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "h264", ResourceFileName = "badge-h264.svg" },
                "AV1" => new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "av1", ResourceFileName = "badge-av1.svg" },
                "VP9" => new BadgeInfo { Category = BadgeCategory.VideoCodec, BadgeKey = "vp9", ResourceFileName = "badge-vp9.svg" },
                _ => null
            };
            if (codecBadge != null)
            {
                badges.Add(codecBadge);
            }
        }

        // Audio badges
        foreach (var audio in metadata.AudioCodecs)
        {
            var audioBadge = audio.ToUpperInvariant() switch
            {
                "ATMOS" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "atmos", ResourceFileName = "badge-atmos.svg" },
                "TRUEHD" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "truehd", ResourceFileName = "badge-truehd.svg" },
                "DTS-HD MA" or "DTSHDMA" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "dtshdma", ResourceFileName = "badge-dtshdma.svg" },
                "DTS:X" or "DTSX" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "dtsx", ResourceFileName = "badge-dtsx.svg" },
                _ => null
            };
            if (audioBadge != null)
            {
                badges.Add(audioBadge);
                break; // Only add first/highest priority audio codec
            }
        }

        // Audio channels
        if (metadata.AudioChannels != null)
        {
            var channelBadge = metadata.AudioChannels switch
            {
                "7.1" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "7.1", ResourceFileName = "badge-7_1.svg" },
                "5.1" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "5.1", ResourceFileName = "badge-5_1.svg" },
                "2.0" => new BadgeInfo { Category = BadgeCategory.Audio, BadgeKey = "stereo", ResourceFileName = "badge-stereo.svg" },
                _ => null
            };
            if (channelBadge != null)
            {
                badges.Add(channelBadge);
            }
        }

        return badges;
    }

    /// <summary>
    /// Gets video quality from .strm filename using FileNameParser.
    /// </summary>
    private VideoQuality GetQualityFromStrmFileName(string filePath)
    {
        var parser = new FileNameParser();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var metadata = parser.Parse(fileName);

        if (metadata.Resolution == null)
        {
            return VideoQuality.Unknown;
        }

        return metadata.Resolution.ToUpperInvariant() switch
        {
            "4K" or "2160P" => VideoQuality.UHD4K,
            "1080P" => VideoQuality.FHD1080p,
            "720P" => VideoQuality.HD720p,
            "480P" or "576P" or "360P" => VideoQuality.SD,
            _ => VideoQuality.Unknown
        };
    }
}
