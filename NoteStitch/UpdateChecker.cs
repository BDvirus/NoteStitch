using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace NoteStitch;

internal static class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/BDvirus/NoteStitch/releases/latest";
    private const string ReleasePage = "https://github.com/BDvirus/NoteStitch/releases/latest";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static async Task<ReleaseInfo?> GetLatestReleaseAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "NoteStitch-Updater");
        client.Timeout = TimeSpan.FromSeconds(10);

        var release = await client.GetFromJsonAsync<GitHubRelease>(ApiUrl);
        if (release is null || string.IsNullOrEmpty(release.TagName)) return null;

        // tag: "v1.2.0" → Version 1.2.0
        var tag = release.TagName.TrimStart('v');
        if (!Version.TryParse(tag, out var latestVersion)) return null;

        if (latestVersion <= CurrentVersion) return null;

        // Find the NoteStitch.exe asset
        var asset = release.Assets?.FirstOrDefault(a =>
            a.Name.Equals("NoteStitch.exe", StringComparison.OrdinalIgnoreCase));

        return new ReleaseInfo
        {
            Version     = latestVersion,
            TagName     = release.TagName,
            DownloadUrl = asset?.BrowserDownloadUrl ?? string.Empty,
            ReleasePage = ReleasePage,
            HasAsset    = asset is not null
        };
    }

    // ── JSON models ───────────────────────────────────────────────────────────

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]   public string? TagName { get; set; }
        [JsonPropertyName("assets")]     public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]                  public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")]  public string BrowserDownloadUrl { get; set; } = "";
    }
}

internal class ReleaseInfo
{
    public Version Version     { get; set; } = new();
    public string  TagName     { get; set; } = "";
    public string  DownloadUrl { get; set; } = "";
    public string  ReleasePage { get; set; } = "";

    public bool HasAsset        { get; set; } = true;
    public bool HasDirectDownload => !string.IsNullOrEmpty(DownloadUrl);
}
