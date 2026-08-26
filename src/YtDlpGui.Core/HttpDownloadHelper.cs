namespace YtDlpGui.Core;

/// <summary>
/// Shared streaming-download-with-progress and executable-bit logic used by both
/// <see cref="YtDlpBinaryManager"/> and <see cref="FfmpegBinaryManager"/>.
/// </summary>
internal static class HttpDownloadHelper
{
    public static async Task DownloadFileWithProgressAsync(
        HttpClient http, string url, string destinationPath, IProgress<double>? progress, CancellationToken ct)
    {
        var tempPath = destinationPath + ".download";
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;

            await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (totalBytes is > 0)
                    progress?.Report(readTotal * 100.0 / totalBytes.Value);
            }
        }

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);
        progress?.Report(100);
    }

    public static void SetExecutableBitIfNeeded(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path,
            mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
