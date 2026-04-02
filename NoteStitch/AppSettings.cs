using System.Text.Json;

namespace NoteStitch;

internal class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NoteStitch", "settings.json");

    public string AutoSaveFolder { get; set; } = string.Empty;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteStitch] Failed to save settings: {ex.Message}");
            return ex.Message;
        }
    }
}
