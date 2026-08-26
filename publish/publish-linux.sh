#!/bin/bash

# Publishes YTSzarpak as a self-contained single-file executable for Linux (x64).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT_PATH="$REPO_ROOT/src/YtDlpGui.App/YtDlpGui.App.csproj"
OUTPUT_DIR="$SCRIPT_DIR/output/linux-x64"

echo "Publishing YTSzarpak for Linux x64..."

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Run dotnet publish
dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUTPUT_DIR"

if [ $? -ne 0 ]; then
    echo "Publish failed" >&2
    exit 1
fi

# Find and make the binary executable
BINARY=$(find "$OUTPUT_DIR" -maxdepth 1 -type f ! -name "*.pdb" ! -name "*.dbg" | head -1)

if [ -z "$BINARY" ]; then
    echo "No executable found in $OUTPUT_DIR" >&2
    exit 1
fi

chmod +x "$BINARY"

echo "Successfully published to: $BINARY"
