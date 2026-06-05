using System.Runtime.InteropServices;
using SystemTray.Interfaces;

namespace NoteStitch;

/// <summary>
/// Loads a tray icon from a raw ICO byte array using a temporary file.
/// </summary>
internal sealed class BytesIcon : IIconFile
{
    private readonly nint _hIcon;

    public BytesIcon(byte[] icoBytes)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "ns_tray.ico");
        File.WriteAllBytes(tmp, icoBytes);
        _hIcon = LoadImage(IntPtr.Zero, tmp, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        try { File.Delete(tmp); } catch { }
        if (_hIcon == 0) throw new InvalidOperationException("Cannot load tray icon.");
    }

    public nint Handle => _hIcon;

    public void Dispose() => DestroyIcon(_hIcon);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern nint LoadImage(IntPtr hInst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);
}
