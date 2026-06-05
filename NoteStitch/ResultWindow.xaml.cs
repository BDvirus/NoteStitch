using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NoteStitch;

public sealed partial class ResultWindow : Window
{
    private readonly List<NotepadDoc> _docs;
    private string _mergedText = string.Empty;

    public ResultWindow(List<NotepadDoc> docs)
    {
        _docs = docs;
        InitializeComponent();

        Title = "NoteStitch — Merged Result";
        TitleLabel.Text = $"Merged Result ({docs.Count} document{(docs.Count == 1 ? "" : "s")})";

        ((FrameworkElement)Content).Loaded += (_, _) => RenderMergedText();
    }

    private void RenderMergedText()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _docs.Count; i++)
        {
            sb.AppendLine($"=== {_docs[i].Filename} ===");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrEmpty(_docs[i].Text) ? "(empty document)" : _docs[i].Text);
            if (i < _docs.Count - 1) sb.AppendLine();
        }
        _mergedText = sb.ToString();

        // Render with bold headers in RichEditBox
        MergedTextBox.IsReadOnly = false;
        var doc = MergedTextBox.Document;
        doc.SetText(TextSetOptions.None, "");

        void Append(string text, bool bold)
        {
            var range = doc.GetRange(int.MaxValue, int.MaxValue);
            range.SetText(TextSetOptions.None, text);
            int end = range.EndPosition;
            int start = end - text.Length;
            var fmt = doc.GetRange(start, end);
            fmt.CharacterFormat.Bold = bold ? FormatEffect.On : FormatEffect.Off;
        }

        for (int i = 0; i < _docs.Count; i++)
        {
            Append($"=== {_docs[i].Filename} ===\n", bold: true);
            Append("\n", bold: false);
            string body = string.IsNullOrEmpty(_docs[i].Text) ? "(empty document)" : _docs[i].Text;
            Append(body + "\n", bold: false);
            if (i < _docs.Count - 1)
                Append("\n", bold: false);
        }
    }

    private void OnBackClicked(object sender, RoutedEventArgs e) => Close();

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        var pkg = new DataPackage();
        pkg.SetText(_mergedText);
        Clipboard.SetContent(pkg);
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = "merged_notepads.txt";
        picker.FileTypeChoices.Add("Text files", [".txt"]);
        picker.FileTypeChoices.Add("All files",  ["."]);

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await Windows.Storage.FileIO.WriteTextAsync(file, _mergedText);
    }

    private async void OnSaveAndCloseClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = "merged_notepads.txt";
        picker.FileTypeChoices.Add("Text files", [".txt"]);
        picker.FileTypeChoices.Add("All files",  ["."]);

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await Windows.Storage.FileIO.WriteTextAsync(file, _mergedText);
        NotepadReader.CloseNotepadWindows(_docs, _docs);
        Close();
    }
}
