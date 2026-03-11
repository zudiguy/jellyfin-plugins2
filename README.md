# Jellyfin Plugins (STRM Support Fork)

This is a fork of [Atilil's jellyfin-plugins](https://github.com/Atilil/jellyfin-plugins) with added support for .strm files.

## JellyTag (STRM Support)

<p align="center">
    <img src="Jellytag/Jellyfin.Plugin.JellyTag/JellyTag.png" />
</p>

Automatically adds quality badges (4K, 1080p, HDR, Atmos, etc.) to your media posters and thumbnails.

### What's Different in This Fork?

The original JellyTag plugin reads metadata from Jellyfin's database. However, **.strm files** (streaming files) don't have embedded metadata in Jellyfin.

**This fork parses .strm filenames to extract quality metadata!**

### Supported Filename Patterns

**Movies:**
```
The Amazing Spider-Man (2012) [Remux-2160p HEVC DV HDR10 10-bit TrueHD Atmos 7.1]-FraMeSToR.strm
```

**TV Shows:**
```
The BMF Documentary (2022) - S02E04 [AMZN WEBDL-1080p 8-bit h264 EAC3 5.1]-RAWR.strm
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

---

## Installation

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Add a new repository:
   - **Name**: `JellyTag STRM`
   - **URL**: `https://raw.githubusercontent.com/zudiguy/jellyfin-plugins2/main/manifest.json`
3. Click **Save**
4. Go to **Catalog** tab and install **JellyTag (STRM Support)**
5. Restart Jellyfin
6. Configure at **Dashboard → Plugins → JellyTag**

---

## How It Works

For .strm files, the plugin:

1. **Checks if file is .strm** - Looks at the file path
2. **Parses the filename** - Extracts content from `[brackets]`
3. **Matches patterns** - Uses regex to find resolution, HDR, codec, audio
4. **Creates badges** - Same badges as standard metadata detection

For regular video files, the plugin uses Jellyfin's standard metadata (unchanged from original).

---

## Requirements

- Jellyfin 10.11.0 or higher
- .NET 9 SDK (for building)

## Credits

- **Original JellyTag** by [Atilil](https://github.com/Atilil/jellyfin-plugins)
- **.strm filename parsing** by [zudiguy](https://github.com/zudiguy)

## License

MIT License - see [LICENSE](LICENSE) file.

---

## Disclaimer

This project was developed with the assistance of AI (Claude by Anthropic). The code has been reviewed and tested before publication.
