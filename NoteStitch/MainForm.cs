using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NoteStitch;

public class MainForm : Form
{
    private Label _infoLabel = null!;
    private Panel _scrollPanel = null!;
    private FlowLayoutPanel _checklistPanel = null!;
    private Button _toggleAllButton = null!;
    private Button _mergeButton = null!;

    private List<NotepadDoc> _docs = new();
    private List<CheckBox> _checkBoxes = new();
    private bool _allSelected = true;

    // ── WinEvent hook ─────────────────────────────────────────────────────────
    private const uint EVENT_OBJECT_DESTROY     = 0x8001;
    private const uint EVENT_OBJECT_SHOW        = 0x8002;
    private const uint EVENT_OBJECT_NAMECHANGE  = 0x800C;
    private const uint EVENT_OBJECT_VALUECHANGE = 0x800E;
    private const uint WINEVENT_OUTOFCONTEXT    = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS  = 0x0002;
    private const uint GA_ROOT                  = 2;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private WinEventDelegate _winEventProc = null!;   // must stay rooted
    private IntPtr _hookShowDestroy  = IntPtr.Zero;
    private IntPtr _hookNameChange   = IntPtr.Zero;
    private IntPtr _hookValueChange  = IntPtr.Zero;
    private System.Windows.Forms.Timer _debounce = null!;
    // ─────────────────────────────────────────────────────────────────────────

    public MainForm()
    {
        InitializeComponent();
        Icon = IconHelper.Load();
        RefreshNotepads();
        InstallHook();
    }

    private void InitializeComponent()
    {
        Text = "NoteStitch";
        Size = new Size(480, 420);
        MinimumSize = new Size(380, 300);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        // Title
        var titleLabel = new Label
        {
            Text = "NoteStitch",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        // Info label
        _infoLabel = new Label
        {
            Text = "Scanning...",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            ForeColor = Color.Gray
        };

        // Scrollable checklist
        _checklistPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4)
        };

        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(8)
        };
        _scrollPanel.Controls.Add(_checklistPanel);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 6),
            WrapContents = false
        };

        _toggleAllButton = new Button { Text = "Deselect All", Width = 90 };
        _mergeButton = new Button { Text = "Merge  ▶", Width = 90, Enabled = false };
        var shortcutButton = new Button { Text = "⌨ Shortcut…", Width = 100 };

        _toggleAllButton.Click += OnToggleAll;
        _mergeButton.Click += OnMergeClicked;
        shortcutButton.Click += OnSetupShortcutClicked;

        buttonPanel.Controls.Add(_toggleAllButton);
        buttonPanel.Controls.Add(_mergeButton);
        buttonPanel.Controls.Add(shortcutButton);

        // Layout (add in reverse dock order)
        Controls.Add(_scrollPanel);
        Controls.Add(_infoLabel);
        Controls.Add(titleLabel);
        Controls.Add(buttonPanel);

        Padding = new Padding(8, 0, 8, 0);
    }

    // ── WinEvent ──────────────────────────────────────────────────────────────

    private void InstallHook()
    {
        _debounce = new System.Windows.Forms.Timer { Interval = 300 };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RefreshNotepads(); };

        _winEventProc = OnWinEvent;

        // Two focused hooks instead of a noisy range
        _hookShowDestroy = SetWinEventHook(
            EVENT_OBJECT_DESTROY, EVENT_OBJECT_SHOW,
            IntPtr.Zero, _winEventProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        _hookNameChange = SetWinEventHook(
            EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE,
            IntPtr.Zero, _winEventProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        _hookValueChange = SetWinEventHook(
            EVENT_OBJECT_VALUECHANGE, EVENT_OBJECT_VALUECHANGE,
            IntPtr.Zero, _winEventProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;

        // For value changes the event fires on the child Edit control,
        // so walk up to the root window to check the class.
        IntPtr root = eventType == EVENT_OBJECT_VALUECHANGE
            ? GetAncestor(hwnd, GA_ROOT)
            : (idObject == 0 ? hwnd : IntPtr.Zero);

        if (root == IntPtr.Zero) return;

        var cls = new StringBuilder(64);
        GetClassName(root, cls, cls.Capacity);
        if (cls.ToString() != "Notepad") return;

        _debounce.Stop();
        _debounce.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnhookWinEvent(_hookShowDestroy);
        UnhookWinEvent(_hookNameChange);
        UnhookWinEvent(_hookValueChange);
        _debounce.Dispose();
        base.OnFormClosed(e);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshNotepads()
    {
        _docs = NotepadReader.GetNotepadWindows();
        _checkBoxes.Clear();
        _checklistPanel.Controls.Clear();
        _allSelected = true;
        _toggleAllButton.Text = "Deselect All";

        if (_docs.Count == 0)
        {
            _infoLabel.Text = "No Notepad windows found. Open Notepad to begin.";
            _infoLabel.ForeColor = Color.OrangeRed;
            _mergeButton.Enabled = false;
            return;
        }

        _infoLabel.Text = $"{_docs.Count} Notepad window{(_docs.Count == 1 ? "" : "s")} found";
        _infoLabel.ForeColor = Color.Gray;

        foreach (var doc in _docs)
        {
            var row = new Panel
            {
                Width = _scrollPanel.ClientSize.Width - 20,
                Height = 28,
                Margin = new Padding(0, 0, 0, 2)
            };

            var cb = new CheckBox
            {
                Text = doc.Filename,
                Checked = true,
                AutoSize = false,
                Width = row.Width - 100,
                Height = 24,
                Left = 4,
                Top = 2,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var countLabel = new Label
            {
                Text = $"({doc.CharCount:N0} ch)",
                Width = 90,
                Height = 24,
                Left = row.Width - 94,
                Top = 2,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.Gray
            };

            cb.CheckedChanged += (_, _) => UpdateMergeButton();

            row.Controls.Add(cb);
            row.Controls.Add(countLabel);
            _checkBoxes.Add(cb);
            _checklistPanel.Controls.Add(row);
        }

        // Resize rows when panel resizes
        _scrollPanel.Resize += (_, _) => ResizeRows();

        UpdateMergeButton();
    }

    private void ResizeRows()
    {
        int w = _scrollPanel.ClientSize.Width - 20;
        foreach (Control ctrl in _checklistPanel.Controls)
        {
            ctrl.Width = w;
            if (ctrl.Controls.Count >= 2)
            {
                ctrl.Controls[0].Width = w - 100;
                ctrl.Controls[1].Left = w - 94;
            }
        }
    }

    private void UpdateMergeButton()
    {
        _mergeButton.Enabled = _checkBoxes.Any(cb => cb.Checked);
    }

    private void OnToggleAll(object? sender, EventArgs e)
    {
        _allSelected = !_allSelected;
        foreach (var cb in _checkBoxes)
            cb.Checked = _allSelected;
        _toggleAllButton.Text = _allSelected ? "Deselect All" : "Select All";
    }

    private void OnSetupShortcutClicked(object? sender, EventArgs e)
    {
        // Build a small inline dialog to pick a Ctrl+Alt+? key
        using var dlg = new Form
        {
            Text = "Set Keyboard Shortcut",
            Size = new Size(340, 160),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9f)
        };

        var lbl = new Label
        {
            Text = "Launch with:  Ctrl + Alt +",
            Left = 16, Top = 20, Width = 170, Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var keys = Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
            .Concat(Enumerable.Range('0', 10).Select(c => ((char)c).ToString()))
            .ToArray();

        var combo = new ComboBox
        {
            Left = 190, Top = 18, Width = 60,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.Items.AddRange(keys);
        combo.SelectedItem = "N";

        var ok = new Button { Text = "Create Shortcut", Left = 60, Top = 68, Width = 120, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 190, Top = 68, Width = 70, DialogResult = DialogResult.Cancel };
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        dlg.Controls.AddRange([lbl, combo, ok, cancel]);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string key = combo.SelectedItem?.ToString() ?? "N";
        CreateStartMenuShortcut(key);
    }

    private static void CreateStartMenuShortcut(string key)
    {
        string exePath = Application.ExecutablePath;
        string lnkPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "NoteStitch.lnk");

        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell not available.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = exePath;
            shortcut.HotKey = $"Ctrl+Alt+{key}";
            shortcut.Description = "NoteStitch";

            string icoPath = IconHelper.EnsureIcoFile();
            if (!string.IsNullOrEmpty(icoPath))
                shortcut.IconLocation = $"{icoPath},0";

            shortcut.Save();

            MessageBox.Show(
                $"Shortcut created.\n\nHotkey:  Ctrl + Alt + {key}\n\nNote: Log off and back on (or restart) for Windows to register the hotkey.",
                "Shortcut Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create shortcut:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnMergeClicked(object? sender, EventArgs e)
    {
        // Remember which filenames are checked before refreshing
        var checkedFilenames = _docs
            .Where((doc, i) => i < _checkBoxes.Count && _checkBoxes[i].Checked)
            .Select(doc => doc.Filename)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Refresh to capture latest content, then restore checked state
        RefreshNotepads();

        for (int i = 0; i < _docs.Count && i < _checkBoxes.Count; i++)
            _checkBoxes[i].Checked = checkedFilenames.Contains(_docs[i].Filename);

        var selected = _docs
            .Where((doc, i) => i < _checkBoxes.Count && _checkBoxes[i].Checked)
            .ToList();

        if (selected.Count == 0) return;

        var resultForm = new ResultForm(selected);
        resultForm.ShowDialog(this);
    }
}
