using System.Windows.Forms;

namespace NoteStitch;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new System.Threading.Mutex(true, "NoteStitch_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("NoteStitch is already running.\nCheck the system tray.",
                "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        var form = new MainForm();

        // Background update check — runs after the UI is up
        form.Shown += async (_, _) =>
        {
            try
            {
                var release = await UpdateChecker.GetLatestReleaseAsync();
                if (release is not null)
                    await Updater.PromptAndUpdateAsync(release, form);
            }
            catch { /* no network or API unavailable — silently ignore */ }
        };

        Application.Run(form);
    }
}
