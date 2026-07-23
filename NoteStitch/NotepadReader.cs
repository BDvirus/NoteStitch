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

using System.Runtime.InteropServices;
using System.Text;

namespace NoteStitch;

public class NotepadDoc
{
    public IntPtr Hwnd       { get; set; }
    public int    ProcessId  { get; set; }
    public string Filename   { get; set; } = string.Empty;
    public string Text       { get; set; } = string.Empty;
    /// <summary>Path to the Win11 tabstate .bin file, if applicable.</summary>
    public string SourceFile { get; set; } = string.Empty;
    public int CharCount => Text.Length - Text.Count(c => c == '\r');
}

public static class NotepadReader
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwnd, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    private const uint WM_GETTEXT = 0x000D;
    private const uint WM_GETTEXTLENGTH = 0x000E;

    public static List<NotepadDoc> GetNotepadWindows()
    {
        // Windows 11 Store Notepad: read all tabs from state files (inactive tabs included)
        if (Win11NotepadReader.IsAvailable)
        {
            var win11Docs = Win11NotepadReader.GetAllTabs();
            if (win11Docs.Count > 0)
            {
                DeduplicateFilenames(win11Docs);
                return win11Docs;
            }
        }

        // Fallback: classic Win32 approach (reads active tab only)
        var docs = new List<NotepadDoc>();

        EnumWindows((hwnd, _) =>
        {
            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);

            if (cls.ToString() == "Notepad")
            {
                var titleBuf = new StringBuilder(512);
                GetWindowText(hwnd, titleBuf, titleBuf.Capacity);
                string filename = ParseTitle(titleBuf.ToString());
                string text = ReadEditText(hwnd);

                GetWindowThreadProcessId(hwnd, out uint pid);
                docs.Add(new NotepadDoc
                {
                    Hwnd      = hwnd,
                    ProcessId = (int)pid,
                    Filename  = filename,
                    Text      = text
                });
            }

            return true;
        }, IntPtr.Zero);

        DeduplicateFilenames(docs);
        return docs;
    }

    // Classic Notepad uses "Edit"; Windows 11 new Notepad uses "RichEditD2DPT"
    private static readonly HashSet<string> EditClasses =
        new(StringComparer.OrdinalIgnoreCase) { "Edit", "RichEditD2DPT" };

    private static string ReadEditText(IntPtr notepadHwnd)
    {
        IntPtr editHwnd = IntPtr.Zero;

        EnumChildWindows(notepadHwnd, (child, _) =>
        {
            var cls = new StringBuilder(64);
            GetClassName(child, cls, cls.Capacity);
            if (EditClasses.Contains(cls.ToString()))
            {
                editHwnd = child;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        if (editHwnd == IntPtr.Zero)
            return string.Empty;

        int length = (int)SendMessage(editHwnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0)
            return string.Empty;

        var buffer = new StringBuilder(length + 1);
        SendMessage(editHwnd, WM_GETTEXT, (IntPtr)(length + 1), buffer);
        return buffer.ToString();
    }

    private static string ParseTitle(string rawTitle)
    {
        const string suffix = " - Notepad";
        string name = rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? rawTitle[..^suffix.Length]
            : rawTitle;

        // Strip leading asterisk (unsaved changes marker)
        if (name.StartsWith('*'))
            name = name[1..];

        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
    }

    /// <param name="selectedDocs">Docs the user wants to close.</param>
    /// <param name="allDocs">All currently known docs — used to decide whether it is safe
    /// to kill a Win11 Notepad process (which hosts all tabs in one process).</param>
    public static void CloseNotepadWindows(
        IEnumerable<NotepadDoc> selectedDocs,
        IEnumerable<NotepadDoc> allDocs)
    {
        var selected = selectedDocs.ToList();
        var all      = allDocs.ToList();

        var allWin11 = all.Where(d => d.Hwnd == IntPtr.Zero).ToList();
        var selectedWin11 = selected.Where(d => d.Hwnd == IntPtr.Zero).ToList();
        var pids = new HashSet<int>();
        foreach (var doc in selected)
        {
            if (doc.Hwnd != IntPtr.Zero)
            {
                // Classic Win32: each window is independent — always safe to kill
                GetWindowThreadProcessId(doc.Hwnd, out uint pid);
                if (pid > 0) pids.Add((int)pid);
            }
        }

        foreach (int pid in pids)
        {
            try { System.Diagnostics.Process.GetProcessById(pid).Kill(); }
            catch { /* process may have already exited */ }
        }

        // Windows 11 Notepad keeps both per-tab files and a separate window/session index.
        // It is only safe to clear them when every known tab was selected.
        if (allWin11.Count > 0 && selectedWin11.Count >= allWin11.Count)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Notepad");
            var processIds = processes.Select(process => process.Id).ToArray();
            foreach (var process in processes) process.Dispose();

            Win11NotepadSessionCloser.CloseAll(
                processIds,
                allWin11.Select(doc => doc.SourceFile));
        }
    }

    private static void DeduplicateFilenames(List<NotepadDoc> docs)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            if (!seen.TryGetValue(doc.Filename, out int count))
            {
                seen[doc.Filename] = 1;
            }
            else
            {
                seen[doc.Filename] = count + 1;
                doc.Filename = $"{doc.Filename} ({count + 1})";
            }
        }
    }
}
