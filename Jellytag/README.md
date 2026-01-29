# JellyTag - Quality Badge Plugin for Jellyfin

JellyTag automatically adds quality resolution badges (4K, 1080p, 720p, SD) to your media posters and thumbnails in Jellyfin.

## Features

- **Universal Client Support**: Badges are applied server-side via HTTP middleware, visible on all Jellyfin clients without configuration
- **Automatic Quality Detection**: Detects video resolution from media metadata
- **Configurable**: Position, size, and which badges to display (per image type)
- **Performance Optimized**: File-based image caching to avoid re-processing
- **Easy Installation**: Standard Jellyfin plugin installation

## Screenshots

*Screenshots coming soon*

## Installation

### From Release

1. Download the latest release from the Releases page
2. Extract the contents to your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/JellyTag/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\JellyTag\`
   - Docker: `/config/plugins/JellyTag/`
3. Restart Jellyfin

### From Source

```bash
git clone https://github.com/yourusername/jellytag.git
cd jellytag/Jellyfin.Plugin.JellyTag
dotnet build -c Release

# Copy to Jellyfin plugins directory
cp bin/Release/net9.0/Jellyfin.Plugin.JellyTag.dll /path/to/jellyfin/plugins/JellyTag/
```

## Configuration

Go to **Dashboard** → **Plugins** → **JellyTag** to configure:

| Option | Description | Default |
|--------|-------------|---------|
| Enable JellyTag | Enable/disable the plugin | Enabled |
| Show 4K / 1080p / 720p / SD | Which quality badges to display | 4K & 1080p enabled |
| Cache Duration | How long to cache images (hours) | 24 |
| JPEG Quality | Output image quality (50-100) | 90 |

Each image type (poster, thumbnail, backdrop) has independent settings:

| Option | Description | Poster Default |
|--------|-------------|----------------|
| Badge Position | Corner position | Top Left |
| Badge Size (%) | Badge width as percentage of image | 15% |
| Badge Margin | Margin from edge in pixels | 10px |

## Quality Detection

Quality is determined by the **maximum dimension** (width or height) to correctly handle various aspect ratios:

| Quality | Badge | Detection Criteria |
|---------|-------|-------------------|
| 4K/UHD | Gold | Max dimension ≥ 2160 |
| 1080p | Light Gray | Max dimension ≥ 1080 |
| 720p | Gray | Max dimension ≥ 720 |
| SD | Dark Gray | Below 720 |

For collections (Series, Season), the plugin checks child items and returns the highest quality found.

## How It Works

JellyTag uses an ASP.NET Core HTTP middleware that intercepts image requests (`/Items/{id}/Images/{type}`):

1. The middleware matches the request path and determines the item type
2. Quality is detected from the video's media metadata
3. If a cached version exists, it's served directly
4. Otherwise, the original image is fetched, the badge is composited using ImageSharp, and the result is cached
5. The modified image is returned to the client

No reverse proxy configuration or client-side changes needed.

## Custom Badges

Replace the PNG files in `Jellyfin.Plugin.JellyTag/Assets/` and rebuild:

- `badge-4k.png` — 4K/UHD badge
- `badge-1080p.png` — 1080p badge
- `badge-720p.png` — 720p badge
- `badge-sd.png` — SD badge

Recommended: 80-100px width, PNG with transparency.

### Generating Badges

```bash
pip install Pillow
python scripts/create-badges.py
```

Or without Pillow (solid color placeholders):
```bash
python scripts/create-badges-simple.py
```

## Requirements

- Jellyfin 10.11.x or later
- .NET 9.0 runtime (included with Jellyfin 10.11+)

## Troubleshooting

### Badges not appearing

1. Verify the plugin is enabled in Dashboard → Plugins → JellyTag
2. Check that the quality badges you want are enabled
3. Clear the browser cache or the plugin's image cache
4. Check Jellyfin logs for errors

### Clearing the cache

- Dashboard → Plugins → JellyTag → "Clear Image Cache"
- Or delete files in the cache folder (Linux: `/var/lib/jellyfin/plugins/JellyTag/cache/`)

### Performance

- Increase cache duration to reduce re-processing
- Lower JPEG quality for smaller images
- Disable badges for qualities you don't need

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
