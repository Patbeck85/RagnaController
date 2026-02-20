using System;
using System.IO;
using System.Text.Json;

namespace RagnaController.Models
{
    /// <summary>
    /// Persistent application settings stored in %AppData%\RagnaController\settings.json.
    /// </summary>
    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RagnaController", "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        // ── Properties ────────────────────────────────────────────────────────────

        public string LastProfileName   { get; set; } = "Melee";
        public bool   StartMinimized    { get; set; } = false;
        public bool   ShowControllerViz { get; set; } = true;
        public bool   AutoStart         { get; set; } = false;
        public string WindowPosition    { get; set; } = string.Empty;
        
        // New features
        public bool   SoundEnabled      { get; set; } = true;
        public bool   RumbleEnabled     { get; set; } = true;
        public bool   StartInMiniMode   { get; set; } = false;
        public int    LogLevel          { get; set; } = 1; // Info

        // ── Load / Save ───────────────────────────────────────────────────────────

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
                }
            }
            catch { /* fall through to defaults */ }
            return new Settings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
