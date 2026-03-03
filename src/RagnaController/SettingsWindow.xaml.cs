using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using RagnaController.Models;

namespace RagnaController
{
    public partial class SettingsWindow : Window
    {
        private readonly Settings _settings;
        private readonly Action<Settings> _onSave;

        public SettingsWindow(Settings current, Action<Settings> onSave)
        {
            InitializeComponent();
            _settings = current;
            _onSave   = onSave;
            LoadValues();
        }

        // ── Werte laden ───────────────────────────────────────────────────────────

        private void LoadValues()
        {
            ChkAutoStart.IsChecked      = _settings.AutoStart;
            ChkStartMinimized.IsChecked = _settings.StartMinimized;
            ChkStartMiniMode.IsChecked  = _settings.StartInMiniMode;
            ChkSound.IsChecked          = _settings.SoundEnabled;
            ChkRumble.IsChecked         = _settings.RumbleEnabled;
            ChkControllerViz.IsChecked  = _settings.ShowControllerViz;

            CmbLogLevel.SelectedIndex   = Math.Clamp(_settings.LogLevel, 0, 3);

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            LblVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.0";

            LblSettingsPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RagnaController");
        }

        // ── Speichern ─────────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoStart         = ChkAutoStart.IsChecked      == true;
            _settings.StartMinimized    = ChkStartMinimized.IsChecked == true;
            _settings.StartInMiniMode   = ChkStartMiniMode.IsChecked  == true;
            _settings.SoundEnabled      = ChkSound.IsChecked          == true;
            _settings.RumbleEnabled     = ChkRumble.IsChecked         == true;
            _settings.ShowControllerViz = ChkControllerViz.IsChecked  == true;
            _settings.LogLevel          = CmbLogLevel.SelectedIndex;

            _settings.Save();
            _onSave(_settings);

            DialogResult = true;
            Close();
        }

        // ── Reset ───────────────────────────────────────────────────────────────────

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all settings to default values?",
                "Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var defaults = new Settings();
            ChkAutoStart.IsChecked      = defaults.AutoStart;
            ChkStartMinimized.IsChecked = defaults.StartMinimized;
            ChkStartMiniMode.IsChecked  = defaults.StartInMiniMode;
            ChkSound.IsChecked          = defaults.SoundEnabled;
            ChkRumble.IsChecked         = defaults.RumbleEnabled;
            ChkControllerViz.IsChecked  = defaults.ShowControllerViz;
            CmbLogLevel.SelectedIndex   = defaults.LogLevel;
        }

        // ── Open settings path ──────────────────────────────────────────────────────

        private void LblSettingsPath_Click(object sender, MouseButtonEventArgs e)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RagnaController");
            if (Directory.Exists(path))
                Process.Start("explorer.exe", path);
        }

        // ── Fenster ───────────────────────────────────────────────────────────────

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
