namespace YtDlpGui.Core;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtDlpGui");

    public static string YtDlpBinDirectory { get; } = Path.Combine(AppDataDirectory, "bin");

    public static string FfmpegBinDirectory { get; } = Path.Combine(AppDataDirectory, "ffmpeg");
}
