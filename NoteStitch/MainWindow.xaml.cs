using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NoteStitch;
using SystemTray.Core;

namespace SystemTrayWinUI3
{
    public partial class MainWindow : Window
    {

        private SystemTrayManager systemTrayManager;

        public MainWindow()
        {
            this.InitializeComponent();

            var helper = new WindowHelper(this);
            systemTrayManager = new SystemTrayManager(helper)
            {
                OpenSettingsAction = () => NavigateToSettings(),
                IsIconVisible = true,
                IconToolTip = "SystemTrayWinUI3",
                CloseButtonMinimizesToTray = false,
                LanguageCode = "en-US"
            };

            Closed += (_, _) => systemTrayManager?.Dispose();

            RootFrame.Navigate(typeof(HomePage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateToSettings();
                return;
            }

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag)
                {
                    case "home":
                        RootFrame.Navigate(typeof(HomePage));
                        break;
                    case "about":
                        RootFrame.Navigate(typeof(AboutPage));
                        break;
                }
            }
        }

        public void NavigateToSettings()
        {
            if (systemTrayManager != null)
            {
                RootFrame.Navigate(typeof(SettingsPage), systemTrayManager);
            }
        }

        //private readonly WindowHelper _helper;
        //private readonly SystemTrayManager _systemTrayManager;

        //public MainWindow()
        //{
        //    this.InitializeComponent();

        //    _helper = new WindowHelper(this)
        //    {
        //        CloseButtonMinimizesToTray = true
        //    };
        //    _systemTrayManager = new SystemTrayManager(_helper)
        //    {
        //        IsIconVisible = true,
        //        IconToolTip = $"NoteStitch v{UpdateChecker.CurrentVersion.ToString(3)}",
        //        MinimizeToTray = true,
        //        CloseButtonMinimizesToTray = true,
        //        LanguageCode = "en-US"
        //    };

        //    //_helper.Message += (msg, _, _) =>
        //    //{
        //    //    if (msg == Program.WM_NOTESTITCH_ACTIVATE)
        //    //        DispatcherQueue.TryEnqueue(() => _helper.ShowWindowFromTray());
        //    //    else if (msg == Program.WM_NOTESTITCH_AUTOSAVE)
        //    //        DispatcherQueue.TryEnqueue(() =>
        //    //        {
        //    //            _helper.ShowWindowFromTray();
        //    //            (RootFrame.Content as HomePage)?.TriggerAutoSave();
        //    //        });
        //    //};

        //    Closed += (_, _) =>
        //    {
        //        _systemTrayManager?.Dispose();
        //        _helper?.Dispose();
        //    };

        //    AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 420));
        //    RootFrame.Navigate(typeof(HomePage));
        //}
    }
}
