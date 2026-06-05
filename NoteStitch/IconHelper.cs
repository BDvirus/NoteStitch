using System.Reflection;

namespace NoteStitch;

internal static class IconHelper
{
    private static Stream? OpenIco() =>
        Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NoteStitch.Assets.icon.ico");

    // Returns the path to the exe (WinUI 3 compatible, no Application.ExecutablePath)
    private static string ExeDir =>
        Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)!;

    // Writes NoteStitch.ico next to the exe so the .lnk shortcut can reference it.
    public static string EnsureIcoFile()
    {
        string icoPath = Path.Combine(ExeDir, "NoteStitch.ico");

        if (File.Exists(icoPath)) return icoPath;

        using var stream = OpenIco();
        if (stream is null) return string.Empty;

        try
        {
            using var fs = File.Create(icoPath);
            stream.CopyTo(fs);
        }
        catch
        {
            try { File.Delete(icoPath); } catch { }
            return string.Empty;
        }

        return icoPath;
    }

    // Returns raw ICO bytes for use as Win32 HICON (loaded via LoadImage / CreateIconFromResource)
    public static byte[]? GetIcoBytes()
    {
        using var stream = OpenIco();
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
