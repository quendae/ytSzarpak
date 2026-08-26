namespace YtDlpGui.Core;

/// <summary>
/// Detects a usable ffmpeg install: a manual override from settings, a system PATH/common-location
/// install, or — since <see cref="FfmpegBinaryManager"/> can fetch a static build automatically —
/// the app's own managed copy under <see cref="AppPaths.FfmpegBinDirectory"/>.
/// </summary>
public sealed class FfmpegLocator
{
    public string? Find(string? manualOverride)
    {
        if (!string.IsNullOrWhiteSpace(manualOverride) && File.Exists(manualOverride))
            return manualOverride;

        var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        var managedCandidate = Path.Combine(AppPaths.FfmpegBinDirectory, exeName);
        if (File.Exists(managedCandidate))
            return managedCandidate;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var candidate in CommonInstallLocations(exeName))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> CommonInstallLocations(string exeName)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(@"C:\ffmpeg\bin", exeName);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Path.Combine(programFiles, "ffmpeg", "bin", exeName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine("/opt/homebrew/bin", exeName);
            yield return Path.Combine("/usr/local/bin", exeName);
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return Path.Combine("/usr/bin", exeName);
            yield return Path.Combine("/usr/local/bin", exeName);
            yield return Path.Combine("/snap/bin", exeName);
        }
    }
}
