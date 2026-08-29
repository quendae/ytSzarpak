<p align="center">
  <img src="branding/icon-source.png" width="96" alt="YTSzarpak icon">
</p>

<h1 align="center">YTSzarpak</h1>

<p align="center">
  A small cross-platform desktop app for downloading media with <a href="https://github.com/yt-dlp/yt-dlp">yt-dlp</a> — without living in a terminal.
</p>

<p align="center">
  <strong>English</strong> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a>
</p>

---

YTSzarpak exists for the times when yt-dlp is exactly the tool you want, but typing the same commands again is not.

Paste a link, choose the format, add it to the queue and let the app take care of the command-line details. It works with YouTube as well as the many other sites supported by yt-dlp.

## What you get

- **Video and audio downloads** through yt-dlp.
- **Quality selection** based on formats actually available for the pasted link.
- **Audio-only mode** with MP3 and other common output formats.
- **Download queue** with progress, speed, ETA and per-item actions.
- **Playlist support** when a playlist URL is supplied.
- **YouTube sign-in support** using cookies from a browser or an exported `cookies.txt` file.
- **Automatic yt-dlp setup and update checks.**
- **Automatic FFmpeg setup** when a usable system installation is not found.
- **Windows, macOS and Linux** builds from one Avalonia/.NET codebase.

## How it works

1. Paste a supported media URL.
2. Click **Grab** so YTSzarpak can inspect the available formats.
3. Pick video quality or switch to audio-only mode.
4. Add the item to the queue.
5. Keep adding links while downloads run in the background.

The app does not reimplement a downloader. It provides a desktop interface around yt-dlp and FFmpeg, while keeping both tools separate and replaceable.

## YouTube sign-in

Some YouTube videos are only available to signed-in users.

YTSzarpak never asks for your Google password. Instead, it can reuse cookies from a browser profile that is already signed in. If browser extraction fails — Chrome on Windows can be especially awkward because its cookie database may be locked — export a Netscape-format `cookies.txt` file and select it in **Settings**. A cookies file takes priority over browser extraction when both are configured.

## FFmpeg

FFmpeg is used for jobs such as merging separate video and audio streams or converting audio.

If YTSzarpak cannot find FFmpeg on your system, it downloads a compatible build into its own application-data directory. You can still point the app at a specific FFmpeg installation in **Settings** if you prefer.

## Build from source

You need the **.NET 10 SDK**.

```bash
git clone https://github.com/quendae/ytSzarpak.git
cd ytSzarpak
dotnet build
```

Run the desktop app directly:

```bash
dotnet run --project src/YtDlpGui.App
```

## Create distributable builds

The repository includes platform-specific publish scripts. They produce self-contained builds, so end users do not need to install .NET or Python separately.

| Platform | Command | Output |
| --- | --- | --- |
| Windows | `publish\publish-windows.ps1` | `publish\output\win-x64\YTSzarpak.exe` |
| macOS | `./publish/publish-macos.sh` | `publish/output/osx-{x64,arm64}/YTSzarpak.app` |
| Linux | `./publish/publish-linux.sh` | `publish/output/linux-x64/YTSzarpak` |

## A few implementation details

YTSzarpak is built with **Avalonia 12** and **.NET 10**. Application state is handled with `CommunityToolkit.Mvvm`, while downloading and binary management live in the separate `YtDlpGui.Core` project.

On first use, yt-dlp is downloaded as a standalone binary and kept in the app-data directory. YTSzarpak can check for newer yt-dlp versions and update the managed copy without requiring Python or pip.

## Third-party projects

YTSzarpak depends on some excellent open-source work:

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — media extraction and downloading.
- [FFmpeg](https://ffmpeg.org/) — media processing, merging and conversion.
- [Avalonia UI](https://avaloniaui.net/) — cross-platform desktop UI.

Each third-party project keeps its own license and distribution terms. This repository currently does not include a separate license file for YTSzarpak itself.

---

<p align="center">
  Built as a simple desktop front end for a very capable command-line tool.
</p>
