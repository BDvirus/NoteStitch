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
    private Button _autoMergeButton = null!;
    private AppSettings _settings = AppSettings.Load();
    private NotifyIcon _trayIcon = null!;
    private bool _trayHintShown = false;

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
        InstallTrayIcon();
        RefreshNotepads();
        InstallHook();
        EnsureStartMenuEntry();
    }

    private void InitializeComponent()
    {
        Text = $"NoteStitch v{UpdateChecker.CurrentVersion.ToString(3)}";
        Size = new Size(520, 420);
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
        _scrollPanel.Resize += (_, _) => ResizeRows();

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 6),
            WrapContents = false
        };

        _toggleAllButton  = new Button { Text = "Deselect All", Width = 90 };
        _mergeButton      = new Button { Text = "Merge  ▶", Width = 90, Enabled = false };
        _autoMergeButton  = new Button { Text = "⚡ Auto Save", Width = 95, Enabled = false };
        var shortcutButton = new Button { Text = "⌨ Shortcut…", Width = 100 };
        var settingsButton = new Button { Text = "⚙", Width = 32 };
        var aboutButton    = new Button { Text = "ℹ", Width = 32 };

        _toggleAllButton.Click  += OnToggleAll;
        _mergeButton.Click      += OnMergeClicked;
        _autoMergeButton.Click  += OnAutoMergeClicked;
        shortcutButton.Click    += OnSetupShortcutClicked;
        settingsButton.Click    += OnSettingsClicked;
        aboutButton.Click       += (_, _) => { using var f = new AboutForm(); f.ShowDialog(this); };

        buttonPanel.Controls.Add(_toggleAllButton);
        buttonPanel.Controls.Add(_mergeButton);
        buttonPanel.Controls.Add(_autoMergeButton);
        buttonPanel.Controls.Add(shortcutButton);
        buttonPanel.Controls.Add(settingsButton);
        buttonPanel.Controls.Add(aboutButton);

        // Layout (add in reverse dock order)
        Controls.Add(_scrollPanel);
        Controls.Add(_infoLabel);
        Controls.Add(titleLabel);
        Controls.Add(buttonPanel);

        Padding = new Padding(8, 0, 8, 0);
    }

    // ── Tray ──────────────────────────────────────────────────────────────────

    private void InstallTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open NoteStitch",  null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Merge  ▶",         null, (_, _) => { ShowFromTray(); OnMergeClicked(null, EventArgs.Empty); });
        menu.Items.Add("⚡ Auto Save",      null, (_, _) => { ShowFromTray(); OnAutoMergeClicked(null, EventArgs.Empty); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙ Settings",       null, (_, _) => { ShowFromTray(); OnSettingsClicked(null, EventArgs.Empty); });
        menu.Items.Add("⌨ Shortcut…",      null, (_, _) => { ShowFromTray(); OnSetupShortcutClicked(null, EventArgs.Empty); });
        menu.Items.Add("📌 Add to Start Menu", null, (_, _) => AddToStartMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About",                null, (_, _) => { using var f = new AboutForm(); f.ShowDialog(this); });
        menu.Items.Add("Check for Updates…",   null, async (_, _) => await CheckForUpdatesManualAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",             null, (_, _) => { _trayIcon.Visible = false; Application.Exit(); });

        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "NoteStitch",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
            MinimizeToTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }
        base.OnFormClosing(e);
    }

    private void MinimizeToTray()
    {
        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            _trayIcon.ShowBalloonTip(2500, "NoteStitch", "NoteStitch is minimized to the system tray.\nDouble-click the icon to reopen.", ToolTipIcon.Info);
        }
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

        BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
    }

    private async Task CheckForUpdatesManualAsync()
    {
        try
        {
            var release = await UpdateChecker.GetLatestReleaseAsync();
            if (release is null)
            {
                MessageBox.Show($"You are up to date (v{UpdateChecker.CurrentVersion.ToString(3)}).",
                    "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await Updater.PromptAndUpdateAsync(release, this);
        }
        catch
        {
            MessageBox.Show("Could not reach GitHub. Check your internet connection.",
                "Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnhookWinEvent(_hookShowDestroy);
        UnhookWinEvent(_hookNameChange);
        UnhookWinEvent(_hookValueChange);
        _debounce.Dispose();
        _trayIcon.Dispose();
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
            _mergeButton.Enabled     = false;
            _autoMergeButton.Enabled = false;
            _toggleAllButton.Enabled = false;
            return;
        }

        _toggleAllButton.Enabled = true;

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
        bool any = _checkBoxes.Any(cb => cb.Checked);
        _mergeButton.Enabled     = any;
        _autoMergeButton.Enabled = any && !string.IsNullOrEmpty(_settings.AutoSaveFolder);
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

    // hotkey: e.g. "Ctrl+Alt+N", or null for no hotkey
    private static void WriteLnk(string lnkPath, string? hotkey = null)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell not available.");
        dynamic shell    = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath  = Application.ExecutablePath;
        shortcut.Description = "NoteStitch";
        if (hotkey is not null) shortcut.HotKey = hotkey;

        string icoPath = IconHelper.EnsureIcoFile();
        if (!string.IsNullOrEmpty(icoPath))
            shortcut.IconLocation = $"{icoPath},0";

        shortcut.Save();
    }

    private static string StartMenuLnkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        "NoteStitch.lnk");

    private static void EnsureStartMenuEntry()
    {
        try
        {
            if (!File.Exists(StartMenuLnkPath))
                WriteLnk(StartMenuLnkPath);
        }
        catch { }
    }

    private static void AddToStartMenu()
    {
        try
        {
            WriteLnk(StartMenuLnkPath);
            MessageBox.Show("NoteStitch has been added to the Start Menu.",
                "Start Menu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add to Start Menu:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CreateStartMenuShortcut(string key)
    {
        try
        {
            WriteLnk(StartMenuLnkPath, $"Ctrl+Alt+{key}");
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

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var dlg = new Form
        {
            Text = "Settings",
            Size = new Size(460, 140),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9f)
        };

        var lbl = new Label
        {
            Text = "Auto-save folder:",
            Left = 12, Top = 22, Width = 110, Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var folderBox = new TextBox
        {
            Left = 125, Top = 20, Width = 220, Height = 24,
            Text = _settings.AutoSaveFolder
        };

        var browseBtn = new Button { Text = "Browse…", Left = 352, Top = 19, Width = 76 };
        browseBtn.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select auto-save folder",
                SelectedPath = folderBox.Text
            };
            if (fbd.ShowDialog() == DialogResult.OK)
                folderBox.Text = fbd.SelectedPath;
        };

        var ok     = new Button { Text = "Save", Left = 260, Top = 62, Width = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 348, Top = 62, Width = 80, DialogResult = DialogResult.Cancel };
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        dlg.Controls.AddRange([lbl, folderBox, browseBtn, ok, cancel]);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _settings.AutoSaveFolder = folderBox.Text.Trim();
        var err = _settings.Save();
        if (err is not null)
            MessageBox.Show($"Settings could not be saved:\n{err}", "Warning",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        UpdateMergeButton();
    }

    private void OnAutoMergeClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_settings.AutoSaveFolder))
        {
            MessageBox.Show("No auto-save folder configured.\nClick ⚙ to set one.",
                "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var checkedFilenames = _docs
            .Where((doc, i) => i < _checkBoxes.Count && _checkBoxes[i].Checked)
            .Select(doc => doc.Filename)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RefreshNotepads();

        for (int i = 0; i < _docs.Count && i < _checkBoxes.Count; i++)
            _checkBoxes[i].Checked = checkedFilenames.Contains(_docs[i].Filename);

        var selected = _docs
            .Where((doc, i) => i < _checkBoxes.Count && _checkBoxes[i].Checked)
            .ToList();

        if (selected.Count == 0) return;

        try
        {
            Directory.CreateDirectory(_settings.AutoSaveFolder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(_settings.AutoSaveFolder, $"merged_notepads_{timestamp}.txt");

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                sb.AppendLine($"=== {selected[i].Filename} ===");
                sb.AppendLine();
                sb.AppendLine(string.IsNullOrEmpty(selected[i].Text) ? "(empty document)" : selected[i].Text);
                if (i < selected.Count - 1) sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            NotepadReader.CloseNotepadWindows(selected);
            RefreshNotepads();

            MessageBox.Show($"Saved to:\n{filePath}\n\nAll Notepad windows have been closed.",
                "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save:\n{ex.Message}", "Error",
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

        using var resultForm = new ResultForm(selected);
        resultForm.ShowDialog(this);
    }
}
