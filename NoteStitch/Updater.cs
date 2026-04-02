using System.Diagnostics;
using System.Net.Http;
using System.Windows.Forms;

namespace NoteStitch;

internal static class Updater
{
    public static async Task PromptAndUpdateAsync(ReleaseInfo release, IWin32Window owner)
    {
        var result = MessageBox.Show(
            $"NoteStitch {release.TagName} is available.\n" +
            $"You are running v{UpdateChecker.CurrentVersion.ToString(3)}.\n\n" +
            "Update now?",
            "Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes) return;

        if (release.HasDirectDownload)
        {
            await DownloadAndReplaceAsync(release.DownloadUrl, owner);
        }
        else
        {
            if (!release.HasAsset)
                MessageBox.Show(
                    $"No downloadable asset was found for {release.TagName}.\nOpening the releases page instead.",
                    "No Asset Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Process.Start(new ProcessStartInfo(release.ReleasePage) { UseShellExecute = true });
        }
    }

    private static async Task DownloadAndReplaceAsync(string downloadUrl, IWin32Window owner)
    {
        string exePath  = Application.ExecutablePath;
        string tempPath = exePath + ".new";
        string batPath  = Path.Combine(Path.GetTempPath(), "notestitch_update.bat");

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NoteStitch-Updater");
            client.Timeout = TimeSpan.FromMinutes(3);

            using var progress = new DownloadProgressForm();
            progress.Show(owner);
            Application.DoEvents();

            await using var src  = await client.GetStreamAsync(downloadUrl);
            await using var dest = File.Create(tempPath);

            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
                await dest.WriteAsync(buffer.AsMemory(0, read));

            progress.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed:\n{ex.Message}", "Update Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Write a self-deleting bat: wait for this process to exit, swap files, restart
        int pid = Environment.ProcessId;
        File.WriteAllText(batPath,
            $"""
            @echo off
            :wait
            tasklist /fi "PID eq {pid}" 2>nul | find "{pid}" >nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak >nul
                goto wait
            )
            move /y "{tempPath}" "{exePath}"
            start "" "{exePath}"
            del "%~f0"
            """);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"")
        {
            WindowStyle     = ProcessWindowStyle.Hidden,
            CreateNoWindow  = true,
            UseShellExecute = false
        });

        Application.Exit();
    }
}

// Minimal progress indicator shown during download
internal class DownloadProgressForm : Form
{
    public DownloadProgressForm()
    {
        Text            = "Updating NoteStitch…";
        Size            = new System.Drawing.Size(320, 90);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        ControlBox      = false;
        Font            = new System.Drawing.Font("Segoe UI", 9f);

        Controls.Add(new Label
        {
            Text      = "Downloading update, please wait…",
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        });
    }
}
