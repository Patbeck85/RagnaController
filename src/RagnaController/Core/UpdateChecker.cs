using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace RagnaController.Core
{
    /// <summary>
    /// Checks for updates via GitHub Releases API.
    /// Downloads the first asset (ZIP/EXE) directly when the user agrees.
    /// </summary>
    public class UpdateChecker
    {
        // ── Configuration ────────────────────────────────────────────────────────
        // Set this to your GitHub releases API URL before publishing:
        // e.g. "https://api.github.com/repos/YourName/RagnaController/releases/latest"
        private const string GithubApiUrl    = "https://api.github.com/repos/OWNER/REPO/releases/latest";
        // Version wird zur Laufzeit aus der Assembly gelesen — niemals hardcoden
        private static readonly string CurrentVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
                ? $"{v.Major}.{v.Minor}.{v.Build}"
                : "1.0.0";
        private const string UserAgent       = "RagnaController-UpdateChecker";

        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static UpdateChecker()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a newer release exists.
        /// Returns null if no update is available or an error occurs.
        /// </summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                // Skip if URL not configured yet
                if (GithubApiUrl.Contains("OWNER") || GithubApiUrl.Contains("REPO"))
                    return null;
                var json    = await _http.GetStringAsync(GithubApiUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                if (release == null) return null;

                string latest = release.tag_name?.TrimStart('v') ?? "0.0.0";
                if (!IsNewer(latest, CurrentVersion)) return null;

                // Find first asset (ZIP or EXE)
                string? downloadUrl  = null;
                string? assetName    = null;
                long    assetSize    = 0;

                if (release.assets != null)
                {
                    foreach (var asset in release.assets)
                    {
                        var name = asset.name ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.browser_download_url;
                            assetName   = asset.name;
                            assetSize   = asset.size;
                            break;
                        }
                    }
                }

                return new UpdateInfo
                {
                    CurrentVersion = CurrentVersion,
                    LatestVersion  = latest,
                    ReleaseNotes   = release.body ?? "No release notes available.",
                    PublishedAt    = release.published_at,
                    DownloadUrl    = downloadUrl ?? release.html_url ?? string.Empty,
                    AssetName      = assetName  ?? "RagnaController.zip",
                    AssetSizeBytes = assetSize,
                    IsDirectDownload = downloadUrl != null
                };
            }
            catch
            {
                return null; // Network error → fail silently
            }
        }

        /// <summary>
        /// Shows update dialog and downloads directly upon confirmation.
        /// </summary>
        public static async Task CheckAndNotifyAsync(Window owner)
        {
            var info = await CheckAsync();
            if (info == null) return;

            string sizeMb = info.AssetSizeBytes > 0
                ? $" ({info.AssetSizeBytes / 1024.0 / 1024.0:F1} MB)"
                : "";

            string msg =
                $"A new version is available!\n\n" +
                $"  Current version:  {info.CurrentVersion}\n" +
                $"  New version:      {info.LatestVersion}\n" +
                $"  Released:          {info.PublishedAt:MM/dd/yyyy}\n\n" +
                $"─────────────────────────────────\n" +
                $"{TruncateNotes(info.ReleaseNotes, 300)}\n\n" +
                (info.IsDirectDownload
                    ? $"Download now? [{info.AssetName}{sizeMb}]"
                    : "Open download page?");

            var result = MessageBox.Show(msg, "RagnaController – Update Available",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            if (info.IsDirectDownload)
                await DownloadAndOpenAsync(info);
            else
                OpenBrowser(info.DownloadUrl);
        }

        // ── Download ──────────────────────────────────────────────────────────────

        private static async Task DownloadAndOpenAsync(UpdateInfo info)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), info.AssetName);

            try
            {
                // Progress dialog (simple)
                var progress = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };

                using var response = await _http.GetAsync(
                    info.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                await using var stream  = await response.Content.ReadAsStreamAsync();
                await using var file    = File.Create(tempPath);
                await stream.CopyToAsync(file);

                // Open temp folder and select file
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = $"/select,\"{tempPath}\"",
                    UseShellExecute = false
                });

                MessageBox.Show(
                    $"Download complete!\n\n{tempPath}\n\n" +
                    "The folder has been opened. Please replace the old executable.",
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Download failed:\n{ex.Message}\n\n" +
                    $"Please download manually:\n{info.DownloadUrl}",
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                OpenBrowser(info.DownloadUrl);
            }
        }

        private static void OpenBrowser(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[UpdateChecker] Browser open error: {ex.Message}"); }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static bool IsNewer(string latest, string current)
        {
            try
            {
                var a = new Version(latest);
                var b = new Version(current);
                return a > b;
            }
            catch { return false; }
        }

        private static string TruncateNotes(string notes, int maxLen) =>
            notes.Length <= maxLen ? notes : notes[..maxLen] + "…";

        // ── GitHub JSON models ───────────────────────────────────────────────────

        private class GitHubRelease
        {
            public string?        tag_name     { get; set; }
            public string?        html_url     { get; set; }
            public string?        body         { get; set; }
            public DateTime       published_at { get; set; }
            public GitHubAsset[]? assets       { get; set; }
        }

        private class GitHubAsset
        {
            public string? name                   { get; set; }
            public string? browser_download_url   { get; set; }
            public long    size                    { get; set; }
        }
    }

    public class UpdateInfo
    {
        public string   CurrentVersion   { get; set; } = string.Empty;
        public string   LatestVersion    { get; set; } = string.Empty;
        public string   ReleaseNotes     { get; set; } = string.Empty;
        public DateTime PublishedAt      { get; set; }
        public string   DownloadUrl      { get; set; } = string.Empty;
        public string   AssetName        { get; set; } = string.Empty;
        public long     AssetSizeBytes   { get; set; }
        public bool     IsDirectDownload { get; set; }
    }
}
