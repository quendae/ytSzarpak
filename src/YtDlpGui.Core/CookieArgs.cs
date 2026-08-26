namespace YtDlpGui.Core;

/// <summary>
/// Shared yt-dlp cookie-argument construction for <see cref="VideoMetadataService"/> and
/// <see cref="DownloadService"/>. YouTube (and most Google properties) reject yt-dlp's old
/// direct username/password login outright — it triggers the same captcha/2FA wall a script
/// can't solve — so the supported way to reach sign-in-gated videos is reusing cookies from a
/// browser the user is already logged into, or a manually exported cookies.txt file.
/// </summary>
internal static class CookieArgs
{
    /// <summary>
    /// A cookies.txt file takes priority over live browser extraction: live extraction has to
    /// copy the browser's locked cookie database out from under it (see
    /// <see cref="DescribeCookieFailureHint"/>), which fails outright on some Chrome/Windows
    /// combinations, while a file the user explicitly picked always just works.
    /// </summary>
    public static IEnumerable<string> Build(string? cookiesFromBrowser, string? cookiesFilePath)
    {
        if (!string.IsNullOrWhiteSpace(cookiesFilePath))
        {
            yield return "--cookies";
            yield return cookiesFilePath;
        }
        else if (!string.IsNullOrWhiteSpace(cookiesFromBrowser))
        {
            yield return "--cookies-from-browser";
            yield return cookiesFromBrowser;
        }
    }

    /// <summary>
    /// yt-dlp/yt-dlp#7271: on some Windows/Chrome combinations, "--cookies-from-browser" can't
    /// copy the browser's locked cookie database (the browser holds an exclusive lock while
    /// running, and newer Chrome versions add app-bound encryption on top). There's no fix on
    /// our side — the reliable workaround is a manually exported cookies.txt file instead.
    /// </summary>
    public static string? DescribeCookieFailureHint(string? diagnosticText)
    {
        if (string.IsNullOrEmpty(diagnosticText) || !diagnosticText.Contains("cookie database", StringComparison.OrdinalIgnoreCase))
            return null;

        return "Tip: this is a known yt-dlp/Chrome issue on Windows — Chrome keeps its cookie database " +
               "locked while running, so live extraction fails. In Settings > YouTube sign-in, browse to a " +
               "cookies.txt file exported from your browser instead (e.g. with a \"cookies.txt\" browser " +
               "extension) — a chosen file always takes priority and works even when Chrome extraction doesn't.";
    }
}
