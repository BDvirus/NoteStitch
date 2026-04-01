using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NoteStitch;

public class ResultForm : Form
{
    private RichTextBox _richTextBox = null!;
    private string _mergedText = string.Empty;
    private readonly List<NotepadDoc> _docs;

    public ResultForm(List<NotepadDoc> docs)
    {
        _docs = docs;
        InitializeComponent();
        DisplayMerged();
    }

    private void InitializeComponent()
    {
        Text = $"Merged Result — {_docs.Count} document{(_docs.Count == 1 ? "" : "s")}";
        Size = new Size(620, 560);
        MinimumSize = new Size(400, 300);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        // Top bar
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 0),
            WrapContents = false
        };

        var backButton = new Button { Text = "◀  Back", Width = 80 };
        backButton.Click += (_, _) => Close();

        var titleLabel = new Label
        {
            Text = $"Merged Result  ({_docs.Count} document{(_docs.Count == 1 ? "" : "s")})",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(8, 4, 0, 0)
        };

        topPanel.Controls.Add(backButton);
        topPanel.Controls.Add(titleLabel);

        // RichTextBox
        _richTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            Font = new Font("Consolas", 9.5f),
            WordWrap = true
        };

        // Bottom bar
        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 6),
            WrapContents = false
        };

        var copyButton = new Button { Text = "Copy to Clipboard", Width = 130 };
        var saveButton = new Button { Text = "Save to File…", Width = 110 };
        var saveAndCloseButton = new Button { Text = "Save & Close Notepads", Width = 160 };

        copyButton.Click += OnCopyClicked;
        saveButton.Click += OnSaveClicked;
        saveAndCloseButton.Click += OnSaveAndCloseClicked;

        bottomPanel.Controls.Add(copyButton);
        bottomPanel.Controls.Add(saveButton);
        bottomPanel.Controls.Add(saveAndCloseButton);

        Controls.Add(_richTextBox);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);
    }

    private void DisplayMerged()
    {
        _richTextBox.Clear();

        var normalFont = new Font("Consolas", 9.5f);
        var boldFont = new Font("Consolas", 9.5f, FontStyle.Bold);
        var sb = new StringBuilder();

        for (int i = 0; i < _docs.Count; i++)
        {
            var doc = _docs[i];
            string header = $"=== {doc.Filename} ===";
            string body = string.IsNullOrEmpty(doc.Text) ? "(empty document)" : doc.Text;

            // Append header in bold
            _richTextBox.SelectionFont = boldFont;
            _richTextBox.AppendText(header + "\n\n");

            // Append body in normal font
            _richTextBox.SelectionFont = normalFont;
            _richTextBox.AppendText(body);

            if (i < _docs.Count - 1)
                _richTextBox.AppendText("\n\n");

            sb.AppendLine(header);
            sb.AppendLine();
            sb.AppendLine(body);
            if (i < _docs.Count - 1)
                sb.AppendLine();
        }

        _mergedText = sb.ToString();
        _richTextBox.SelectionStart = 0;
        _richTextBox.ScrollToCaret();
    }

    private void OnCopyClicked(object? sender, EventArgs e)
    {
        Clipboard.SetText(_mergedText);
        MessageBox.Show("Copied to clipboard.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Save Merged Text",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "merged_notepads.txt",
            DefaultExt = "txt"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dlg.FileName, _mergedText, Encoding.UTF8);
            MessageBox.Show($"Saved to:\n{dlg.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnSaveAndCloseClicked(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Save Merged Text",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "merged_notepads.txt",
            DefaultExt = "txt"
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        File.WriteAllText(dlg.FileName, _mergedText, Encoding.UTF8);
        NotepadReader.CloseNotepadWindows(_docs);
        MessageBox.Show($"Saved to:\n{dlg.FileName}\n\nAll Notepad windows have been closed.", "Done",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
