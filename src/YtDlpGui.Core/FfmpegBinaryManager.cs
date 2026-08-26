using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace YtDlpGui.Core;

/// <summary>
/// Downloads a static ffmpeg (+ffprobe) build into the app's data directory when the system has
/// none. Windows/Linux builds come from BtbN/FFmpeg-Builds on GitHub (a continuously-updated
/// "latest" release, GPL variant for the fullest codec set); BtbN does not publish macOS builds,
/// so macOS uses evermeet.cx's static-build service instead. <see cref="FfmpegLocator"/> checks
/// the resulting <see cref="AppPaths.FfmpegBinDirectory"/> automatically, so nothing else needs
/// to know a download happened.
/// </summary>
public sealed class FfmpegBinaryManager
{
    private const string BtbNLatestReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";

    private readonly string _binDirectory;
    private readonly HttpClient _http;

    public FfmpegBinaryManager(string binDirectory, HttpClient httpClient)
    {
        _binDirectory = binDirectory;
        Directory.CreateDirectory(_binDirectory);
        _http = httpClient;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("YTSzarpak/1.0");
    }

    private static string FfmpegExeName => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
    private static string FfprobeExeName => OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

    public string ManagedFfmpegPath => Path.Combine(_binDirectory, FfmpegExeName);

    public bool IsManagedAvailable => File.Exists(ManagedFfmpegPath);

    /// <summary>Downloads a static ffmpeg+ffprobe build for this OS/arch if not already present.</summary>
    public async Task<string> AcquireAsync(IProgress<double>? progress, CancellationToken ct)
    {
        if (IsManagedAvailable)
            return ManagedFfmpegPath;

        var workDir = Path.Combine(_binDirectory, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                await AcquireFromBtbNAsync(workDir, progress, ct);
            else if (OperatingSystem.IsMacOS())
                await AcquireFromEvermeetAsync(workDir, progress, ct);
            else
                throw new PlatformNotSupportedException("Automatic ffmpeg download is not supported on this OS.");

            if (!IsManagedAvailable)
                throw new InvalidOperationException("ffmpeg was downloaded but the executable could not be located afterwards.");

            return ManagedFfmpegPath;
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    // --- Windows / Linux: BtbN/FFmpeg-Builds -------------------------------------------------

    private async Task AcquireFromBtbNAsync(string workDir, IProgress<double>? progress, CancellationToken ct)
    {
        var assetName = BtbNAssetName();
        var downloadUrl = await FetchBtbNAssetUrlAsync(assetName, ct)
            ?? throw new InvalidOperationException($"Could not find an ffmpeg build ({assetName}) for this platform.");

        var archivePath = Path.Combine(workDir, assetName);
        await HttpDownloadHelper.DownloadFileWithProgressAsync(_http, downloadUrl, archivePath, progress, ct);

        var extractDir = Path.Combine(workDir, "extracted");
        Directory.CreateDirectory(extractDir);

        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDir);
        }
        else
        {
            // .NET has no built-in LZMA/xz decoder; system `tar` (present on effectively every
            // Linux distro and xz-aware) extracts it instead of pulling in a native dependency.
            await RunTarExtractAsync(archivePath, extractDir, ct);
        }

        CopyExtractedBinary(extractDir, FfmpegExeName);
        CopyExtractedBinary(extractDir, FfprobeExeName);
    }

    private static string BtbNAssetName()
    {
        var isArm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;

        if (OperatingSystem.IsWindows())
            return isArm64 ? "ffmpeg-master-latest-winarm64-gpl.zip" : "ffmpeg-master-latest-win64-gpl.zip";
        if (OperatingSystem.IsLinux())
            return isArm64 ? "ffmpeg-master-latest-linuxarm64-gpl.tar.xz" : "ffmpeg-master-latest-linux64-gpl.tar.xz";

        throw new PlatformNotSupportedException();
    }

    private async Task<string?> FetchBtbNAssetUrlAsync(string assetName, CancellationToken ct)
    {
        using var response = await _http.GetAsync(BtbNLatestReleaseUrl, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var nameEl) && nameEl.GetString() == assetName &&
                asset.TryGetProperty("browser_download_url", out var urlEl))
                return urlEl.GetString();
        }

        return null;
    }

    private static async Task RunTarExtractAsync(string archivePath, string extractDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("tar")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-xJf");
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(extractDir);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start tar to extract ffmpeg.");
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Failed to extract ffmpeg archive: {stderr}");
    }

    // --- macOS: evermeet.cx -----------------------------------------------------------------

    private async Task AcquireFromEvermeetAsync(string workDir, IProgress<double>? progress, CancellationToken ct)
    {
        var ffmpegUrl = await FetchEvermeetZipUrlAsync("ffmpeg", ct)
            ?? throw new InvalidOperationException("Could not determine an ffmpeg download URL for macOS.");
        var ffprobeUrl = await FetchEvermeetZipUrlAsync("ffprobe", ct)
            ?? throw new InvalidOperationException("Could not determine an ffprobe download URL for macOS.");

        var ffmpegZip = Path.Combine(workDir, "ffmpeg.zip");
        var ffprobeZip = Path.Combine(workDir, "ffprobe.zip");

        // Two separate downloads share the visible 0-100 progress range evenly.
        await HttpDownloadHelper.DownloadFileWithProgressAsync(
            _http, ffmpegUrl, ffmpegZip, new Progress<double>(p => progress?.Report(p / 2)), ct);
        await HttpDownloadHelper.DownloadFileWithProgressAsync(
            _http, ffprobeUrl, ffprobeZip, new Progress<double>(p => progress?.Report(50 + p / 2)), ct);

        var extractDir = Path.Combine(workDir, "extracted");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(ffmpegZip, extractDir);
        ZipFile.ExtractToDirectory(ffprobeZip, extractDir);

        CopyExtractedBinary(extractDir, FfmpegExeName);
        CopyExtractedBinary(extractDir, FfprobeExeName);
    }

    private async Task<string?> FetchEvermeetZipUrlAsync(string tool, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"https://evermeet.cx/ffmpeg/info/{tool}/release", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        return doc.RootElement.TryGetProperty("download", out var download) &&
               download.TryGetProperty("zip", out var zip) &&
               zip.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : null;
    }

    // --- Shared -------------------------------------------------------------------------------

    private void CopyExtractedBinary(string extractDir, string exeName)
    {
        var found = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"Could not find {exeName} in the downloaded ffmpeg archive.");

        var destination = Path.Combine(_binDirectory, exeName);
        File.Copy(found, destination, overwrite: true);
        HttpDownloadHelper.SetExecutableBitIfNeeded(destination);
    }
}
