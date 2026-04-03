using System.Text;

namespace NoteStitch;

/// <summary>
/// Reads tab content directly from Windows 11 Notepad's binary state files.
/// Allows reading ALL open tabs, not just the active one.
/// Path: %LocalAppData%\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState\TabState\
/// Binary format reverse-engineered by ogmini (https://github.com/ogmini/Notepad-State-Library).
/// </summary>
internal static class Win11NotepadReader
{
    internal static readonly string TabStateFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState\TabState");

    // Bug #1 fix: check process once, not twice (IsAvailable + GetAllTabs)
    // Bug #2 fix: dispose Process objects
    public static bool IsAvailable
    {
        get
        {
            if (!Directory.Exists(TabStateFolder)) return false;
            var procs = System.Diagnostics.Process.GetProcessesByName("Notepad");
            bool running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return running;
        }
    }

    public static List<NotepadDoc> GetAllTabs()
    {
        var docs = new List<NotepadDoc>();

        // Bug #2 fix: dispose processes after extracting PID
        var procs = System.Diagnostics.Process.GetProcessesByName("Notepad");
        int pid = procs.Length > 0 ? procs[0].Id : 0;
        foreach (var p in procs) p.Dispose();

        try
        {
            foreach (var file in Directory.GetFiles(TabStateFolder, "*.bin"))
            {
                try
                {
                    var doc = ParseTabState(file, pid);
                    if (doc is not null)
                        docs.Add(doc);
                }
                catch { /* skip corrupt/unreadable files */ }
            }
        }
        catch { }

        return docs;
    }

    private static NotepadDoc? ParseTabState(string filePath, int pid)
    {
        byte[] bytes;
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 4) return null;
            bytes = new byte[fs.Length];
            // Bug #3 fix: ReadExactly guarantees full buffer fill
            fs.ReadExactly(bytes);
        }
        catch { return null; }

        using var ms  = new MemoryStream(bytes);
        using var rdr = new BinaryReader(ms);

        // Validate "NP" header
        if (rdr.ReadByte() != 0x4E || rdr.ReadByte() != 0x50) return null;

        ReadLEB128(rdr); // SequenceNumber (unused)
        ulong typeFlag = ReadLEB128(rdr);

        string filename = "Untitled";
        string content  = string.Empty;

        switch (typeFlag)
        {
            case 0: // Unsaved buffer — content is embedded in state file
            {
                rdr.ReadByte();             // unknown delimiter
                ReadLEB128(rdr);            // SelectionStartIndex
                ReadLEB128(rdr);            // SelectionEndIndex
                rdr.ReadByte();             // WordWrap
                rdr.ReadByte();             // RightToLeft
                rdr.ReadByte();             // ShowUnicode
                Skip(rdr, ReadLEB128(rdr)); // Options (OptionCount bytes)

                ulong len = ReadLEB128(rdr);
                // Bug #4 fix: guard against corrupt/huge values before cast
                if (len > 0x400000) return null; // >4M chars is unreasonable
                if (len > 0)
                    content = Encoding.Unicode.GetString(rdr.ReadBytes((int)len * 2));

                rdr.ReadByte();   // Unsaved flag
                rdr.ReadBytes(4); // CRC32
                break;
            }

            case 1: // Saved buffer — linked to a file on disk
            {
                ulong pathLen = ReadLEB128(rdr);
                if (pathLen > 0x8000) return null; // sanity
                string savedPath = Encoding.Unicode.GetString(rdr.ReadBytes((int)pathLen * 2));
                filename = Path.GetFileName(savedPath);
                if (string.IsNullOrWhiteSpace(filename)) filename = "Untitled";

                ulong savedContentLen = ReadLEB128(rdr);
                rdr.ReadByte();             // EncodingType
                rdr.ReadByte();             // CarriageReturnType
                ReadLEB128(rdr);            // Timestamp
                rdr.ReadBytes(32);          // FileHashStored (SHA-256)
                rdr.ReadBytes(2);           // Delimiter (0x00 0x01)
                ReadLEB128(rdr);            // SelectionStartIndex
                ReadLEB128(rdr);            // SelectionEndIndex
                rdr.ReadByte();             // WordWrap
                rdr.ReadByte();             // RightToLeft
                rdr.ReadByte();             // ShowUnicode
                Skip(rdr, ReadLEB128(rdr)); // Options (OptionCount bytes)

                ulong len = ReadLEB128(rdr);
                if (len > 0x400000) return null; // Bug #4 fix
                if (len > 0)
                {
                    content = Encoding.Unicode.GetString(rdr.ReadBytes((int)len * 2));
                }
                else if (savedContentLen > 0 && File.Exists(savedPath))
                {
                    // Content unchanged from disk — read the actual file
                    try { content = File.ReadAllText(savedPath); } catch { }
                }

                rdr.ReadByte();   // Unsaved flag
                rdr.ReadBytes(4); // CRC32
                break;
            }

            default:
                // State file (0.bin / 1.bin) — not a tab content file
                return null;
        }

        // Bug #5 fix: apply unsaved buffer chunks (incremental edits on top of base content)
        content = ApplyUnsavedChunks(rdr, content);

        // Skip empty untitled tabs
        if (string.IsNullOrEmpty(content) && filename == "Untitled")
            return null;

        return new NotepadDoc
        {
            Hwnd       = IntPtr.Zero,
            ProcessId  = pid,
            Filename   = filename,
            Text       = content,
            SourceFile = filePath
        };
    }

    /// <summary>
    /// Replays incremental keystrokes stored after the CRC32 to produce the current text.
    /// Each chunk: cursorPos, deleteCount, addCount, addedChars (UTF-16).
    /// </summary>
    private static string ApplyUnsavedChunks(BinaryReader rdr, string baseContent)
    {
        if (rdr.BaseStream.Position >= rdr.BaseStream.Length)
            return baseContent;

        var sb = new StringBuilder(baseContent);
        while (rdr.BaseStream.Position < rdr.BaseStream.Length)
        {
            ulong pos = ReadLEB128(rdr);
            ulong del = ReadLEB128(rdr);
            ulong add = ReadLEB128(rdr);

            if (add > 0x100000) break; // sanity guard against corrupt data
            string added = add > 0
                ? Encoding.Unicode.GetString(rdr.ReadBytes((int)add * 2))
                : string.Empty;
            rdr.ReadBytes(4); // CRC32 per chunk

            int index = (int)Math.Min(pos, (ulong)sb.Length);
            if (del > 0)
                sb.Remove(index, (int)Math.Min(del, (ulong)(sb.Length - index)));
            if (added.Length > 0)
                sb.Insert(index, added);
        }
        return sb.ToString();
    }

    private static ulong ReadLEB128(BinaryReader rdr)
    {
        ulong value = 0;
        int   shift = 0;
        while (rdr.BaseStream.Position < rdr.BaseStream.Length)
        {
            byte b = rdr.ReadByte();
            value |= (ulong)(b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0) break;
        }
        return value;
    }

    private static void Skip(BinaryReader rdr, ulong count)
    {
        if (count > 0) rdr.ReadBytes((int)count);
    }
}
