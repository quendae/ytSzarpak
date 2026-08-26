using Avalonia.Controls;
using Avalonia.Platform.Storage;
using YtDlpGui.App.ViewModels;

namespace YtDlpGui.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Wire up folder picker for output directory
        viewModel.PickFolderAsync = async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Choose output folder"
                });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        };

        // Wire up file picker for ffmpeg path
        viewModel.PickFileAsync = async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Choose ffmpeg executable",
                    AllowMultiple = false
                });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        };

        // Wire up file picker for the cookies.txt file
        viewModel.PickCookiesFileAsync = async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Choose cookies.txt file",
                    AllowMultiple = false
                });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        };

        // Close window when save or cancel is requested
        viewModel.SaveCompleted += (_, _) => Close();
        viewModel.CancelRequested += (_, _) => Close();
    }
}
