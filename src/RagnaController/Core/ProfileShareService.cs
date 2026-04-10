using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RagnaController.Profiles;

namespace RagnaController.Core
{
    /// <summary>
    /// Community Profile Sharing via GitHub Gist API.
    ///
    /// Why GitHub Gists (not Firebase / custom server):
    ///  - Zero server costs, zero maintenance, runs forever
    ///  - No account required for READING (download by code)
    ///  - Optional: user provides their own Personal Access Token for uploading
    ///  - Gist IDs are 32-char hex — we generate a human-readable 6-char code from the first 6 chars
    ///  - Profiles are plain JSON — inspectable, trustworthy, no binary blobs
    ///
    /// Share code format: "GX-A3F9" — class prefix (2 chars) + 4 hex chars from Gist ID
    /// </summary>
    public static class ProfileShareService
    {
        private const string GistApiUrl  = "https://api.github.com/gists";
        private const string UserAgent   = "RagnaController-ProfileShare/1.2";

        // Shared read-only HTTP client (no auth — for downloads)
        private static readonly HttpClient _anonHttp = BuildClient(token: null);

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Upload a profile as a public Gist and return a share code like "GX-A3F9".
        /// <paramref name="token"/> is a GitHub Personal Access Token (gist scope only).
        /// If null, uploads anonymously (Gist will be public + anonymous).
        /// </summary>
        public static async Task<ShareResult> UploadAsync(Profile profile, string? token = null)
        {
            try
            {
                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                string filename = SanitizeFilename(profile.Name) + ".ragnaprofile.json";

                var body = new
                {
                    description = $"RagnaController Profile — {profile.Name} ({profile.Class})",
                    @public = true,
                    files = new System.Collections.Generic.Dictionary<string, object>
                    {
                        [filename] = new { content = json }
                    }
                };

                string payload = JsonSerializer.Serialize(body);
                var request = new HttpRequestMessage(HttpMethod.Post, GistApiUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var http = token != null ? BuildClient(token) : _anonHttp;
                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return ShareResult.Fail($"GitHub API error: {(int)response.StatusCode}");

                var raw = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(raw);
                string gistId = doc.RootElement.GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(gistId))
                    return ShareResult.Fail("Invalid response from GitHub");

                string code = GenerateCode(profile.Class, gistId);
                return ShareResult.Ok(code, gistId, $"https://gist.github.com/{gistId}");
            }
            catch (Exception ex)
            {
                return ShareResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Download a profile by its share code or full Gist ID.
        /// Accepts both "GX-A3F9" codes and raw 32-char Gist IDs.
        /// </summary>
        public static async Task<DownloadResult> DownloadAsync(string codeOrId)
        {
            try
            {
                string gistId = ResolveGistId(codeOrId.Trim());
                if (string.IsNullOrEmpty(gistId))
                    return DownloadResult.Fail("Invalid code format. Expected format: XX-XXXX or a full Gist ID.");

                string url = $"{GistApiUrl}/{gistId}";
                var response = await _anonHttp.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return DownloadResult.Fail($"Profile code '{codeOrId}' not found.");
                if (!response.IsSuccessStatusCode)
                    return DownloadResult.Fail($"GitHub API error: {(int)response.StatusCode}");

                var raw = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(raw);

                // Find the first .ragnaprofile.json file in the Gist
                if (!doc.RootElement.TryGetProperty("files", out var files))
                    return DownloadResult.Fail("Gist contains no files.");

                foreach (var file in files.EnumerateObject())
                {
                    if (!file.Name.EndsWith(".ragnaprofile.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? content = null;
                    if (file.Value.TryGetProperty("content", out var contentProp))
                        content = contentProp.GetString();

                    // Gist API truncates large files — fetch raw_url if needed
                    if (string.IsNullOrEmpty(content) &&
                        file.Value.TryGetProperty("raw_url", out var rawUrl))
                    {
                        content = await _anonHttp.GetStringAsync(rawUrl.GetString()!);
                    }

                    if (string.IsNullOrEmpty(content))
                        return DownloadResult.Fail("Profile file is empty.");

                    var profile = JsonSerializer.Deserialize<Profile>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (profile == null)
                        return DownloadResult.Fail("Could not parse profile JSON.");

                    profile.IsBuiltIn = false;
                    return DownloadResult.Ok(profile);
                }

                return DownloadResult.Fail("No .ragnaprofile.json file found in this Gist.");
            }
            catch (Exception ex)
            {
                return DownloadResult.Fail(ex.Message);
            }
        }

        // ── Code generation & resolution ───────────────────────────────────────────

        // "Guillotine Cross" → "GX", "High Priest" → "HP", "Sniper" → "SN"
        private static string GenerateCode(string className, string gistId)
        {
            string prefix = className.Length >= 2
                ? (className.Split(' ') is { Length: > 1 } parts
                    ? $"{parts[0][0]}{parts[^1][0]}"
                    : className[..2])
                : "RC";
            string suffix = gistId[..4].ToUpperInvariant();
            return $"{prefix.ToUpper()}-{suffix}";
        }

        // "GX-A3F9" → try to find matching Gist. For now: the suffix IS part of the Gist ID.
        // Full 32-char hex IDs pass through directly.
        private static string ResolveGistId(string code)
        {
            // Raw Gist ID: 32 hex chars
            if (Regex.IsMatch(code, @"^[0-9a-fA-F]{20,}$"))
                return code;

            // Share code format "XX-XXXX" — suffix is first 4 chars of Gist ID
            // We can't reverse the full ID from 4 chars, so we store the full ID in
            // a local share-code registry (see ShareCodeCache below).
            // If the code is not in cache, return empty and let caller show error.
            var match = Regex.Match(code, @"^[A-Z]{2}-([0-9A-Fa-f]{4})$");
            if (!match.Success) return string.Empty;

            string suffix = match.Groups[1].Value.ToLowerInvariant();
            return ShareCodeCache.Resolve(suffix);
        }

        private static string SanitizeFilename(string name) =>
            Regex.Replace(name, @"[^\w\s\-]", "").Replace(' ', '_').Trim();

        private static HttpClient BuildClient(string? token)
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (token != null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }

    // ── Simple share-code registry ─────────────────────────────────────────────────
    // Maps the 4-char suffix → full Gist ID, persisted to AppData.
    // Built up as the user uploads/downloads profiles.
    public static class ShareCodeCache
    {
        private static readonly string CachePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RagnaController", "share_codes.json");

        private static System.Collections.Generic.Dictionary<string, string> _map = Load();

        public static void Register(string suffix, string gistId)
        {
            _map[suffix.ToLowerInvariant()] = gistId;
            Save();
        }

        public static string Resolve(string suffix) =>
            _map.TryGetValue(suffix.ToLowerInvariant(), out var id) ? id : string.Empty;

        private static System.Collections.Generic.Dictionary<string, string> Load()
        {
            try
            {
                if (System.IO.File.Exists(CachePath))
                    return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(
                        System.IO.File.ReadAllText(CachePath)) ?? new();
            }
            catch { }
            return new();
        }

        private static void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CachePath)!);
                System.IO.File.WriteAllText(CachePath, JsonSerializer.Serialize(_map,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    // ── Result types ───────────────────────────────────────────────────────────────
    public class ShareResult
    {
        public bool    Success  { get; private set; }
        public string  Code     { get; private set; } = "";
        public string  GistId   { get; private set; } = "";
        public string  Url      { get; private set; } = "";
        public string  Error    { get; private set; } = "";

        public static ShareResult Ok(string code, string gistId, string url) =>
            new() { Success = true, Code = code, GistId = gistId, Url = url };
        public static ShareResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class DownloadResult
    {
        public bool     Success { get; private set; }
        public Profile? Profile { get; private set; }
        public string   Error   { get; private set; } = "";

        public static DownloadResult Ok(Profile p) =>
            new() { Success = true, Profile = p };
        public static DownloadResult Fail(string error) =>
            new() { Success = false, Error = error };
    }
}
