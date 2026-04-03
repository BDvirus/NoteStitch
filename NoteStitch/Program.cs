using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NoteStitch;

static class Program
{
    internal static readonly uint WM_NOTESTITCH_ACTIVATE  =
        RegisterWindowMessage("WM_NOTESTITCH_ACTIVATE_2024");
    internal static readonly uint WM_NOTESTITCH_AUTOSAVE  =
        RegisterWindowMessage("WM_NOTESTITCH_AUTOSAVE_2024");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int HWND_BROADCAST = 0xFFFF;

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new System.Threading.Mutex(true, "NoteStitch_SingleInstance", out bool isNew);
        if (!isNew)
        {
            bool autoSave = args.Contains("/autosave", StringComparer.OrdinalIgnoreCase);
            uint msg = autoSave ? WM_NOTESTITCH_AUTOSAVE : WM_NOTESTITCH_ACTIVATE;
            PostMessage((IntPtr)HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
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
