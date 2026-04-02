using System.Drawing;
using System.Windows.Forms;

namespace NoteStitch;

internal class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "About the Author";
        Size = new Size(460, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.White;

        // Icon panel on the left
        var iconBox = new PictureBox
        {
            Size = new Size(64, 64),
            Left = 24,
            Top = 24,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = IconHelper.Load().ToBitmap()
        };

        // App name
        var appNameLabel = new Label
        {
            Text = "NoteStitch",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            Left = 104, Top = 20,
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 30, 30)
        };

        var taglineLabel = new Label
        {
            Text = "Stitch multiple Notepad windows into one document.",
            Font = new Font("Segoe UI", 9f),
            Left = 106, Top = 52,
            AutoSize = true,
            ForeColor = Color.Gray
        };

        // Separator
        var sep = new Panel
        {
            Left = 24, Top = 102,
            Width = 396, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        };

        // About text
        var aboutBox = new RichTextBox
        {
            Left = 24, Top = 116,
            Width = 396, Height = 200,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.None,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(40, 40, 40)
        };

        aboutBox.AppendText("Developed by  ");
        int nameStart = aboutBox.TextLength;
        aboutBox.AppendText("Dvirus");
        aboutBox.Select(nameStart, 6);
        aboutBox.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        aboutBox.SelectionColor = Color.FromArgb(0, 100, 200);
        aboutBox.Select(aboutBox.TextLength, 0);
        aboutBox.SelectionFont = new Font("Segoe UI", 9.5f);
        aboutBox.SelectionColor = Color.FromArgb(40, 40, 40);

        aboutBox.AppendText(
            "\n\nNoteStitch was designed with a focus on performance, " +
            "simplicity, and forward-thinking workflow automation. " +
            "Rather than polling or refreshing manually, it listens to " +
            "the OS directly — responding instantly as Notepad windows " +
            "open, close, or change.\n\n" +
            "The architecture favors zero dependencies, a minimal footprint, " +
            "and native Windows integration — built to stay out of your way " +
            "until you need it.");

        // Vision section
        var visionLabel = new Label
        {
            Text = "Vision",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Left = 24, Top = 296,
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var visionText = new Label
        {
            Text = "Planned: cloud sync, multi-format export, and global hotkey merge.",
            Font = new Font("Segoe UI", 9f),
            Left = 24, Top = 314,
            Width = 396,
            AutoSize = false,
            Height = 20,
            ForeColor = Color.Gray
        };

        // Close button
        var closeBtn = new Button
        {
            Text = "Close",
            Width = 80,
            Left = 340,
            Top = 358,
            DialogResult = DialogResult.OK
        };
        AcceptButton = closeBtn;

        Controls.AddRange([
            iconBox, appNameLabel, taglineLabel, sep,
            aboutBox, visionLabel, visionText, closeBtn
        ]);
    }
}
