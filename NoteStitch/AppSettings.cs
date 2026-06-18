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
