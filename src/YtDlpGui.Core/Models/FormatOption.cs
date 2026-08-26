namespace YtDlpGui.Core.Models;

/// <summary>
/// A quality choice presented to the user. <see cref="Selector"/> is a yt-dlp -f expression
/// (e.g. "bv*[height&lt;=1080]+ba/b[height&lt;=1080]"), never a raw format_id — explicit ids can
/// expire between metadata fetch and the actual download, but selector expressions let yt-dlp's
/// own fallback logic resolve at download time.
/// </summary>
public sealed record FormatOption(string Label, string Selector, int? Height);
