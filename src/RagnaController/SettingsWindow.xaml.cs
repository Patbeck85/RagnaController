using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RagnaController.Models;

namespace RagnaController
{
    public partial class SettingsWindow : Window
    {
        private readonly Settings _s;
        // Nullable: null is valid when no live callback is needed
        private readonly Action<Settings>? _onSave;

        public SettingsWindow(Settings s, Action<Settings>? onSave)
        {
            InitializeComponent();
            _s = s;
            _onSave = onSave;
            ChkAutoStart.IsChecked       = _s.AutoStart;
            ChkSound.IsChecked           = _s.SoundEnabled;
            ChkRumble.IsChecked          = _s.RumbleEnabled;
            ChkStartInMiniMode.IsChecked = _s.StartInMiniMode;
            ChkFocusLock.IsChecked       = _s.FocusLockEnabled;
            TxtFocusProcess.Text         = _s.FocusLockProcess;
            LogLevelCombo.SelectedIndex  = _s.LogLevel;
            LblVersion.Text = "Version " +
                (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.2.0");
            LblSettingsPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RagnaController");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _s.AutoStart       = ChkAutoStart.IsChecked == true;
            _s.SoundEnabled    = ChkSound.IsChecked == true;
            _s.RumbleEnabled   = ChkRumble.IsChecked == true;
            _s.StartInMiniMode = ChkStartInMiniMode.IsChecked == true;
            _s.FocusLockEnabled = ChkFocusLock.IsChecked == true;
            _s.FocusLockProcess = string.IsNullOrWhiteSpace(TxtFocusProcess.Text) ? "ragexe" : TxtFocusProcess.Text.Trim();
            _s.LogLevel        = LogLevelCombo.SelectedIndex;
            _s.Save();
            _onSave?.Invoke(_s);
            DialogResult = true;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var d = new Settings();
            ChkAutoStart.IsChecked       = d.AutoStart;
            ChkSound.IsChecked           = d.SoundEnabled;
            ChkRumble.IsChecked          = d.RumbleEnabled;
            ChkStartInMiniMode.IsChecked = d.StartInMiniMode;
            ChkFocusLock.IsChecked       = d.FocusLockEnabled;
            TxtFocusProcess.Text         = d.FocusLockProcess;
            LogLevelCombo.SelectedIndex  = d.LogLevel;
        }

        private void LblSettingsPath_Click(object sender, MouseButtonEventArgs e)
        {
            try { Process.Start("explorer.exe", $"\"{LblSettingsPath.Text}\""); } catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select your Ragnarok Online client executable",
                Filter = "Executable (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (ofd.ShowDialog() == true)
            {
                // Store only the filename without extension — that's what GetProcessById returns
                string processName = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                TxtFocusProcess.Text = processName;
            }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
