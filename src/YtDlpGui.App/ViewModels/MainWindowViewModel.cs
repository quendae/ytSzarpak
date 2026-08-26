using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDlpGui.Core;
using YtDlpGui.Core.Models;

namespace YtDlpGui.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly VideoMetadataService _metadataService;
    private readonly DownloadService _downloadService;
    private readonly YtDlpBinaryManager _binaryManager;
    private readonly FfmpegLocator _ffmpegLocator;
    private readonly SettingsService _settingsService;
    private readonly FfmpegBinaryManager _ffmpegBinaryManager;

    private readonly CancellationTokenSource _lifetimeCts = new();

    /// <summary>
    /// Held so the fire-and-forget bootstrap task is rooted and its faults are observed
    /// (it never throws — see <see cref="InitializeAsync"/> — but keeping the reference
    /// avoids an unobserved-task-exception if that ever changes).
    /// </summary>
    private readonly Task _initTask;

    /// <summary>Exposed so the bootstrap task can be awaited (tests, integration) rather than dangling.</summary>
    public Task InitializationTask => _initTask;

    private string? _pendingUpdateDownloadUrl;

    /// <summary>Raised by <see cref="OpenSettingsCommand"/>; the host window owns showing the dialog.</summary>
    public event EventHandler? SettingsRequested;

    public MainWindowViewModel(
        VideoMetadataService metadataService,
        DownloadService downloadService,
        YtDlpBinaryManager binaryManager,
        FfmpegLocator ffmpegLocator,
        SettingsService settingsService,
        FfmpegBinaryManager ffmpegBinaryManager)
    {
        _metadataService = metadataService;
        _downloadService = downloadService;
        _binaryManager = binaryManager;
        _ffmpegLocator = ffmpegLocator;
        _settingsService = settingsService;
        _ffmpegBinaryManager = ffmpegBinaryManager;

        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _downloadService.Finished += OnDownloadFinished;

        RefreshSignInStatus();
        _initTask = InitializeAsync();
    }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    public static IReadOnlyList<string> AudioFormats { get; } = ["mp3", "m4a", "opus", "wav", "flac"];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    public partial string UrlInput { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    public partial bool IsFetching { get; set; }

    [ObservableProperty]
    public partial string? FetchError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
    public partial VideoInfo? FetchedVideo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
    public partial FormatOption? SelectedFormat { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
    public partial bool IsAudioOnly { get; set; }

    [ObservableProperty]
    public partial string SelectedAudioFormat { get; set; } = "mp3";

    [ObservableProperty]
    public partial bool QueueEntirePlaylist { get; set; }

    [ObservableProperty]
    public partial bool IsBootstrapping { get; set; }

    [ObservableProperty]
    public partial string? BootstrapMessage { get; set; }

    [ObservableProperty]
    public partial double BootstrapProgress { get; set; }

    [ObservableProperty]
    public partial string? UpdateAvailableMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateNowCommand))]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    [ObservableProperty]
    public partial bool IsFfmpegAvailable { get; set; }

    [ObservableProperty]
    public partial string? FfmpegPath { get; set; }

    [ObservableProperty]
    public partial string? FfmpegWarning { get; set; }

    [ObservableProperty]
    public partial string? SignInStatusText { get; set; }

    // --- Bootstrap -------------------------------------------------------------------------

    private async Task InitializeAsync()
    {
        var ct = _lifetimeCts.Token;

        try
        {
            // Must happen before anything spawns the binary, while nothing is holding it open.
            _binaryManager.ApplyPendingUpdateIfAny();

            if (!_binaryManager.IsAvailable)
            {
                IsBootstrapping = true;
                BootstrapMessage = "Downloading yt-dlp...";
                BootstrapProgress = 0;

                var progress = new Progress<double>(percent =>
                    Dispatcher.UIThread.Post(() => BootstrapProgress = percent));

                await _binaryManager.EnsureAvailableAsync(progress, ct).ConfigureAwait(true);
            }

            IsBootstrapping = false;
        }
        catch (OperationCanceledException)
        {
            IsBootstrapping = false;
            return;
        }
        catch (Exception ex)
        {
            // Leave the overlay up: without the binary nothing else in the app can work,
            // so the message needs to stay on screen rather than flash past.
            BootstrapProgress = 0;
            BootstrapMessage = $"Could not download yt-dlp: {ex.Message}";
            return;
        }

        RefreshFfmpegStatus();

        if (!IsFfmpegAvailable)
            await AcquireFfmpegAsync(ct).ConfigureAwait(true);

        _ = CheckForUpdatesAsync(ct);
    }

    /// <summary>
    /// Unlike yt-dlp, a failed ffmpeg auto-download is not fatal: merging/audio-conversion stay
    /// unavailable (the existing warning banner already covers that), but the rest of the app —
    /// fetching, browsing formats, plain downloads that don't need ffmpeg — still works. So this
    /// clears the bootstrap overlay either way instead of leaving it stuck like the yt-dlp path.
    /// </summary>
    private async Task AcquireFfmpegAsync(CancellationToken ct)
    {
        IsBootstrapping = true;
        BootstrapMessage = "Downloading ffmpeg...";
        BootstrapProgress = 0;

        try
        {
            var progress = new Progress<double>(percent =>
                Dispatcher.UIThread.Post(() => BootstrapProgress = percent));

            await _ffmpegBinaryManager.AcquireAsync(progress, ct).ConfigureAwait(true);

            // Picks up the freshly downloaded managed copy via FfmpegLocator.
            RefreshFfmpegStatus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // Don't call RefreshFfmpegStatus() here — it would overwrite this specific message
            // with its generic "not found" text.
            FfmpegWarning = $"Automatic ffmpeg download failed ({ex.Message}). Install it manually or set a path in Settings.";
        }
        finally
        {
            IsBootstrapping = false;
        }
    }

    /// <summary>Re-reads the sign-in configuration; safe to call after the Settings dialog closes.</summary>
    public void RefreshSignInStatus()
    {
        var settings = _settingsService.Current;
        SignInStatusText = !string.IsNullOrWhiteSpace(settings.YouTubeCookiesFromBrowser)
            ? $"Signed in via {settings.YouTubeCookiesFromBrowser[0].ToString().ToUpperInvariant()}{settings.YouTubeCookiesFromBrowser[1..]}"
            : !string.IsNullOrWhiteSpace(settings.YouTubeCookiesFilePath)
                ? "Signed in via a cookies file"
                : null;
    }

    /// <summary>Re-reads the ffmpeg location; also safe to call after the Settings dialog closes.</summary>
    public void RefreshFfmpegStatus()
    {
        FfmpegPath = _ffmpegLocator.Find(_settingsService.Current.FfmpegPathOverride);
        IsFfmpegAvailable = !string.IsNullOrWhiteSpace(FfmpegPath);
        FfmpegWarning = IsFfmpegAvailable
            ? null
            : "ffmpeg not found — audio conversion and merging high-quality video+audio will not work. Set a path in Settings.";
    }

    private async Task CheckForUpdatesAsync(CancellationToken ct)
    {
        try
        {
            var result = await _binaryManager.CheckForUpdateAsync(force: false, _settingsService, ct)
                .ConfigureAwait(true);

            if (!result.UpdateAvailable || string.IsNullOrWhiteSpace(result.DownloadUrl))
                return;

            _pendingUpdateDownloadUrl = result.DownloadUrl;
            UpdateAvailableMessage = $"yt-dlp update available: {result.LatestVersion}";
            UpdateNowCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // An update check is opportunistic; a network hiccup must not surface as an error.
        }
    }

    // --- Commands --------------------------------------------------------------------------

    private bool CanFetch() => !IsFetching && !string.IsNullOrWhiteSpace(UrlInput);

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchAsync()
    {
        if (!CanFetch())
            return;

        IsFetching = true;
        FetchError = null;

        try
        {
            var settings = _settingsService.Current;
            var info = await _metadataService
                .FetchInfoAsync(UrlInput.Trim(), settings.YouTubeCookiesFromBrowser, settings.YouTubeCookiesFilePath, _lifetimeCts.Token)
                .ConfigureAwait(true);

            FetchedVideo = info;
            SelectedFormat = info.VideoFormats.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            FetchedVideo = null;
            SelectedFormat = null;
            FetchError = ex.Message;
        }
        finally
        {
            IsFetching = false;
        }
    }

    private bool CanAddToQueue() =>
        FetchedVideo is not null && (IsAudioOnly || SelectedFormat is not null);

    /// <summary>
    /// Always exactly one <see cref="DownloadRequest"/> per click: yt-dlp itself expands a
    /// playlist URL into all of its entries within a single process run, and there is no clean
    /// way to force "only this one entry" out of a playlist-context URL — so
    /// <see cref="QueueEntirePlaylist"/> is a user acknowledgement, not a code branch.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddToQueue))]
    private void AddToQueue()
    {
        var video = FetchedVideo;
        if (video is null)
            return;

        var settings = _settingsService.Current;
        var taskId = Guid.NewGuid().ToString();

        var request = new DownloadRequest(
            TaskId: taskId,
            Url: UrlInput.Trim(),
            OutputDirectory: settings.OutputDirectory,
            FilenameTemplate: settings.FilenameTemplate,
            Selector: SelectedFormat?.Selector ?? "bv*+ba/b",
            AudioFormat: IsAudioOnly ? SelectedAudioFormat : null,
            FfmpegPath: _ffmpegLocator.Find(settings.FfmpegPathOverride),
            CookiesFromBrowser: settings.YouTubeCookiesFromBrowser,
            CookiesFilePath: settings.YouTubeCookiesFilePath);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        var item = new QueueItemViewModel
        {
            TaskId = taskId,
            Url = request.Url,
            Title = video.Title,
            IsAudioOnly = IsAudioOnly,
            Status = DownloadStatus.Queued,
            Cts = cts,
        };

        Queue.Add(item);

        // Deliberately not awaited: RunAsync only completes when the whole download does, and
        // DownloadService's own semaphore queues anything past the concurrency limit.
        _ = _downloadService.RunAsync(request, cts.Token);
    }

    /// <summary>
    /// Removes finished entries (completed/failed/cancelled) from the queue. Active or still-queued
    /// downloads are left alone — clicking with nothing eligible is a harmless no-op, so this
    /// doesn't need a CanExecute guard wired to every item's Status changing.
    /// </summary>
    [RelayCommand]
    private void ClearQueue()
    {
        for (var i = Queue.Count - 1; i >= 0; i--)
        {
            if (Queue[i].Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                Queue.RemoveAt(i);
        }
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private bool CanUpdateNow() => !IsUpdating && !string.IsNullOrWhiteSpace(_pendingUpdateDownloadUrl);

    [RelayCommand(CanExecute = nameof(CanUpdateNow))]
    private async Task UpdateNowAsync()
    {
        var url = _pendingUpdateDownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
            return;

        IsUpdating = true;
        UpdateProgress = 0;

        try
        {
            var progress = new Progress<double>(percent =>
                Dispatcher.UIThread.Post(() => UpdateProgress = percent));

            await _binaryManager.DownloadUpdateAsync(url, progress, _lifetimeCts.Token)
                .ConfigureAwait(true);

            // The new binary lands as a ".new" file and is swapped in by
            // ApplyPendingUpdateIfAny() at the next start — there is no live hot-swap.
            _pendingUpdateDownloadUrl = null;
            UpdateAvailableMessage = "Update downloaded — restart the app to apply.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            UpdateAvailableMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    // --- DownloadService events (may arrive on a background thread) -------------------------

    private void OnDownloadProgressChanged(object? sender, DownloadProgress e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = FindItem(e.TaskId);
            if (item is null)
                return;

            if (item.Status == DownloadStatus.Queued)
                item.Status = DownloadStatus.Downloading;

            item.Percent = e.Percent;
            item.Speed = e.Speed;
            item.Eta = e.Eta;
        });
    }

    private void OnDownloadFinished(object? sender, (string TaskId, bool Success, string Message) e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = FindItem(e.TaskId);
            if (item is null)
                return;

            item.ResultMessage = e.Message;
            item.Speed = "--";
            item.Eta = "--:--";

            if (e.Success)
            {
                item.Status = DownloadStatus.Completed;
                item.Percent = 100;
            }
            else
            {
                // A cancel flipped the status locally already; don't downgrade it to Failed.
                if (item.Status != DownloadStatus.Cancelled)
                    item.Status = DownloadStatus.Failed;
            }

            item.Cts?.Dispose();
            item.Cts = null;
        });
    }

    private QueueItemViewModel? FindItem(string taskId)
    {
        foreach (var item in Queue)
        {
            if (item.TaskId == taskId)
                return item;
        }
        return null;
    }
}
