using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YtDlpGui.Core;

public sealed record DownloadProgress(string TaskId, double Percent, string Speed, string Eta);

public sealed record DownloadRequest(
    string TaskId,
    string Url,
    string OutputDirectory,
    string FilenameTemplate,
    string Selector,
    string? AudioFormat,
    string? FfmpegPath,
    string? CookiesFromBrowser = null,
    string? CookiesFilePath = null);

/// <summary>
/// Runs yt-dlp as a subprocess per download. Concurrency is capped by a semaphore sized from
/// settings at construction time; live-resizing mid-session is intentionally not supported —
/// a changed concurrency limit takes effect on the next app start, keeping the gate simple.
/// </summary>
public sealed class DownloadService
{
    private const string ProgressMarker = "YTDLPGUI-PROGRESS ";

    private readonly YtDlpBinaryManager _binaryManager;
    private readonly SemaphoreSlim _concurrencyGate;

    public event EventHandler<DownloadProgress>? ProgressChanged;
    public event EventHandler<(string TaskId, bool Success, string Message)>? Finished;

    public DownloadService(YtDlpBinaryManager binaryManager, int maxConcurrentDownloads)
    {
        _binaryManager = binaryManager;
        _concurrencyGate = new SemaphoreSlim(Math.Max(1, maxConcurrentDownloads));
    }

    public async Task RunAsync(DownloadRequest request, CancellationToken ct)
    {
        await _concurrencyGate.WaitAsync(ct);
        try
        {
            await ExecuteAsync(request, ct);
        }
        catch (OperationCanceledException)
        {
            Finished?.Invoke(this, (request.TaskId, false, "Cancelled"));
        }
        catch (Exception ex)
        {
            Finished?.Invoke(this, (request.TaskId, false, ex.Message));
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private async Task ExecuteAsync(DownloadRequest request, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_binaryManager.ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in BuildArguments(request))
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        var outcome = new OutputOutcome();
        var stderrBuffer = new List<string>();

        var stdoutTask = PumpStdoutAsync(process, request.TaskId, outcome, ct);
        var stderrTask = PumpStderrAsync(process, stderrBuffer, ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode == 0)
        {
            Finished?.Invoke(this, (request.TaskId, true, outcome.ResolveMessage()));
        }
        else
        {
            var message = stderrBuffer.Count > 0 ? string.Join('\n', stderrBuffer[^Math.Min(5, stderrBuffer.Count)..]) : "yt-dlp exited with an error";
            var hint = CookieArgs.DescribeCookieFailureHint(message);
            if (hint is not null)
                message = $"{message}\n{hint}";
            Finished?.Invoke(this, (request.TaskId, false, message));
        }
    }

    /// <summary>
    /// Tracks the file path(s) yt-dlp actually wrote, parsed from its plain progress output
    /// rather than <c>--print</c> (which implies a quiet mode that would otherwise have to be
    /// fought with <c>--no-quiet</c>, and which prints the pre-merge *intended* filename even
    /// when ffmpeg is unavailable and the merge is silently skipped — leaving two real files on
    /// disk under different names than what was printed). A "[Merger]"/"[ExtractAudio]" line
    /// means post-processing produced one definitive final file, taking priority over the raw
    /// "[download] Destination:" line(s) that preceded it; without one of those (e.g. ffmpeg
    /// missing, so bv+ba downloaded but never merged), all destinations found are reported.
    /// </summary>
    private sealed class OutputOutcome
    {
        private static readonly Regex DestinationRegex = new(@"^\[download\] Destination: (.+)$", RegexOptions.Compiled);
        private static readonly Regex AlreadyDownloadedRegex = new(@"^\[download\] (.+) has already been downloaded$", RegexOptions.Compiled);
        private static readonly Regex MergerRegex = new(@"^\[Merger\] Merging formats into ""(.+)""$", RegexOptions.Compiled);
        private static readonly Regex ExtractAudioRegex = new(@"^\[ExtractAudio\] Destination: (.+)$", RegexOptions.Compiled);

        private readonly List<string> _destinations = [];
        private string? _finalPath;

        public void Observe(string line)
        {
            if (MergerRegex.Match(line) is { Success: true } merger)
                _finalPath = merger.Groups[1].Value;
            else if (ExtractAudioRegex.Match(line) is { Success: true } extractAudio)
                _finalPath = extractAudio.Groups[1].Value;
            else if (DestinationRegex.Match(line) is { Success: true } destination)
                _destinations.Add(destination.Groups[1].Value);
            else if (AlreadyDownloadedRegex.Match(line) is { Success: true } already)
                _destinations.Add(already.Groups[1].Value);
        }

        public string ResolveMessage()
        {
            if (_finalPath is not null)
                return _finalPath;

            return _destinations.Count switch
            {
                0 => "Download complete",
                1 => _destinations[0],
                _ => string.Join(", ", _destinations),
            };
        }
    }

    private static IEnumerable<string> BuildArguments(DownloadRequest request)
    {
        yield return "--newline";
        yield return "--no-warnings";
        yield return "--progress-template";
        yield return $"download:{ProgressMarker}%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s";
        yield return "-o";
        yield return Path.Combine(request.OutputDirectory, request.FilenameTemplate);

        if (!string.IsNullOrWhiteSpace(request.FfmpegPath))
        {
            yield return "--ffmpeg-location";
            yield return request.FfmpegPath;
        }

        foreach (var arg in CookieArgs.Build(request.CookiesFromBrowser, request.CookiesFilePath))
            yield return arg;

        if (!string.IsNullOrWhiteSpace(request.AudioFormat))
        {
            yield return "-x";
            yield return "--audio-format";
            yield return request.AudioFormat;
            yield return "-f";
            yield return "bestaudio/best";
        }
        else
        {
            yield return "-f";
            yield return request.Selector;
        }

        yield return request.Url;
    }

    private async Task PumpStdoutAsync(Process process, string taskId, OutputOutcome outcome, CancellationToken ct)
    {
        while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith(ProgressMarker, StringComparison.Ordinal))
            {
                var parsed = ParseProgressLine(taskId, line[ProgressMarker.Length..]);
                if (parsed is not null)
                    ProgressChanged?.Invoke(this, parsed);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                outcome.Observe(line.Trim());
            }
        }
    }

    private static async Task PumpStderrAsync(Process process, List<string> stderrBuffer, CancellationToken ct)
    {
        while (await process.StandardError.ReadLineAsync(ct) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                stderrBuffer.Add(line.Trim());
        }
    }

    private static DownloadProgress? ParseProgressLine(string taskId, string payload)
    {
        var parts = payload.Split('|');
        if (parts.Length != 5)
            return null;

        var downloaded = ParseDouble(parts[0]);
        var total = ParseDouble(parts[1]) ?? ParseDouble(parts[2]);
        var speed = ParseDouble(parts[3]);
        var eta = ParseDouble(parts[4]);

        var percent = downloaded is not null && total is > 0 ? downloaded.Value / total.Value * 100.0 : -1;

        return new DownloadProgress(taskId, percent, FormatSpeed(speed), FormatEta(eta));
    }

    private static double? ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string FormatSpeed(double? bytesPerSecond)
    {
        if (bytesPerSecond is null or <= 0)
            return "--";

        string[] units = ["B/s", "KiB/s", "MiB/s", "GiB/s"];
        var value = bytesPerSecond.Value;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.#} {units[unitIndex]}";
    }

    private static string FormatEta(double? seconds)
    {
        if (seconds is null or < 0)
            return "--:--";

        var span = TimeSpan.FromSeconds(seconds.Value);
        return span.Hours > 0
            ? $"{span.Hours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes}:{span.Seconds:D2}";
    }
}
