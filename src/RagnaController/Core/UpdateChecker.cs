using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace RagnaController.Core
{
    /// <summary>
    /// Checks for updates from GitHub Releases and notifies the user.
    /// </summary>
    public class UpdateChecker
    {
        private const string CurrentVersion = "1.1.0";
        private const string GithubApiUrl = "https://api.github.com/repos/yourusername/RagnaController/releases/latest";
        private const string UserAgent = "RagnaController-UpdateChecker";

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GithubApiUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (release == null) return null;

                string latestVersion = release.tag_name?.TrimStart('v') ?? "0.0.0";
                
                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    return new UpdateInfo
                    {
                        LatestVersion = latestVersion,
                        CurrentVersion = CurrentVersion,
                        DownloadUrl = release.html_url ?? string.Empty,
                        ReleaseNotes = release.body ?? "No release notes available.",
                        PublishedAt = release.published_at
                    };
                }

                return null; // No update available
            }
            catch
            {
                // Network error or API issue - fail silently
                return null;
            }
        }

        public static void ShowUpdateNotification(UpdateInfo update)
        {
            var result = MessageBox.Show(
                $"A new version of RagnaController is available!\n\n" +
                $"Current version: {update.CurrentVersion}\n" +
                $"Latest version: {update.LatestVersion}\n\n" +
                $"Would you like to download it now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                OpenDownloadPage(update.DownloadUrl);
            }
        }

        public static void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    $"Could not open browser. Please visit:\n{url}",
                    "Update Download",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ── Version Comparison ────────────────────────────────────────────────────

        private static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('.');
                var currentParts = current.Split('.');

                for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
                {
                    if (!int.TryParse(latestParts[i], out int latestNum) ||
                        !int.TryParse(currentParts[i], out int currentNum))
                        continue;

                    if (latestNum > currentNum) return true;
                    if (latestNum < currentNum) return false;
                }

                return latestParts.Length > currentParts.Length;
            }
            catch
            {
                return false;
            }
        }

        // ── Data Models ───────────────────────────────────────────────────────────

        private class GitHubRelease
        {
            public string? tag_name { get; set; }
            public string? html_url { get; set; }
            public string? body { get; set; }
            public DateTime published_at { get; set; }
        }
    }

    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }
}
