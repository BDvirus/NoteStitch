using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NoteStitch;

public sealed partial class HomePage : Page
{
    private readonly ObservableCollection<NotepadDocVm> _items = new();
    private List<NotepadDoc> _docs = new();
    private bool _allSelected = true;
    private AppSettings _settings = AppSettings.Load();


    // WinEvent hook
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
    private const uint EVENT_OBJECT_VALUECHANGE = 0x800E;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint GA_ROOT = 2;

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

    private WinEventDelegate _winEventProc = null!;
    private IntPtr _hookShowDestroy = IntPtr.Zero;
    private IntPtr _hookNameChange = IntPtr.Zero;
    private IntPtr _hookValueChange = IntPtr.Zero;
    private DispatcherTimer _debounce = null!;
    private FileSystemWatcher? _tabStateWatcher;
    private DispatcherQueue _dq = null!;


    public HomePage()
    {
        this.InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        _dq = DispatcherQueue.GetForCurrentThread();
        DocList.ItemsSource = _items;
        Loaded += OnLoaded;
    }


    private bool _initialized;
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        InstallHook();
        RefreshNotepads();
    }

    public void TriggerAutoSave() => _dq.TryEnqueue(() => OnAutoMergeClicked(null, null));
    public void ReloadSettings()
    {
        _settings = AppSettings.Load();
        RefreshNotepads();
    }

    // ── WinEvent hooks ───────────────────────────────────────────────────────

    private void InstallHook()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RefreshNotepads(); };

        _winEventProc = OnWinEvent;

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

        if (Directory.Exists(Win11NotepadReader.TabStateFolder))
        {
            _tabStateWatcher = new FileSystemWatcher(Win11NotepadReader.TabStateFolder, "*.bin")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            void onChanged(object _, FileSystemEventArgs __) =>
                _dq.TryEnqueue(() => { _debounce.Stop(); _debounce.Start(); });
            _tabStateWatcher.Changed += onChanged;
            _tabStateWatcher.Created += onChanged;
            _tabStateWatcher.Deleted += onChanged;
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;

        IntPtr root = eventType == EVENT_OBJECT_VALUECHANGE
            ? GetAncestor(hwnd, GA_ROOT)
            : (idObject == 0 ? hwnd : IntPtr.Zero);

        if (root == IntPtr.Zero) return;

        var cls = new StringBuilder(64);
        GetClassName(root, cls, cls.Capacity);
        if (cls.ToString() != "Notepad") return;

        _dq.TryEnqueue(() => { _debounce.Stop(); _debounce.Start(); });
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void RefreshNotepads()
    {
        var all = NotepadReader.GetNotepadWindows();

        if (!_settings.IncludeSavedFiles)
            all = all.Where(d => d.Filename == "Untitled" ||
                                 d.Filename.StartsWith("Untitled (", StringComparison.Ordinal)).ToList();

        if (!_settings.IncludeMergedFiles)
            all = all.Where(d => !d.Filename.StartsWith("merged_notepads_", StringComparison.OrdinalIgnoreCase)).ToList();

        _docs = all;

        // Preserve checked state by filename
        var checkedNames = _items.Where(v => v.IsChecked)
                                 .Select(v => v.Filename)
                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _items.Clear();
        foreach (var doc in _docs)
        {
            var vm = new NotepadDocVm(doc);
            vm.IsChecked = checkedNames.Count == 0 || checkedNames.Contains(doc.Filename);
            vm.PropertyChanged += (_, _) => UpdateMergeButton();
            _items.Add(vm);
        }

        if (_docs.Count == 0)
        {
            InfoLabel.Text = "No Notepad windows found. Open Notepad to begin.";
            InfoLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.OrangeRed);
        }
        else
        {
            InfoLabel.Text = $"{_docs.Count} Notepad window{(_docs.Count == 1 ? "" : "s")} found";
            InfoLabel.ClearValue(TextBlock.ForegroundProperty);
        }

        _allSelected = true;
        ToggleAllButton.Content = "Deselect All";
        UpdateMergeButton();
    }

    private void UpdateMergeButton()
    {
        bool any = _items.Any(v => v.IsChecked);
        MergeButton.IsEnabled = any;
        AutoMergeButton.IsEnabled = any && !string.IsNullOrEmpty(_settings.AutoSaveFolder);
        ToggleAllButton.IsEnabled = _items.Count > 0;
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void OnToggleAll(object sender, RoutedEventArgs e)
    {
        _allSelected = !_allSelected;
        foreach (var vm in _items)
            vm.IsChecked = _allSelected;
        ToggleAllButton.Content = _allSelected ? "Deselect All" : "Select All";
    }

    private async void OnMergeClicked(object? sender, RoutedEventArgs? e)
    {
        var selectedNames = _items.Where(v => v.IsChecked)
                                  .Select(v => v.Filename)
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RefreshNotepads();
        foreach (var vm in _items)
            vm.IsChecked = selectedNames.Contains(vm.Filename);

        var selected = _items.Where(v => v.IsChecked).Select(v => v.Doc).ToList();
        if (selected.Count == 0) return;

        var result = new ResultWindow(selected);
        result.AppWindow.Resize(new Windows.Graphics.SizeInt32(620, 560));
        result.Activate();
    }

    private async void OnAutoMergeClicked(object? sender, RoutedEventArgs? e)
    {
        if (string.IsNullOrEmpty(_settings.AutoSaveFolder))
        {
            await Updater.ShowDialogAsync(App.MainWindow!, "Not Configured",
                "No auto-save folder configured.\nClick ⚙ to set one.", "OK", "");
            return;
        }

        var selectedNames = _items.Where(v => v.IsChecked)
                                  .Select(v => v.Filename)
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RefreshNotepads();
        foreach (var vm in _items)
            vm.IsChecked = selectedNames.Contains(vm.Filename);

        var selected = _items.Where(v => v.IsChecked).Select(v => v.Doc).ToList();
        if (selected.Count == 0) return;

        try
        {
            Directory.CreateDirectory(_settings.AutoSaveFolder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(_settings.AutoSaveFolder, $"merged_notepads_{timestamp}.txt");

            var sb = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                sb.AppendLine($"=== {selected[i].Filename} ===");
                sb.AppendLine();
                sb.AppendLine(string.IsNullOrEmpty(selected[i].Text) ? "(empty document)" : selected[i].Text);
                if (i < selected.Count - 1) sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            NotepadReader.CloseNotepadWindows(selected, _docs);
            RefreshNotepads();

            bool allSelected = selected.Count == _docs.Count || _docs.Count == 0;
            string closedMsg = allSelected
                ? "All Notepad windows have been closed."
                : "Note: Notepad was kept open because only some tabs were selected.";

            await Updater.ShowDialogAsync(App.MainWindow!, "Done",
                $"Saved to:\n{filePath}\n\n{closedMsg}", "OK", "");
        }
        catch (Exception ex)
        {
            await Updater.ShowDialogAsync(App.MainWindow!, "Error", $"Failed to save:\n{ex.Message}", "OK", "");
        }
    }

    private void OnShortcutClicked(object? sender, RoutedEventArgs? e) =>
        _ = ShowShortcutDialogAsync();

    // ── Dialogs ──────────────────────────────────────────────────────────────

    private async Task ShowShortcutDialogAsync()
    {
        var tcs = new TaskCompletionSource<(string key, bool isAutoSave)?>();
        this.Frame.Navigate(typeof(ShortcutPage), (_settings.AutoSaveFolder, tcs));
        var result = await tcs.Task;
        if (result is null) return;

        string key = result.Value.key;
        string? arg = result.Value.isAutoSave ? "/autosave" : null;
        CreateStartMenuShortcut(key, arg);
    }

    private async Task CheckForUpdatesManualAsync()
    {
        try
        {
            var release = await UpdateChecker.GetLatestReleaseAsync();
            if (release is null)
            {
                await Updater.ShowDialogAsync(App.MainWindow!, "No Updates",
                    $"You are up to date (v{UpdateChecker.CurrentVersion.ToString(3)}).", "OK", "");
                return;
            }
            await Updater.PromptAndUpdateAsync(release, App.MainWindow!);
        }
        catch
        {
            await Updater.ShowDialogAsync(App.MainWindow!, "Update Check Failed",
                "Could not reach GitHub. Check your internet connection.", "OK", "");
        }
    }

    // ── Start Menu shortcut ──────────────────────────────────────────────────

    private static string StartMenuLnkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        "NoteStitch.lnk");

    private static void WriteLnk(string lnkPath, string? hotkey = null, string? arguments = null)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
        shortcut.Description = "NoteStitch";
        if (hotkey is not null) shortcut.HotKey = hotkey;
        if (arguments is not null) shortcut.Arguments = arguments;

        string icoPath = IconHelper.EnsureIcoFile();
        if (!string.IsNullOrEmpty(icoPath))
            shortcut.IconLocation = $"{icoPath},0";

        shortcut.Save();
    }

    private static void EnsureStartMenuEntry()
    {
        try { if (!File.Exists(StartMenuLnkPath)) WriteLnk(StartMenuLnkPath); }
        catch { }
    }

    private async void AddToStartMenu()
    {
        try
        {
            WriteLnk(StartMenuLnkPath);
            await Updater.ShowDialogAsync(App.MainWindow!, "Start Menu",
                "NoteStitch has been added to the Start Menu.", "OK", "");
        }
        catch (Exception ex)
        {
            await Updater.ShowDialogAsync(App.MainWindow!, "Error",
                $"Failed to add to Start Menu:\n{ex.Message}", "OK", "");
        }
    }

    private async void CreateStartMenuShortcut(string key, string? arguments = null)
    {
        try
        {
            WriteLnk(StartMenuLnkPath, $"Ctrl+Alt+{key}", arguments);
            string action = arguments == "/autosave" ? "⚡ Auto Save & Close" : "Open NoteStitch";
            await Updater.ShowDialogAsync(App.MainWindow!, "Shortcut Created",
                $"Shortcut created.\n\nHotkey:  Ctrl + Alt + {key}\nAction:  {action}\n\n" +
                "Note: Log off and back on (or restart) for Windows to register the hotkey.",
                "OK", "");
        }
        catch (Exception ex)
        {
            await Updater.ShowDialogAsync(App.MainWindow!, "Error",
                $"Failed to create shortcut:\n{ex.Message}", "OK", "");
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    private void Cleanup()
    {
        UnhookWinEvent(_hookShowDestroy);
        UnhookWinEvent(_hookNameChange);
        UnhookWinEvent(_hookValueChange);
        _tabStateWatcher?.Dispose();
        _debounce?.Stop();
    }





}
