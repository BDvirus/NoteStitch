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
