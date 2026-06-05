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
