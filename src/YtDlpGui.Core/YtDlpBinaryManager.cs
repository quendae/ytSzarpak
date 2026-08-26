using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace YtDlpGui.Core;

public sealed record UpdateCheckResult(bool UpdateAvailable, string? CurrentVersion, string? LatestVersion, string? DownloadUrl);

/// <summary>
/// Locates, first-run-downloads, version-checks, and updates the standalone yt-dlp executable.
/// yt-dlp is never a pip/Python dependency here: the official standalone builds
/// (yt-dlp.exe / yt-dlp_macos / yt-dlp_linux[_aarch64]) are themselves self-contained,
/// so nothing beyond this one file needs to exist on the target machine.
/// </summary>
public sealed class YtDlpBinaryManager
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);

    private readonly string _binDirectory;
    private readonly HttpClient _http;

    public YtDlpBinaryManager(string binDirectory, HttpClient httpClient)
    {
        _binDirectory = binDirectory;
        Directory.CreateDirectory(_binDirectory);
        _http = httpClient;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("YtDlpGui/1.0");
    }

    public string ExecutablePath => Path.Combine(_binDirectory, LocalExecutableName());

    public bool IsAvailable => File.Exists(ExecutablePath);

    private string PendingUpdatePath => ExecutablePath + ".new";

    private static string LocalExecutableName() => OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    private static string ReleaseAssetName()
    {
        if (OperatingSystem.IsWindows()) return "yt-dlp.exe";
        if (OperatingSystem.IsMacOS()) return "yt-dlp_macos";
        if (OperatingSystem.IsLinux())
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "yt-dlp_linux_aarch64" : "yt-dlp_linux";
        throw new PlatformNotSupportedException("Unsupported OS for yt-dlp binary selection.");
    }

    /// <summary>
    /// Swaps a previously downloaded update into place. Must be called once at startup,
    /// before any download/metadata calls, and only when no process is using the old binary.
    /// </summary>
    public void ApplyPendingUpdateIfAny()
    {
        if (!File.Exists(PendingUpdatePath))
            return;

        if (File.Exists(ExecutablePath))
            File.Delete(ExecutablePath);
        File.Move(PendingUpdatePath, ExecutablePath);
        HttpDownloadHelper.SetExecutableBitIfNeeded(ExecutablePath);
    }

    /// <summary>Downloads the latest release binary if none is present yet (first run).</summary>
    public async Task EnsureAvailableAsync(IProgress<double>? progress, CancellationToken ct)
    {
        if (IsAvailable)
            return;

        var (_, downloadUrl) = await FetchLatestReleaseAsync(ct);
        if (downloadUrl is null)
            throw new InvalidOperationException("Could not determine a yt-dlp download URL for this platform.");

        await HttpDownloadHelper.DownloadFileWithProgressAsync(_http, downloadUrl, ExecutablePath, progress, ct);
        HttpDownloadHelper.SetExecutableBitIfNeeded(ExecutablePath);
    }

    public async Task<string?> GetLocalVersionAsync(CancellationToken ct)
    {
        if (!IsAvailable)
            return null;

        var psi = new ProcessStartInfo(ExecutablePath, "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi);
        if (process is null)
            return null;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output.Trim();
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(bool force, SettingsService settings, CancellationToken ct)
    {
        var last = settings.Current.LastYtDlpUpdateCheckUtc;
        if (!force && last.HasValue && DateTime.UtcNow - last.Value < UpdateCheckInterval)
            return new UpdateCheckResult(false, null, null, null);

        var currentVersion = await GetLocalVersionAsync(ct);
        var (latestVersion, downloadUrl) = await FetchLatestReleaseAsync(ct);

        settings.Current.LastYtDlpUpdateCheckUtc = DateTime.UtcNow;
        settings.Save();

        var updateAvailable = IsNewer(latestVersion, currentVersion);

        return new UpdateCheckResult(updateAvailable, currentVersion, latestVersion, downloadUrl);
    }

    public Task DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress, CancellationToken ct) =>
        HttpDownloadHelper.DownloadFileWithProgressAsync(_http, downloadUrl, PendingUpdatePath, progress, ct);

    private async Task<(string? Version, string? DownloadUrl)> FetchLatestReleaseAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(LatestReleaseUrl, ct);
        if (!response.IsSuccessStatusCode)
            return (null, null);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var version = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        var assetName = ReleaseAssetName();

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var nameEl) && nameEl.GetString() == assetName &&
                    asset.TryGetProperty("browser_download_url", out var urlEl))
                {
                    downloadUrl = urlEl.GetString();
                    break;
                }
            }
        }

        return (version, downloadUrl);
    }

    /// <summary>
    /// yt-dlp versions are zero-padded date strings ("2024.12.06[.rev]"), so ordinal string
    /// comparison is a safe stand-in for a real version-order comparison.
    /// </summary>
    internal static bool IsNewer(string? latestVersion, string? currentVersion) =>
        latestVersion is not null &&
        (currentVersion is null || string.CompareOrdinal(latestVersion, currentVersion) > 0);
}
