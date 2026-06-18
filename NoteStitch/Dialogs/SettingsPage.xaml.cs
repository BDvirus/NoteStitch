// NoteStitch — Stitch multiple Notepad windows into one document.
// Copyright (C) 2026 Dvirus
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
