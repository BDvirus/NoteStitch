using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace NoteStitch;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        VersionLabel.Text = $"v{UpdateChecker.CurrentVersion.ToString(3)}";

        var bytes = IconHelper.GetIcoBytes();
        if (bytes is not null)
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            _ = bmp.SetSourceAsync(ms.AsRandomAccessStream());
            AppIcon.Source = bmp;
        }

        Loaded += async (_, _) => { await Task.Yield(); PopulateAboutText(); };
    }

    private void PopulateAboutText()
    {
        AboutText.IsReadOnly = false;
        var doc = AboutText.Document;
        doc.SetText(TextSetOptions.None, "");

        void Append(string text, bool bold, Color? color = null)
        {
            var range = doc.GetRange(int.MaxValue, int.MaxValue);
            range.SetText(TextSetOptions.None, text);
            int end = range.EndPosition;
            int start = end - text.Length;
            var fmt = doc.GetRange(start, end);
            fmt.CharacterFormat.Bold = bold ? FormatEffect.On : FormatEffect.Off;
            if (color.HasValue) fmt.CharacterFormat.ForegroundColor = color.Value;
        }

        Append("Created by ", false);
        Append("Dvirus", true, Color.FromArgb(255, 0, 100, 200));
        Append("\n\n", false);
        Append("GitHub: ", false);
        Append("github.com/BDvirus/NoteStitch", false, Color.FromArgb(255, 0, 100, 200));
        Append("\n\nNoteStitch detects all open Notepad windows and merges their content into a single document. Supports Windows 10 and Windows 11 (including multi-tab Notepad).", false);
        AboutText.IsReadOnly = true;
    }

    private void OnCloseClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        Frame.GoBack();
}
