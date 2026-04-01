using System.Drawing;
using System.Reflection;

namespace NoteStitch;

internal static class IconHelper
{
    private static Stream? OpenIco() =>
        Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NoteStitch.Assets.icon.ico");

    public static Icon Load()
    {
        var stream = OpenIco();
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    // Writes NoteStitch.ico next to the exe so the .lnk shortcut can reference it.
    public static string EnsureIcoFile()
    {
        string icoPath = Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath)!,
            "NoteStitch.ico");

        if (File.Exists(icoPath)) return icoPath;

        using var stream = OpenIco();
        if (stream is null) return string.Empty;

        try { using var fs = File.Create(icoPath); stream.CopyTo(fs); }
        catch { return string.Empty; }

        return icoPath;
    }
}
