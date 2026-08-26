using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YtDlpGui.App.ViewModels;

public enum DownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Failed,
    Cancelled,
}

public partial class QueueItemViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string TaskId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowInFolderCommand))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    /// <summary>
    /// Left-edge accent color for the queue row, one per <see cref="DownloadStatus"/> — encodes
    /// real state rather than decorating, so it must stay in sync with Styles/Theme.axaml's palette.
    /// </summary>
    public IBrush StatusColor => Status switch
    {
        DownloadStatus.Downloading => new SolidColorBrush(Color.Parse("#35C7F0")),
        DownloadStatus.Completed => new SolidColorBrush(Color.Parse("#2ECC71")),
        DownloadStatus.Failed => new SolidColorBrush(Color.Parse("#E63946")),
        DownloadStatus.Cancelled => new SolidColorBrush(Color.Parse("#5A6B84")),
        _ => new SolidColorBrush(Color.Parse("#3A4B63")),
    };

    /// <summary>-1 means "unknown/indeterminate" (yt-dlp did not report a total size yet).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    [NotifyPropertyChangedFor(nameof(DisplayPercent))]
    public partial double Percent { get; set; } = -1;

    [ObservableProperty]
    public partial string Speed { get; set; } = "--";

    [ObservableProperty]
    public partial string Eta { get; set; } = "--:--";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowInFolderCommand))]
    public partial string? ResultMessage { get; set; }

    [ObservableProperty]
    public partial bool IsAudioOnly { get; set; }

    /// <summary>Owned by the enqueuing <see cref="MainWindowViewModel"/>; not observable.</summary>
    public CancellationTokenSource? Cts { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Url : Title;

    public bool IsIndeterminate => Percent < 0;

    /// <summary>Percent clamped for the ProgressBar, which cannot render a negative value.</summary>
    public double DisplayPercent => Percent < 0 ? 0 : Percent;

    private bool CanCancel() => Status is DownloadStatus.Queued or DownloadStatus.Downloading;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        Cts?.Cancel();
        Status = DownloadStatus.Cancelled;
    }

    private bool CanShowInFolder() =>
        Status == DownloadStatus.Completed && !string.IsNullOrWhiteSpace(ResultMessage);

    /// <summary>
    /// Best-effort convenience: yt-dlp's last stdout line is normally the final file path
    /// (--print after_move:filepath), but it is not guaranteed to be one, so this verifies the
    /// path exists first and silently does nothing if the OS file manager call fails.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowInFolder))]
    private void ShowInFolder()
    {
        var path = ResultMessage;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", ["/select,", path]);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", ["-R", path]);
            }
            else if (OperatingSystem.IsLinux())
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Process.Start("xdg-open", [directory]);
            }
        }
        catch
        {
            // Opening the file manager is a nicety, never a critical path — ignore failures.
        }
    }
}
