using System;
using System.IO;
using System.Text.Json;

namespace RagnaController.Models
{
    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RagnaController", "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string LastProfileName    { get; set; } = "Novice";
        public string LastGameMode       { get; set; } = "Ren";
        public bool StartMinimized       { get; set; } = false;
        public bool ShowControllerViz    { get; set; } = true;
        public bool AutoStart            { get; set; } = false;
        public string WindowPosition     { get; set; } = string.Empty;
        public bool SoundEnabled         { get; set; } = true;
        public bool RumbleEnabled        { get; set; } = true;
        public bool StartInMiniMode      { get; set; } = false;
        public int  LogLevel             { get; set; } = 1;
        // Focus Lock: pause engine when the RO client window is not in the foreground
        public bool FocusLockEnabled     { get; set; } = true;
        public string FocusLockProcess   { get; set; } = "ragexe";

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch { }
        }
    }
}