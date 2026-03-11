using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Service for parsing media metadata from .strm filenames.
/// Supports filename patterns like:
/// - The Amazing Spider-Man (2012) [Remux-2160p HEVC DV HDR10 10-bit TrueHD Atmos 7.1]-FraMeSToR.strm
/// - The BMF Documentary - Blowing Money Fast (2022) - S02E04 [AMZN WEBDL-1080p 8-bit h264 EAC3 5.1]-RAWR.strm
/// </summary>
public class FileNameParser
{
    // Compiled regex patterns for performance
    private static readonly Regex BracketContentRegex = new(@"\[(.*?)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ResolutionRegex = new(@"\b(2160p|1080p|720p|576p|480p|360p|4k|8k)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SourcePrefixRegex = new(@"\b(AMZN|DSNP|HMAX|ATVP|PCOK|PMTP|NF|HULU)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SourceTypeRegex = new(@"\b(Remux|REMUX|BluRay|Blu-ray|WEBDL|WEB-?DL|WEBRip|HDTV|SDTV|DVDRip|BDRip)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VideoCodecRegex = new(@"\b(HEVC|h264|h265|H\.?265|H\.?264|x265|x264|AV1|VP9|VC-?1)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HdrFormatRegex = new(@"\b(DV|Dolby\.?Vision|HDR10\+|HDR10|HDR|HLG)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BitDepthRegex = new(@"\b(10-?bit|8-?bit)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AudioCodecRegex = new(@"\b(TrueHD|Atmos|DTS-?HD\.?MA|DTS-?HD\.?HRA|DTS|AAC|AC-?3|E-?AC-?3|EAC3|FLAC|PCM|MP3)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AudioChannelsRegex = new(@"\b([0-9]\.[0-9])\b", RegexOptions.Compiled);

    /// <summary>
    /// Parses a filename to extract media metadata.
    /// </summary>
    /// <param name="filename">The filename to parse (including or excluding .strm extension).</param>
    /// <returns>A MediaMetadata object containing the parsed information.</returns>
    public MediaMetadata Parse(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return new MediaMetadata();
        }

        var metadata = new MediaMetadata();

        // Extract content from square brackets
        var bracketMatch = BracketContentRegex.Match(filename);
        var searchText = bracketMatch.Success ? bracketMatch.Groups[1].Value : filename;

        // Parse each metadata component
        metadata.Resolution = ExtractResolution(searchText);
        metadata.SourcePrefix = ExtractMatch(searchText, SourcePrefixRegex);
        metadata.SourceType = ExtractMatch(searchText, SourceTypeRegex);
        metadata.VideoCodec = ExtractMatch(searchText, VideoCodecRegex);
        metadata.HdrFormats = ExtractAllMatches(searchText, HdrFormatRegex);
        metadata.BitDepth = ExtractMatch(searchText, BitDepthRegex);
        metadata.AudioCodecs = ExtractAudioCodecs(searchText);
        metadata.AudioChannels = ExtractMatch(searchText, AudioChannelsRegex);

        return metadata;
    }

    private static string? ExtractResolution(string text)
    {
        var match = ResolutionRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var resolution = match.Value.ToUpperInvariant();

        // Normalize common resolutions
        return resolution switch
        {
            "2160P" or "4K" => "4K",
            "1080P" => "1080p",
            "720P" => "720p",
            "576P" => "576p",
            "480P" => "480p",
            "360P" => "360p",
            "8K" => "8K",
            _ => resolution
        };
    }

    private static string? ExtractMatch(string text, Regex regex)
    {
        var match = regex.Match(text);
        return match.Success ? match.Value : null;
    }

    private static List<string> ExtractAllMatches(string text, Regex regex)
    {
        var matches = regex.Matches(text);
        var results = new List<string>();

        foreach (Match match in matches)
        {
            var value = match.Value;

            // Normalize HDR formats
            if (regex == HdrFormatRegex)
            {
                value = value.ToUpperInvariant() switch
                {
                    var v when v.Contains("DOLBY") || v == "DV" => "DV",
                    var v when v.Contains("HDR10+") => "HDR10+",
                    var v when v.Contains("HDR10") => "HDR10",
                    var v when v.Contains("HDR") => "HDR",
                    "HLG" => "HLG",
                    _ => value
                };
            }

            if (!results.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(value);
            }
        }

        return results;
    }

    private static List<string> ExtractAudioCodecs(string text)
    {
        var matches = AudioCodecRegex.Matches(text);
        var codecs = new List<string>();

        foreach (Match match in matches)
        {
            var codec = match.Value;

            // Normalize audio codec names
            codec = codec.ToUpperInvariant() switch
            {
                var c when c.Contains("TRUEHD") => "TrueHD",
                "ATMOS" => "Atmos",
                var c when c.Contains("DTS") && c.Contains("MA") => "DTS-HD MA",
                var c when c.Contains("DTS") && c.Contains("HRA") => "DTS-HD HRA",
                "DTS" => "DTS",
                "AAC" => "AAC",
                var c when c.Contains("EAC3") || c.Contains("E-AC-3") => "EAC3",
                var c when c.Contains("AC3") || c.Contains("AC-3") => "AC3",
                "FLAC" => "FLAC",
                "PCM" => "PCM",
                "MP3" => "MP3",
                _ => codec
            };

            if (!codecs.Contains(codec, StringComparer.OrdinalIgnoreCase))
            {
                codecs.Add(codec);
            }
        }

        return codecs;
    }
}

/// <summary>
/// Represents parsed media metadata from a filename.
/// </summary>
public class MediaMetadata
{
    /// <summary>
    /// Gets or sets the resolution (e.g., "4K", "1080p", "720p").
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// Gets or sets the source prefix (e.g., "AMZN", "DSNP", "NF").
    /// </summary>
    public string? SourcePrefix { get; set; }

    /// <summary>
    /// Gets or sets the source type (e.g., "Remux", "WEB-DL", "BluRay").
    /// </summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// Gets or sets the video codec (e.g., "HEVC", "h264", "AV1").
    /// </summary>
    public string? VideoCodec { get; set; }

    /// <summary>
    /// Gets or sets the HDR formats (e.g., ["DV", "HDR10"]).
    /// </summary>
    public List<string> HdrFormats { get; set; } = new();

    /// <summary>
    /// Gets or sets the bit depth (e.g., "10-bit", "8-bit").
    /// </summary>
    public string? BitDepth { get; set; }

    /// <summary>
    /// Gets or sets the audio codecs (e.g., ["TrueHD", "Atmos"]).
    /// </summary>
    public List<string> AudioCodecs { get; set; } = new();

    /// <summary>
    /// Gets or sets the audio channels (e.g., "7.1", "5.1", "2.0").
    /// </summary>
    public string? AudioChannels { get; set; }

    /// <summary>
    /// Checks if any metadata was parsed.
    /// </summary>
    public bool HasMetadata()
    {
        return Resolution != null
            || SourcePrefix != null
            || SourceType != null
            || VideoCodec != null
            || HdrFormats.Count > 0
            || BitDepth != null
            || AudioCodecs.Count > 0
            || AudioChannels != null;
    }
}
