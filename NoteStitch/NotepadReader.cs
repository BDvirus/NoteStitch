using System.Runtime.InteropServices;
using System.Text;

namespace NoteStitch;

public class NotepadDoc
{
    public IntPtr Hwnd { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
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

                docs.Add(new NotepadDoc
                {
                    Hwnd = hwnd,
                    Filename = filename,
                    Text = text
                });
            }

            return true;
        }, IntPtr.Zero);

        DeduplicateFilenames(docs);
        return docs;
    }

    private static string ReadEditText(IntPtr notepadHwnd)
    {
        IntPtr editHwnd = IntPtr.Zero;

        EnumChildWindows(notepadHwnd, (child, _) =>
        {
            var cls = new StringBuilder(64);
            GetClassName(child, cls, cls.Capacity);
            if (cls.ToString() == "Edit")
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

    public static void CloseNotepadWindows(IEnumerable<NotepadDoc> docs)
    {
        foreach (var doc in docs)
        {
            uint threadId = GetWindowThreadProcessId(doc.Hwnd, out uint pid);
            if (threadId == 0 || pid == 0) continue;
            try
            {
                var process = System.Diagnostics.Process.GetProcessById((int)pid);
                process.Kill();
            }
            catch { /* process may have already exited */ }
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
