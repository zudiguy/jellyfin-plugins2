# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

From `Jellyfin.Plugin.JellyTag/`:
```bash
dotnet restore              # Restore NuGet dependencies
dotnet build -c Release     # Build plugin DLL (output: bin/Release/net9.0/)
```

Badge generation scripts:
```bash
python scripts/create-badges.py          # Requires Pillow
python scripts/create-badges-simple.py   # No Pillow dependency
```

No automated test suite exists. Target framework is .NET 9.0, targeting Jellyfin 10.11.

## Architecture

JellyTag is a Jellyfin plugin that overlays quality badges (4K, 1080p, 720p, SD) onto media images by intercepting HTTP requests at the middleware level.

**Request flow:**
1. **ImageOverlayMiddleware** intercepts GET requests matching `/Items/{id}/Images/{Primary|Thumb|Backdrop}` via regex
2. **QualityDetectionService** determines video quality from media metadata (uses max dimension: ≥2160→4K, ≥1080→FHD, ≥720→HD, else SD). For collections (Series/Season), scans first 10 children and returns highest quality
3. **ImageCacheService** checks file-based cache (SHA256 key includes itemId, quality, imageTag, and full config hash for automatic invalidation on settings change)
4. **ImageOverlayService** composites the badge using SixLabors.ImageSharp with manual pixel-level alpha blending (not DrawImage), then encodes as JPEG

**Key design decisions:**
- All services are singletons registered via `PluginServiceRegistrator`
- Badge images are embedded resources loaded lazily with SemaphoreSlim for thread safety
- Cache writes use atomic temp-file-then-rename pattern
- Middleware falls back to original image on any overlay failure
- Configuration supports independent settings (position, size, margin) per image type (poster/thumbnail/backdrop)

**Admin API** (`/JellyTag/`): ClearCache (POST, elevated), Status (GET), Debug/Resources (GET), Debug/Badge/{quality} (GET).

## Coding Conventions

- Standard .NET: 4-space indentation, PascalCase for public types/methods, camelCase for locals/parameters
- Asset filenames: `badge-<quality>.png` (e.g., `badge-1080p.png`)
- Services should be focused and stateless where possible
- Commit messages: short, imperative (e.g., "Add cache invalidation button")
- PRs should describe user impact and include screenshots for UI changes
