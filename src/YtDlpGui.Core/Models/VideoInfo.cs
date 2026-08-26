namespace YtDlpGui.Core.Models;

public sealed record VideoInfo(
    string Title,
    string? ThumbnailUrl,
    bool IsPlaylist,
    int EntryCount,
    IReadOnlyList<FormatOption> VideoFormats);
