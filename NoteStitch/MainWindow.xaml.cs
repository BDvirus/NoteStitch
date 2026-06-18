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
using System.Runtime.InteropServices;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using WinRT.Interop;

namespace NoteStitch;

public partial class MainWindow : Window
{
    private static TaskbarIcon? s_trayIcon;

    private bool _isExitRequested;
    private bool _launchToTray;
    private nint _hwnd;
    private nint _originalWndProc;
    private WndProcDelegate? _wndProc;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.Resize(new SizeInt32(980, 680));
        ApplyWindowIcon();
        ApplyTitleBarStyling();
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnClosed;
        Activated += OnFirstActivated;
        ((FrameworkElement)Content).ActualThemeChanged += OnActualThemeChanged;

        RootFrame.Navigate(typeof(HomePage));
    }

    public void HandleLaunchArguments(IEnumerable<string> args)
    {
        _launchToTray = args.Contains("/tray", StringComparer.OrdinalIgnoreCase);

        if (args.Contains("/autosave", StringComparer.OrdinalIgnoreCase))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowMainWindow();
                TriggerAutoSave();
            });
        }
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;

        _hwnd = WindowNative.GetWindowHandle(this);
        HookWindowMessages();
        InitializeTrayIcon();

        if (_launchToTray)
        {
            DispatcherQueue.TryEnqueue(HideToTray);
        }
    }

    private void InitializeTrayIcon()
    {
        var openCommand = new XamlUICommand
        {
            Label = "Open NoteStitch",
            Description = "Show the NoteStitch window."
        };
        openCommand.ExecuteRequested += (_, _) => ShowMainWindow();

        var autoSaveCommand = new XamlUICommand
        {
            Label = "Auto Save && Close",
            Description = "Merge the selected notes into the auto-save folder."
        };
        autoSaveCommand.ExecuteRequested += (_, _) =>
        {
            ShowMainWindow();
            TriggerAutoSave();
        };

        var exitCommand = new XamlUICommand
        {
            Label = "Exit",
            Description = "Quit NoteStitch."
        };
        exitCommand.ExecuteRequested += (_, _) => ExitApplication();

        var flyout = new MenuFlyout
        {
            AreOpenCloseAnimationsEnabled = false
        };
        flyout.Items.Add(new MenuFlyoutItem { Command = openCommand });
        flyout.Items.Add(new MenuFlyoutItem { Command = autoSaveCommand });
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(new MenuFlyoutItem { Command = exitCommand });

        s_trayIcon?.Dispose();
        s_trayIcon = new TaskbarIcon
        {
            ToolTipText = $"NoteStitch v{UpdateChecker.CurrentVersion.ToString(3)}",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/icon.ico")),
            ContextFlyout = flyout,
            LeftClickCommand = openCommand,
            NoLeftClickDelay = true,
            Visibility = Visibility.Visible
        };
        s_trayIcon.ForceCreate();
    }

    private void ApplyWindowIcon()
    {
        string iconPath = IconHelper.EnsureIcoFile();
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void ApplyTitleBarStyling()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0);

        bool isDark = ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;
        titleBar.ButtonForegroundColor = isDark
            ? Microsoft.UI.ColorHelper.FromArgb(255, 243, 244, 246)
            : Microsoft.UI.ColorHelper.FromArgb(255, 31, 41, 31);
        titleBar.ButtonHoverForegroundColor = titleBar.ButtonForegroundColor;
        titleBar.ButtonPressedForegroundColor = titleBar.ButtonForegroundColor;
        titleBar.ButtonInactiveForegroundColor = isDark
            ? Microsoft.UI.ColorHelper.FromArgb(255, 148, 163, 184)
            : Microsoft.UI.ColorHelper.FromArgb(255, 113, 125, 113);
        titleBar.ButtonHoverBackgroundColor = isDark
            ? Microsoft.UI.ColorHelper.FromArgb(48, 255, 255, 255)
            : Microsoft.UI.ColorHelper.FromArgb(32, 70, 97, 74);
        titleBar.ButtonPressedBackgroundColor = isDark
            ? Microsoft.UI.ColorHelper.FromArgb(72, 255, 255, 255)
            : Microsoft.UI.ColorHelper.FromArgb(56, 70, 97, 74);

        if (AppIconBadge.Background is SolidColorBrush accentBrush)
        {
            TitleText.Foreground = new SolidColorBrush(titleBar.ButtonForegroundColor ?? accentBrush.Color);
        }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyTitleBarStyling();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExitRequested)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        s_trayIcon?.Dispose();
        s_trayIcon = null;

        if (_hwnd != 0 && _originalWndProc != 0)
        {
            SetWindowLongPtr(_hwnd, GWL_WNDPROC, _originalWndProc);
            _originalWndProc = 0;
        }
    }

    private void HideToTray()
    {
        this.Hide();
    }

    private void ShowMainWindow()
    {
        this.Show();
        ShowWindow(_hwnd, SW_RESTORE);
        Activate();
        SetForegroundWindow(_hwnd);
    }

    private void TriggerAutoSave()
    {
        if (RootFrame.Content is HomePage homePage)
        {
            homePage.TriggerAutoSave();
        }
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        s_trayIcon?.Dispose();
        s_trayIcon = null;
        Close();
    }

    private void HookWindowMessages()
    {
        if (_hwnd == 0 || _originalWndProc != 0)
        {
            return;
        }

        _wndProc = WindowProc;
        _originalWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == Program.WM_NOTESTITCH_ACTIVATE)
        {
            DispatcherQueue.TryEnqueue(ShowMainWindow);
            return 0;
        }

        if (msg == Program.WM_NOTESTITCH_AUTOSAVE)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowMainWindow();
                TriggerAutoSave();
            });
            return 0;
        }

        return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem selectedItem)
        {
            return;
        }

        switch (selectedItem.Tag)
        {
            case "home":
                if (RootFrame.Content is not HomePage)
                {
                    RootFrame.Navigate(typeof(HomePage));
                }
                break;
            case "settings":
                _ = ShowSettingsAsync();
                break;
            case "about":
                if (RootFrame.Content is not AboutPage)
                {
                    RootFrame.Navigate(typeof(AboutPage));
                }
                break;
        }
    }

    private async Task ShowSettingsAsync()
    {
        var settings = AppSettings.Load();
        var tcs = new TaskCompletionSource<bool>();
        RootFrame.Navigate(typeof(SettingsPage), (settings, tcs));

        bool saved = await tcs.Task;
        if (saved)
        {
            var err = settings.Save();
            var startupErr = StartupManager.SetEnabled(settings.RunOnWindowsStartup);
            if (err is not null || startupErr is not null)
            {
                var parts = new List<string>();
                if (err is not null)
                {
                    parts.Add($"Settings could not be saved:\n{err}");
                }

                if (startupErr is not null)
                {
                    parts.Add($"Windows startup could not be updated:\n{startupErr}");
                }

                await Updater.ShowDialogAsync(App.MainWindow!, "Warning",
                    string.Join("\n\n", parts), "OK", "");
            }

            if (RootFrame.Content is HomePage homePage)
            {
                homePage.ReloadSettings();
            }
        }

        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, "home", StringComparison.Ordinal));
    }

    private const int GWL_WNDPROC = -4;
    private const int SW_RESTORE = 9;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
