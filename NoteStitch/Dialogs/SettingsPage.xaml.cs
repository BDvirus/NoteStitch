using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace NoteStitch;

public sealed partial class SettingsPage : Page
{
    private AppSettings? _settings;
    private TaskCompletionSource<bool>? _tcs;

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not (AppSettings settings, TaskCompletionSource<bool> tcs)) return;
        _settings = settings;
        _tcs = tcs;
        FolderBox.Text            = settings.AutoSaveFolder;
        IncludeSavedCb.IsChecked  = settings.IncludeSavedFiles;
        IncludeMergedCb.IsChecked = settings.IncludeMergedFiles;
        RunOnStartupCb.IsChecked  = StartupManager.IsEnabled() || settings.RunOnWindowsStartup;
    }

    private async void OnBrowseClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow!);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            FolderBox.Text = folder.Path;
    }

    private void OnSaveClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            _settings.AutoSaveFolder    = FolderBox.Text.Trim();
            _settings.IncludeSavedFiles  = IncludeSavedCb.IsChecked  == true;
            _settings.IncludeMergedFiles = IncludeMergedCb.IsChecked == true;
            _settings.RunOnWindowsStartup = RunOnStartupCb.IsChecked == true;
        }
        _tcs?.SetResult(true);
        Frame.GoBack();
    }

    private void OnCancelClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _tcs?.SetResult(false);
        Frame.GoBack();
    }
}
