namespace YtDlpGui.Core.Models;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home
            ? Path.Combine(home, "Downloads")
            : Directory.GetCurrentDirectory();

    public int MaxConcurrentDownloads { get; set; } = 2;

    public string FilenameTemplate { get; set; } = "%(title)s [%(id)s].%(ext)s";

    public string? FfmpegPathOverride { get; set; }

    public DateTime? LastYtDlpUpdateCheckUtc { get; set; }

    /// <summary>
    /// A yt-dlp browser id ("chrome", "firefox", "edge", ...) to reuse an existing login from,
    /// or null for none. Takes priority over <see cref="YouTubeCookiesFilePath"/> when both are set.
    /// </summary>
    public string? YouTubeCookiesFromBrowser { get; set; }

    /// <summary>Path to a Netscape-format cookies.txt, used only when no browser is selected.</summary>
    public string? YouTubeCookiesFilePath { get; set; }
}
