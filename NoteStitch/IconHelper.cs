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
