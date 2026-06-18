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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace NoteStitch;

public sealed partial class ShortcutPage : Page
{
    private TaskCompletionSource<(string key, bool isAutoSave)?>? _tcs;

    public ShortcutPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not (string autoSaveFolder, TaskCompletionSource<(string, bool)?> tcs)) return;
        _tcs = tcs;

        var keys = Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
            .Concat(Enumerable.Range('0', 10).Select(c => ((char)c).ToString()))
            .ToArray();
        foreach (var k in keys) KeyCombo.Items.Add(k);
        KeyCombo.SelectedItem = "N";

        if (string.IsNullOrEmpty(autoSaveFolder))
        {
            AutoSaveRadio.IsEnabled = false;
            WarnLabel.Visibility    = Visibility.Visible;
        }
    }

    private void OnCreateClicked(object sender, RoutedEventArgs e)
    {
        _tcs?.SetResult((KeyCombo.SelectedItem?.ToString() ?? "N", AutoSaveRadio.IsChecked == true));
        Frame.GoBack();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        _tcs?.SetResult(null);
        Frame.GoBack();
    }
}
