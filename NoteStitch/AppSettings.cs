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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteStitch;

/// <summary>
/// Source-generated serializer for <see cref="AppSettings"/>. Using a generated context
/// (instead of reflection-based <see cref="JsonSerializer"/>) keeps load/save trim- and
/// AOT-safe and avoids the IL2026 warnings that can silently break deserialization. Reads are
/// case-insensitive so hand-edited or legacy settings files still load.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NoteStitch", "settings.json");

    public string AutoSaveFolder        { get; set; } = string.Empty;
    public bool   IncludeSavedFiles     { get; set; } = false;
    public bool   IncludeMergedFiles    { get; set; } = false;
    public bool   RunOnWindowsStartup   { get; set; } = false;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteStitch] Failed to load settings: {ex.Message}");
        }
        return new AppSettings();
    }

    /// <returns>null on success, error message on failure.</returns>
    public string? Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteStitch] Failed to save settings: {ex.Message}");
            return ex.Message;
        }
    }
}
