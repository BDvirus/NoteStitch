// NoteStitch — Stitch multiple Notepad windows into one document.
// Copyright (C) 2026 Dvirus
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace NoteStitch;

internal static class Win11NotepadSessionCloser
{
    private static readonly string WindowStateFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState\WindowState");

    internal static void CloseAll(
        IEnumerable<int> processIds,
        IEnumerable<string> tabStateFiles)
    {
        CloseAll(
            processIds,
            tabStateFiles,
            WindowStateFolder,
            StopAndWait,
            DeleteFile,
            EnumerateStateFiles);
    }

    internal static void CloseAll(
        IEnumerable<int> processIds,
        IEnumerable<string> tabStateFiles,
        string windowStateFolder,
        Func<int, bool> stopAndWait,
        Action<string> deleteFile,
        Func<string, IEnumerable<string>> enumerateFiles)
    {
        // Notepad writes session data while it is running. Stop every process first,
        // otherwise deleted state files can be recreated before shutdown completes.
        foreach (int processId in processIds.Distinct())
        {
            if (!stopAndWait(processId))
                return;
        }

        foreach (string file in tabStateFiles.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
            deleteFile(file);

        foreach (string file in enumerateFiles(windowStateFolder))
            deleteFile(file);
    }

    private static bool StopAndWait(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return process.WaitForExit(5000);
        }
        catch
        {
            // A process that already exited is safe; an inaccessible live process is not.
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                return process.HasExited;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void DeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { }
    }

    private static IEnumerable<string> EnumerateStateFiles(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*.bin").ToArray()
                : [];
        }
        catch
        {
            return [];
        }
    }
}
