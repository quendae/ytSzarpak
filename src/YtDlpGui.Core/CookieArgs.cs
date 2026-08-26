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
    public static IEnumerable<string> Build(string? cookiesFromBrowser, string? cookiesFilePath)
    {
        if (!string.IsNullOrWhiteSpace(cookiesFromBrowser))
        {
            yield return "--cookies-from-browser";
            yield return cookiesFromBrowser;
        }
        else if (!string.IsNullOrWhiteSpace(cookiesFilePath))
        {
            yield return "--cookies";
            yield return cookiesFilePath;
        }
    }
}
