using System.Diagnostics;
using System.Text.Json;
using YtDlpGui.Core.Models;

namespace YtDlpGui.Core;

public sealed class VideoMetadataService
{
    private readonly YtDlpBinaryManager _binaryManager;

    public VideoMetadataService(YtDlpBinaryManager binaryManager)
    {
        _binaryManager = binaryManager;
    }

    public async Task<VideoInfo> FetchInfoAsync(
        string url, string? cookiesFromBrowser, string? cookiesFilePath, CancellationToken ct)
    {
        // --flat-playlist only changes behavior when the URL actually resolves to a playlist;
        // for a single video it has no effect and the full `formats` array is still returned.
        var args = new List<string> { "-J", "--no-warnings", "--flat-playlist" };
        args.AddRange(CookieArgs.Build(cookiesFromBrowser, cookiesFilePath));
        args.Add(url);

        var (exitCode, stdout, stderr) = await RunAsync(args.ToArray(), ct);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException($"yt-dlp failed to fetch video info: {stderr}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        var isPlaylist = root.TryGetProperty("entries", out var entriesEl);
        var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? url : url;
        var thumbnail = root.TryGetProperty("thumbnail", out var thumbEl) ? thumbEl.GetString() : null;

        if (isPlaylist)
        {
            var entryCount = entriesEl.ValueKind == JsonValueKind.Array ? entriesEl.GetArrayLength() : 0;
            return new VideoInfo(title, thumbnail, true, entryCount, GenericPlaylistFormats());
        }

        var formats = root.TryGetProperty("formats", out var formatsEl) && formatsEl.ValueKind == JsonValueKind.Array
            ? BuildVideoFormats(formatsEl)
            : GenericPlaylistFormats();

        return new VideoInfo(title, thumbnail, false, 1, formats);
    }

    /// <summary>
    /// Builds the quality list from the distinct heights actually present in this video's formats.
    /// Every selector uses the "bv*[height&lt;=H]+ba/b[height&lt;=H]" shape: yt-dlp resolves the
    /// bv+ba half when separate video/audio streams exist (DASH) and falls back to the single
    /// combined "b" half automatically when they don't (progressive-only extractors) — so one
    /// expression form covers both cases without the caller needing to know which applies.
    /// </summary>
    internal static List<FormatOption> BuildVideoFormats(JsonElement formatsEl)
    {
        var heights = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        var maxFpsAtHeight = new Dictionary<int, double>();

        foreach (var fmt in formatsEl.EnumerateArray())
        {
            var vcodec = GetString(fmt, "vcodec");
            var acodec = GetString(fmt, "acodec");
            if ((vcodec is null or "none") && (acodec is null or "none"))
                continue; // storyboard or otherwise unusable entry

            if (vcodec is null or "none")
                continue; // pure audio format; not part of the video-quality list

            if (!TryGetInt(fmt, "height", out var height))
                continue;

            heights.Add(height);

            if (TryGetDouble(fmt, "fps", out var fps))
            {
                maxFpsAtHeight[height] = maxFpsAtHeight.TryGetValue(height, out var existing)
                    ? Math.Max(existing, fps)
                    : fps;
            }
        }

        var result = new List<FormatOption> { new("Best available", "bv*+ba/b", null) };

        foreach (var height in heights)
        {
            var label = $"{height}p";
            if (maxFpsAtHeight.TryGetValue(height, out var fps) && fps > 30)
                label += $"{(int)Math.Round(fps)}";

            result.Add(new FormatOption(label, $"bv*[height<={height}]+ba/b[height<={height}]", height));
        }

        return result;
    }

    /// <summary>
    /// Playlists are queued as a single yt-dlp invocation against the playlist URL rather than
    /// probed entry-by-entry (too slow), so we can't know real per-entry heights up front —
    /// offer a reasonable fixed set instead.
    /// </summary>
    private static List<FormatOption> GenericPlaylistFormats() =>
    [
        new("Best available", "bv*+ba/b", null),
        new("1080p", "bv*[height<=1080]+ba/b[height<=1080]", 1080),
        new("720p", "bv*[height<=720]+ba/b[height<=720]", 720),
        new("480p", "bv*[height<=480]+ba/b[height<=480]", 480),
    ];

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_binaryManager.ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryGetInt(JsonElement el, string prop, out int value)
    {
        value = 0;
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return false;
        return v.TryGetInt32(out value);
    }

    private static bool TryGetDouble(JsonElement el, string prop, out double value)
    {
        value = 0;
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return false;
        return v.TryGetDouble(out value);
    }
}
