#!/bin/bash

# Publishes YTSzarpak as self-contained single-file executables for macOS (x64 and ARM64),
# each wrapped in a minimal .app bundle for double-click launching.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT_PATH="$REPO_ROOT/src/YtDlpGui.App/YtDlpGui.App.csproj"

# Architectures to build
ARCHITECTURES=("osx-x64" "osx-arm64")

for ARCH in "${ARCHITECTURES[@]}"; do
    OUTPUT_DIR="$SCRIPT_DIR/output/$ARCH"

    echo "Publishing YTSzarpak for macOS $ARCH..."

    # Create output directory
    mkdir -p "$OUTPUT_DIR"

    # Run dotnet publish
    dotnet publish "$PROJECT_PATH" \
        -c Release \
        -r "$ARCH" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$OUTPUT_DIR"

    if [ $? -ne 0 ]; then
        echo "Publish failed for $ARCH" >&2
        exit 1
    fi

    # Find the published binary (should be YTSzarpak or YTSzarpak.exe if somehow cross-compiled)
    BINARY=$(find "$OUTPUT_DIR" -maxdepth 1 -type f -executable ! -name "*.pdb" ! -name "*.dbg" | head -1)

    if [ -z "$BINARY" ]; then
        echo "No executable found in $OUTPUT_DIR" >&2
        exit 1
    fi

    BINARY_NAME=$(basename "$BINARY")

    # Create .app bundle structure
    APP_BUNDLE="$OUTPUT_DIR/YTSzarpak.app"
    MACOS_DIR="$APP_BUNDLE/Contents/MacOS"

    mkdir -p "$MACOS_DIR"

    # Copy binary into the bundle
    cp "$BINARY" "$MACOS_DIR/YTSzarpak"
    chmod +x "$MACOS_DIR/YTSzarpak"

    # Create Info.plist
    cat > "$APP_BUNDLE/Contents/Info.plist" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>YTSzarpak</string>
    <key>CFBundleIdentifier</key>
    <string>com.ytszarpak.app</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>YTSzarpak</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.13</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

    echo "Successfully published $ARCH to: $APP_BUNDLE"
done

echo "All macOS builds completed successfully."
