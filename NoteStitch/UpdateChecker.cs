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
