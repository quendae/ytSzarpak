# YTSzarpak

A cross-platform desktop GUI wrapper around [yt-dlp](https://github.com/yt-dlp/yt-dlp), built with Avalonia and .NET.

## What It Does

YTSzarpak lets you download videos from YouTube, TikTok, and hundreds of other sites—no terminal required. Paste a URL, pick a quality your source actually offers, optionally convert to MP3 or other audio formats, and download. The app handles all the yt-dlp details behind a friendly interface.

## Zero-Install Design

YTSzarpak is distributed as **self-contained single-file executables** per OS (Windows, macOS, Linux). No need to install .NET or Python separately—just download and run.

- **yt-dlp** is fetched automatically as a standalone binary on first launch and cached for offline use.
- **Updates** to yt-dlp are checked automatically; users can apply new versions on the next restart via the Settings dialog.

## FFmpeg

[FFmpeg](https://ffmpeg.org/) is needed to:
- Merge high-quality video + audio streams.
- Convert to MP3 and other audio formats.

FFmpeg is **not bundled** in the app, but it doesn't need to be installed by hand either: if no system install is found on launch, YTSzarpak downloads a static build automatically into its own app-data folder (Windows/Linux from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), macOS from [evermeet.cx](https://evermeet.cx/ffmpeg/)) before you need it. A system install (or one set manually in Settings) is always preferred over the managed copy if present.

Settings shows a green "Detected: \<path\>" once ffmpeg is available (system, managed, or manual), or a warning if automatic acquisition failed — in which case you can install FFmpeg yourself from [ffmpeg.org/download.html](https://ffmpeg.org/download.html) and point Settings at it.

## Build & Run

### From Source

Clone the repository and build:

```bash
cd yt-dlp
dotnet build
```

Run the app directly:

```bash
dotnet run --project src/YtDlpGui.App
```

### Distributable Builds

Use the publish scripts in the `publish/` folder to create release binaries:

- **Windows:** `publish\publish-windows.ps1` → `publish\output\win-x64\YTSzarpak.exe`
- **macOS:** `publish\publish-macos.sh` → `publish\output\osx-{x64,arm64}\YTSzarpak.app`
- **Linux:** `publish\publish-linux.sh` → `publish\output\linux-x64\YTSzarpak`

All scripts run from within the `publish/` folder (or anywhere, as they use location-independent paths).

## License & Attribution

- **YTSzarpak** source code: MIT (via Avalonia dependency)
- **yt-dlp**: [Unlicense](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE)
- **FFmpeg**: Not bundled; downloaded on demand from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) (Windows/Linux, GPL build) or [evermeet.cx](https://evermeet.cx/ffmpeg/) (macOS) — see [ffmpeg.org](https://ffmpeg.org/) for its license terms
- **Avalonia UI**: MIT
