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

using System.Diagnostics;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NoteStitch;

internal static class Updater
{
    public static async Task PromptAndUpdateAsync(ReleaseInfo release, Window owner)
    {
        var result = await ShowDialogAsync(owner,
            "Update Available",
            $"NoteStitch {release.TagName} is available.\n" +
            $"You are running v{UpdateChecker.CurrentVersion.ToString(3)}.\n\n" +
            "Update now?",
            primaryBtn: "Yes", closeBtn: "No");

        if (result != ContentDialogResult.Primary) return;

        if (release.HasDirectDownload)
            await DownloadAndReplaceAsync(release.DownloadUrl, owner);
        else
        {
            if (!release.HasAsset)
                await ShowDialogAsync(owner,
                    "No Asset Found",
                    $"No downloadable asset was found for {release.TagName}.\nOpening the releases page instead.",
                    primaryBtn: "OK", closeBtn: "");

            Process.Start(new ProcessStartInfo(release.ReleasePage) { UseShellExecute = true });
        }
    }

    private static async Task DownloadAndReplaceAsync(string downloadUrl, Window owner)
    {
        string exePath  = Process.GetCurrentProcess().MainModule!.FileName;
        string tempPath = exePath + ".new";
        string batPath  = Path.Combine(Path.GetTempPath(), "notestitch_update.bat");

        // Show progress dialog
        var progressDlg = new ContentDialog
        {
            Title   = "Updating NoteStitch…",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                    new TextBlock   { Text = "Downloading update, please wait…" }
                }
            },
            XamlRoot = owner.Content.XamlRoot
        };

        _ = progressDlg.ShowAsync(); // fire-and-forget; we close it manually

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NoteStitch-Updater");
            client.Timeout = TimeSpan.FromMinutes(3);

            await using var src  = await client.GetStreamAsync(downloadUrl);
            await using var dest = File.Create(tempPath);

            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
                await dest.WriteAsync(buffer.AsMemory(0, read));
        }
        catch (Exception ex)
        {
            progressDlg.Hide();
            await ShowDialogAsync(owner, "Update Error",
                $"Download failed:\n{ex.Message}", primaryBtn: "OK", closeBtn: "");
            return;
        }

        progressDlg.Hide();

        // Write self-replacing bat: wait for this process to exit, swap files, restart
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
            WindowStyle    = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    internal static async Task<ContentDialogResult> ShowDialogAsync(
        Window owner, string title, string content,
        string primaryBtn, string closeBtn)
    {
        var dlg = new ContentDialog
        {
            Title           = title,
            Content         = new TextBlock { Text = content, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
            XamlRoot        = owner.Content.XamlRoot
        };

        if (!string.IsNullOrEmpty(primaryBtn)) dlg.PrimaryButtonText = primaryBtn;
        if (!string.IsNullOrEmpty(closeBtn))   dlg.CloseButtonText   = closeBtn;
        if (!string.IsNullOrEmpty(primaryBtn)) dlg.DefaultButton     = ContentDialogButton.Primary;

        return await dlg.ShowAsync();
    }
}
