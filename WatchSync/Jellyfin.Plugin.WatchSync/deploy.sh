#!/bin/bash
# Deployment script to dev Jellyfin

PLUGIN_DIR="$HOME/docker/jellyfin-dev/config/plugins/WatchSync"
BUILD_DIR="$(dirname "$0")/bin/Release/net9.0"

# Build
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ~/.dotnet/dotnet build -c Release "$(dirname "$0")"

# Copy
mkdir -p "$PLUGIN_DIR"
cp "$BUILD_DIR/Jellyfin.Plugin.WatchSync.dll" "$PLUGIN_DIR/"

# Restart Jellyfin
sudo docker restart jellyfin-dev

echo "Plugin deployed and Jellyfin restarted"
