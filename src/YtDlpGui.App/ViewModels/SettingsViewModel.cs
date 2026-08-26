using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDlpGui.Core;
using YtDlpGui.Core.Models;

namespace YtDlpGui.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    /// <summary>The "no browser selected" sentinel shown in the picker; maps to a null setting.</summary>
    public const string NoBrowser = "None";

    /// <summary>yt-dlp's supported --cookies-from-browser values, title-cased for display.</summary>
    public static IReadOnlyList<string> BrowserOptions { get; } =
        [NoBrowser, "Chrome", "Firefox", "Edge", "Brave", "Opera", "Vivaldi", "Chromium", "Safari", "Whale"];

    private readonly SettingsService _settingsService;
    private readonly FfmpegLocator _ffmpegLocator;

    [ObservableProperty]
    public partial string OutputDirectory { get; set; }

    [ObservableProperty]
    public partial int MaxConcurrentDownloads { get; set; }

    [ObservableProperty]
    public partial string FilenameTemplate { get; set; }

    [ObservableProperty]
    public partial string? FfmpegPathOverride { get; set; }

    [ObservableProperty]
    public partial string FfmpegStatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsFfmpegDetected { get; set; }

    /// <summary>
    /// One of <see cref="BrowserOptions"/>. Reusing the login already sitting in a browser is the
    /// only sign-in method that actually works against YouTube — yt-dlp's old direct
    /// username/password login gets stopped by Google's captcha/2FA wall, so this app never asks
    /// for a password. Takes priority over <see cref="CookiesFilePath"/> when both are set.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedBrowser { get; set; } = NoBrowser;

    /// <summary>Path to a Netscape-format cookies.txt, used only when <see cref="SelectedBrowser"/> is None.</summary>
    [ObservableProperty]
    public partial string? CookiesFilePath { get; set; }

    /// <summary>
    /// Delegate for folder picking. Should be set by the View to point to Avalonia's StorageProvider.
    /// </summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>
    /// Delegate for picking the ffmpeg executable. Should be set by the View to point to Avalonia's StorageProvider.
    /// </summary>
    public Func<Task<string?>>? PickFileAsync { get; set; }

    /// <summary>
    /// Delegate for picking a cookies.txt file. Separate from <see cref="PickFileAsync"/> so the
    /// View can show a picker dialog titled for cookies rather than for ffmpeg.
    /// </summary>
    public Func<Task<string?>>? PickCookiesFileAsync { get; set; }

    public event EventHandler? SaveCompleted;
    public event EventHandler? CancelRequested;

    public SettingsViewModel(SettingsService settingsService, FfmpegLocator ffmpegLocator)
    {
        _settingsService = settingsService;
        _ffmpegLocator = ffmpegLocator;

        // Initialize from settings
        OutputDirectory = settingsService.Current.OutputDirectory;
        MaxConcurrentDownloads = settingsService.Current.MaxConcurrentDownloads;
        FilenameTemplate = settingsService.Current.FilenameTemplate;
        FfmpegPathOverride = settingsService.Current.FfmpegPathOverride;
        CookiesFilePath = settingsService.Current.YouTubeCookiesFilePath;
        SelectedBrowser = BrowserOptions.FirstOrDefault(b =>
            string.Equals(b, settingsService.Current.YouTubeCookiesFromBrowser, StringComparison.OrdinalIgnoreCase))
            ?? NoBrowser;

        // Initialize ffmpeg status
        RefreshFfmpegStatus();
    }

    partial void OnFfmpegPathOverrideChanged(string? value)
    {
        RefreshFfmpegStatus();
    }

    private void RefreshFfmpegStatus()
    {
        var detectedPath = _ffmpegLocator.Find(FfmpegPathOverride);
        if (detectedPath != null)
        {
            IsFfmpegDetected = true;
            FfmpegStatusMessage = $"Detected: {detectedPath}";
        }
        else
        {
            IsFfmpegDetected = false;
            FfmpegStatusMessage = "ffmpeg not found";
        }
    }

    [RelayCommand]
    public async Task BrowseOutputDirectory()
    {
        if (PickFolderAsync == null)
            return;

        var selectedPath = await PickFolderAsync();
        if (selectedPath != null)
        {
            OutputDirectory = selectedPath;
        }
    }

    [RelayCommand]
    public async Task BrowseFfmpegPath()
    {
        if (PickFileAsync == null)
            return;

        var selectedPath = await PickFileAsync();
        if (selectedPath != null)
        {
            FfmpegPathOverride = selectedPath;
        }
    }

    [RelayCommand]
    public async Task BrowseCookiesFile()
    {
        if (PickCookiesFileAsync == null)
            return;

        var selectedPath = await PickCookiesFileAsync();
        if (selectedPath != null)
        {
            CookiesFilePath = selectedPath;
        }
    }

    [RelayCommand]
    public void Save()
    {
        // Update settings object with current values
        _settingsService.Current.OutputDirectory = OutputDirectory;
        _settingsService.Current.MaxConcurrentDownloads = MaxConcurrentDownloads;
        _settingsService.Current.FilenameTemplate = FilenameTemplate;
        _settingsService.Current.FfmpegPathOverride = FfmpegPathOverride;
        _settingsService.Current.YouTubeCookiesFromBrowser =
            SelectedBrowser == NoBrowser ? null : SelectedBrowser.ToLowerInvariant();
        _settingsService.Current.YouTubeCookiesFilePath = CookiesFilePath;

        // Persist to disk
        _settingsService.Save();

        // Notify that save completed
        SaveCompleted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
