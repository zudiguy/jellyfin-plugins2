# JellyTag (STRM Support) - Project Documentation

## Project Overview
JellyTag is a Jellyfin plugin that automatically overlays quality badges (resolution, HDR, codec, audio, language) on media posters and thumbnails. This fork adds support for .strm files by parsing filenames for metadata.

**Repository**: https://github.com/zudiguy/jellyfin-plugins2
**Based on**: [Atilil's JellyTag v2.0.2.0](https://github.com/Atilil/jellyfin-plugins)
**Current Version**: 2.1.0.0

## Key Modification: .strm File Support

The original JellyTag reads metadata from Jellyfin's database. However, **.strm files** (streaming files) don't have embedded metadata in Jellyfin.

**This fork parses .strm filenames to extract quality metadata!**

### Example Filenames Supported

**Movies:**
```
The Amazing Spider-Man (2012) [Remux-2160p HEVC DV HDR10 10-bit TrueHD Atmos 7.1]-FraMeSToR.strm
```

**TV Shows:**
```
The BMF Documentary - Blowing Money Fast (2022) - S02E04 [AMZN WEBDL-1080p 8-bit h264 EAC3 5.1]-RAWR.strm
```

### Detected Metadata

| Category | Detected Values |
|----------|-----------------|
| **Resolution** | 4K, 2160p, 1080p, 720p, 480p |
| **HDR Formats** | DV (Dolby Vision), HDR10+, HDR10, HDR, HLG |
| **Video Codecs** | HEVC, H.264, AV1, VP9 |
| **Audio Codecs** | Atmos, TrueHD, DTS-HD MA, DTS:X |
| **Audio Channels** | 7.1, 5.1, Stereo |
| **Streaming Services** | AMZN, DSNP, HMAX, NF, HULU |

## Files Modified/Added

### New File: `Jellytag/Jellyfin.Plugin.JellyTag/Services/FileNameParser.cs`

Custom service for parsing .strm filenames using regex patterns:
- Extracts content from square brackets `[...]`
- Detects resolution, HDR formats, video/audio codecs, channels
- Returns `MediaMetadata` object with parsed values

### Modified: `Jellytag/Jellyfin.Plugin.JellyTag/Services/QualityDetectionService.cs`

Added .strm file detection in `DetectBadgesFromVideo()`:
```csharp
// Check if this is a .strm file - if so, try filename parsing first
var filePath = video.Path;
if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
{
    var fileNameBadges = DetectBadgesFromStrmFileName(filePath, includeResolution);
    if (fileNameBadges.Count > 0)
    {
        badges.AddRange(fileNameBadges);
        return; // Use filename-based badges for .strm files
    }
}
// Falls back to standard metadata detection for non-.strm files
```

## Technical Details

### Regex Patterns Used

```csharp
BracketContentRegex = @"\[(.*?)\]"
ResolutionRegex = @"\b(2160p|1080p|720p|576p|480p|360p|4k|8k)\b"
SourcePrefixRegex = @"\b(AMZN|DSNP|HMAX|ATVP|PCOK|PMTP|NF|HULU)\b"
SourceTypeRegex = @"\b(Remux|REMUX|BluRay|Blu-ray|WEBDL|WEB-?DL|WEBRip|HDTV|SDTV|DVDRip|BDRip)\b"
VideoCodecRegex = @"\b(HEVC|h264|h265|H\.?265|H\.?264|x265|x264|AV1|VP9|VC-?1)\b"
HdrFormatRegex = @"\b(DV|Dolby\.?Vision|HDR10\+|HDR10|HDR|HLG)\b"
AudioCodecRegex = @"\b(TrueHD|Atmos|DTS-?HD\.?MA|DTS-?HD\.?HRA|DTS|AAC|AC-?3|E-?AC-?3|EAC3|FLAC|PCM|MP3)\b"
AudioChannelsRegex = @"\b([0-9]\.[0-9])\b"
```

### How It Works

1. Plugin intercepts image requests via HTTP middleware
2. For each media item, checks if file path ends with `.strm`
3. If .strm: `FileNameParser` extracts metadata from bracketed filename content
4. If not .strm: Uses Jellyfin's standard metadata (unchanged from original)
5. Creates quality badges based on detected metadata
6. Composites badges onto poster image using SkiaSharp

## Installation

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Add a new repository:
   - **Name**: `JellyTag STRM`
   - **URL**: `https://raw.githubusercontent.com/zudiguy/jellyfin-plugins2/main/manifest.json`
3. Click **Save**
4. Go to **Catalog** tab and install **JellyTag (STRM Support)**
5. Restart Jellyfin
6. Configure at **Dashboard → Plugins → JellyTag**

## Building

```bash
cd Jellytag/Jellyfin.Plugin.JellyTag
dotnet publish -c Release -o output
```

## Creating a Release

1. Update version in:
   - `Jellyfin.Plugin.JellyTag.csproj`
   - `.github/workflows/build-jellytag.yml`
   - `manifest.json`

2. Create and push tag:
   ```bash
   git tag jellytag-X.Y.Z.W
   git push origin jellytag-X.Y.Z.W
   ```

3. GitHub Actions automatically builds and creates release

4. Get checksum from release zip:
   ```bash
   md5 jellytag-X.Y.Z.W.zip
   ```

5. Update `manifest.json` with new checksum and push

## Requirements

- Jellyfin 10.11.0 or higher
- .NET 9 SDK (for building)

## Release History

### v2.1.0.0 (2026-03-11)
- Fork with .strm file support
- Added `FileNameParser.cs` for filename metadata extraction
- Modified `QualityDetectionService.cs` to use filename parsing for .strm files
- Extracts resolution, HDR, codec, audio from filenames like `[Remux-2160p HEVC DV HDR10 TrueHD Atmos 7.1]`
- Supports streaming service tags (AMZN, DSNP, HMAX, NF)
- Falls back to standard metadata if filename parsing fails
- Checksum: `d97f6909e432396ee0bdf7140b9328db`

### v2.0.2.0 (Original by Atilil)
- Base version this fork is built on
- Fix: exclude SkiaSharp native assets that caused plugin loading crash
- Pin Jellyfin packages to 10.11.0 for full 10.11.x compatibility

## Project History

This project went through several iterations:

1. **Initial attempt**: Built plugin from scratch - incomplete (~30% of original features)
2. **Final approach**: Forked original JellyTag and added only the .strm filename parsing

The fork approach was chosen because the original plugin has:
- 64 badge asset files
- Full configuration UI with ~40 settings
- Controllers, scheduled tasks, caching
- All edge cases handled

Adding .strm support required only 2 file changes while keeping full feature parity.

## Credits

- **Original JellyTag** by [Atilil](https://github.com/Atilil/jellyfin-plugins)
- **.strm filename parsing** by [zudiguy](https://github.com/zudiguy)
- **AI assistance** by Claude (Anthropic)

## License

MIT License
