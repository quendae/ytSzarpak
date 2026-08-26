using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YtDlpGui.App.ViewModels;
using YtDlpGui.App.Views;
using YtDlpGui.Core;

namespace YtDlpGui.App;

public partial class App : Application
{
    // Held for the process lifetime; both binary-manager downloads and metadata/download
    // subprocesses share it, so it's constructed once here rather than per-service.
    private readonly HttpClient _httpClient = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService(AppPaths.AppDataDirectory);
            var binaryManager = new YtDlpBinaryManager(AppPaths.YtDlpBinDirectory, _httpClient);
            var ffmpegLocator = new FfmpegLocator();
            var ffmpegBinaryManager = new FfmpegBinaryManager(AppPaths.FfmpegBinDirectory, _httpClient);
            var metadataService = new VideoMetadataService(binaryManager);
            var downloadService = new DownloadService(binaryManager, settingsService.Current.MaxConcurrentDownloads);

            var mainViewModel = new MainWindowViewModel(
                metadataService, downloadService, binaryManager, ffmpegLocator, settingsService, ffmpegBinaryManager);

            mainViewModel.SettingsRequested += (_, _) =>
            {
                var settingsViewModel = new SettingsViewModel(settingsService, ffmpegLocator);
                var settingsWindow = new SettingsWindow(settingsViewModel);

                // Concurrency changes only take effect on next start (DownloadService's
                // semaphore is sized once, at construction); ffmpeg/sign-in status refresh live.
                settingsViewModel.SaveCompleted += (_, _) =>
                {
                    mainViewModel.RefreshFfmpegStatus();
                    mainViewModel.RefreshSignInStatus();
                };

                if (desktop.MainWindow is not null)
                    settingsWindow.ShowDialog(desktop.MainWindow);
                else
                    settingsWindow.Show();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
