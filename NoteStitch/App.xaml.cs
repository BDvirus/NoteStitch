using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace NoteStitch;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    private readonly string[] _args;

    public App(string[] args)
    {
        _args = args;
        this.InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.HandleLaunchArguments(_args);
        MainWindow.Activate();

        // Background update check — fire once after window first activates
        TypedEventHandler<object, WindowActivatedEventArgs>? onActivated = null;
        onActivated = async (_, _) =>
        {
            MainWindow!.Activated -= onActivated!;
            try
            {
                var release = await UpdateChecker.GetLatestReleaseAsync();
                if (release is not null)
                    await Updater.PromptAndUpdateAsync(release, MainWindow);
            }
            catch { /* no network or API unavailable */ }
        };
        MainWindow.Activated += onActivated;
    }
}
