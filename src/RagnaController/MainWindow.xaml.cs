using System;
using System.Reflection;
using WinForms = System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RagnaController.Core;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using RagnaController.Models;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class MainWindow : Window
    {
        private readonly HybridEngine   _engine         = new();
        private readonly ProfileManager _profileManager = new();
        private readonly Settings       _settings       = Settings.Load();
        private readonly StringBuilder  _logBuffer      = new();

        private MiniModeWindow? _miniWindow;
        private bool   _isMiniMode      = false;
        private bool   _actionRpgOn     = true;
        private string _activeTab       = "Base";
        private bool   _isDirty         = false;   // unsaved changes
        private WinForms.NotifyIcon? _trayIcon;
        private bool   _suppressSliders = false;   // suppress recursive events while loading profile

        // ── Global Hotkeys (Ctrl+1-4 = profile slots 1-4) ─────────────────────
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private const uint MOD_CONTROL   = 0x0002;
        private const int  HOTKEY_ID_BASE = 9000; // uses IDs 9001–9004 (profiles), 9010 (Ctrl+F minimode)

        public MainWindow()
        {
            InitializeComponent();
            // Version aus Assembly lesen — niemals hardcoden
            var _ver = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersionLabel.Text = _ver != null ? $"v{_ver.Major}.{_ver.Minor}.{_ver.Build}" : "v?";
            SourceInitialized += (_, _) => RegisterGlobalHotkeys();
            SubscribeEngineEvents();
            PopulateProfiles();
            SelectLastProfile();
            SetActiveTab("Base");
            ApplySettings(_settings);
            InitTrayIcon();
            RestoreWindowPosition();
            if (_settings.AutoStart)      _engine.Start();
            if (_settings.StartInMiniMode) SwitchToMiniMode();
            _ = CheckForUpdatesAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // ENGINE EVENTS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeEngineEvents()
        {
            _engine.StatusChanged += s => Dispatcher.Invoke(() =>
            {
                bool running    = s == EngineStatus.Running;
                bool noCtrl     = s == EngineStatus.NoController;

                var color = running ? Color.FromRgb(57, 255, 20)
                          : noCtrl  ? Color.FromRgb(255, 170, 0)
                          :           Color.FromRgb(255, 68, 68);

                StatusDot.Fill              = new SolidColorBrush(color);
                StatusText.Text             = running ? "RUNNING" : noCtrl ? "NO CONTROLLER" : "STOPPED";
                StatusText.Foreground       = new SolidColorBrush(color);
                EnginePulse.Fill            = new SolidColorBrush(running ? Color.FromRgb(57, 255, 20) : Color.FromRgb(85, 94, 106));
                EngineStateLabel.Text       = StatusText.Text;
                EngineStateLabel.Foreground = new SolidColorBrush(running ? Color.FromRgb(57, 255, 20) : Color.FromRgb(85, 94, 106));

                BtnStart.IsEnabled = !running;
                BtnStop.IsEnabled  = running;
            });

            _engine.SnapshotUpdated += snap => Dispatcher.Invoke(() =>
            {
                // Stick-Dots
                Canvas.SetLeft(LeftStickDot,  20 + snap.LeftX  * 18);
                Canvas.SetTop (LeftStickDot,  20 - snap.LeftY  * 18);
                Canvas.SetLeft(RightStickDot, 20 + snap.RightX * 18);
                Canvas.SetTop (RightStickDot, 20 - snap.RightY * 18);

                // Controller-Badge
                bool connected = snap.ControllerType is not ("" or "Unknown");

                // Per-brand accent colours
                (Color fg, Color bg) ctrlTheme = snap.ControllerType switch
                {
                    "PS4" or "PS5"  => (Color.FromRgb(  0, 112, 212), Color.FromRgb( 0,  18,  40)), // PlayStation blue
                    "Switch"        => (Color.FromRgb(230,   0,  18), Color.FromRgb(40,   0,   0)), // Nintendo red
                    "8BitDo"        => (Color.FromRgb(255, 105,   0), Color.FromRgb(40,  20,   0)), // 8BitDo orange
                    "Logitech"      => (Color.FromRgb(  0, 164, 228), Color.FromRgb( 0,  20,  35)), // Logitech cyan
                    "Razer"         => (Color.FromRgb(  0, 255,  68), Color.FromRgb( 0,  30,  10)), // Razer green
                    "Thrustmaster"  => (Color.FromRgb(255, 215,   0), Color.FromRgb(30,  25,   0)), // Thrustmaster gold
                    "Xbox"          => (Color.FromRgb( 16, 124,  16), Color.FromRgb( 5,  25,   5)), // Xbox green
                    _               => (Color.FromRgb( 85,  94, 106), Color.FromRgb(20,  13,  13))  // disconnected grey
                };

                Color ctrlColor = connected ? ctrlTheme.fg : Color.FromRgb(85, 94, 106);
                Color ctrlBg    = connected ? ctrlTheme.bg : Color.FromRgb(20, 13, 13);

                string ctrlLabel = connected ? snap.ControllerType.ToUpper() : "NO CONTROLLER";

                ControllerDot.Fill                  = new SolidColorBrush(ctrlColor);
                ControllerStatusText.Text           = ctrlLabel;
                ControllerStatusText.Foreground     = new SolidColorBrush(ctrlColor);
                ControllerBadge.Background          = new SolidColorBrush(ctrlBg);
                ControllerBadge.BorderBrush         = new SolidColorBrush(ctrlColor);
                ControllerBadge.BorderThickness     = connected ? new System.Windows.Thickness(1) : new System.Windows.Thickness(0);

                // Layer-Badge
                string layer = snap.LayerText ?? "BASE";
                LayerText.Text = layer + " LAYER";
                StatusLayer.Text = layer;
                var layerColor = layer switch {
                    "L1+" or "R1+" => Color.FromRgb(255, 140, 0),
                    "L2+" or "R2+" => Color.FromRgb(255, 68, 68),
                    _              => Color.FromRgb(0, 255, 255)
                };
                LayerText.Foreground   = new SolidColorBrush(layerColor);
                LayerBadge.BorderBrush = new SolidColorBrush(layerColor);
                LayerBadge.Background  = new SolidColorBrush(Color.FromArgb(30, layerColor.R, layerColor.G, layerColor.B));
                StatusLayer.Foreground = new SolidColorBrush(layerColor);

                // Precision
                bool precision = snap.PrecisionMode;
                PrecisionBadge.Visibility = precision ? Visibility.Visible : Visibility.Collapsed;
                PrecisionLabel.Text       = precision ? "ON" : "OFF";
                PrecisionLabel.Foreground = new SolidColorBrush(precision ? Color.FromRgb(255, 170, 0) : Color.FromRgb(85, 94, 106));
                PrecisionIndicator.Background = new SolidColorBrush(precision ? Color.FromRgb(30, 25, 0) : Color.FromRgb(20, 18, 0));
                StatusPrecision.Text      = precision ? "⊕ PRECISION ON" : "○ PRECISION OFF";
                StatusPrecision.Foreground = new SolidColorBrush(precision ? Color.FromRgb(255, 170, 0) : Color.FromRgb(85, 94, 106));

                // Mob-Sweep Status
                bool sweep = snap.MobSweepActive;
                StatusSweep.Text      = sweep ? "⚔ SWEEP ON" : "◌ SWEEP OFF";
                StatusSweep.Foreground = new SolidColorBrush(sweep
                    ? Color.FromRgb(212, 168, 50)   // Gold wenn aktiv
                    : Color.FromRgb(85, 94, 106));  // Grau wenn inaktiv

                // Update MiniMode live if open
                if (_isMiniMode && _miniWindow != null)
                {
                    string profileName = ProfileCombo.SelectedItem is Profile pr ? pr.Name : "–";
                    _miniWindow.UpdateState(profileName, snap.StateLabel,
                        _engine.IsRunning, snap.CombatState);
                }
            });

            // Log-Nachrichten → LOG-Tab
            _engine.BatteryChanged     += level => Dispatcher.Invoke(() => UpdateBatteryDisplay(level));
            _engine.ProfileQuickSwitch += delta  => Dispatcher.Invoke(() => QuickSwitchProfile(delta));
            _engine.LogMessage += msg => Dispatcher.Invoke(() =>
            {
                _logBuffer.AppendLine(msg);

                // Limit buffer to ~500 lines
                if (_logBuffer.Length > 20000)
                {
                    var lines = _logBuffer.ToString().Split('\n');
                    _logBuffer.Clear();
                    foreach (var l in lines.TakeLast(400))
                        _logBuffer.AppendLine(l);
                }

                LogTextBlock.Text = _logBuffer.ToString();

                // Auto-scroll
                if (_activeTab == "Log")
                    LogScrollViewer.ScrollToEnd();
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROFIL
        // ══════════════════════════════════════════════════════════════════════

        private void PopulateProfiles()
        {
            ProfileCombo.ItemsSource       = _profileManager.Profiles;
            ProfileCombo.DisplayMemberPath = "Name";
        }

        private void SelectLastProfile()
        {
            ProfileCombo.SelectedItem =
                _profileManager.Profiles.FirstOrDefault(p => p.Name == _settings.LastProfileName)
                ?? _profileManager.Profiles.FirstOrDefault();
        }

        private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile p) return;

            // Populate sliders without setting dirty flag
            _suppressSliders = true;
            SensitivitySlider.Value = p.CursorMaxSpeed;
            DeadzoneSlider.Value    = Math.Max(DeadzoneSlider.Minimum, Math.Min(DeadzoneSlider.Maximum, p.Deadzone));
            CurveSlider.Value       = Math.Max(CurveSlider.Minimum,    Math.Min(CurveSlider.Maximum,    p.CursorCurve));
            ActionSpeedSlider.Value = Math.Max(ActionSpeedSlider.Minimum, Math.Min(ActionSpeedSlider.Maximum, p.ActionSpeed));
            _suppressSliders = false;

            // Toggle
            _actionRpgOn = p.ActionRpgMode;
            UpdateToggleVisual();

            _engine.LoadProfile(p);
            BuildMappingTables(p);
            UpdateInfoTab(p);

            // Statusbar + Settings
            StatusProfile.Text     = p.Name;
            StatusCursorSpeed.Text = $"{p.CursorMaxSpeed:F0} px/s";
            _settings.LastProfileName = p.Name;
            _settings.Save();

            SetDirty(false);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile p) return;

            // Write current slider values back to profile
            p.CursorMaxSpeed  = (float)SensitivitySlider.Value;
            p.Deadzone        = (float)DeadzoneSlider.Value;
            p.CursorCurve     = (float)CurveSlider.Value;
            p.ActionSpeed     = (float)ActionSpeedSlider.Value;
            p.ActionRpgMode   = _actionRpgOn;

            _profileManager.SaveProfile(p);
            SetDirty(false);

            // Brief visual feedback
            BtnSave.Content    = "✓  SAVED";
            BtnSave.Background = new SolidColorBrush(Color.FromRgb(10, 40, 10));
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                BtnSave.Content    = "💾  SAVE";
                BtnSave.Background = new SolidColorBrush(Color.FromRgb(26, 42, 10));
                if (!_isDirty) BtnSave.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        private void SetDirty(bool dirty)
        {
            _isDirty            = dirty;
            BtnSave.Visibility  = dirty ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════════════
        // TABS
        // ══════════════════════════════════════════════════════════════════════

        private void SetActiveTab(string tab)
        {
            _activeTab = tab;

            PanelBase.Visibility = PanelL1.Visibility = PanelR1.Visibility =
                PanelL2.Visibility = PanelR2.Visibility =
                PanelLog.Visibility = PanelInfo.Visibility = Visibility.Collapsed;

            switch (tab)
            {
                case "Base": PanelBase.Visibility  = Visibility.Visible; break;
                case "L1":   PanelL1.Visibility    = Visibility.Visible; break;
                case "R1":   PanelR1.Visibility    = Visibility.Visible; break;
                case "L2":   PanelL2.Visibility    = Visibility.Visible; break;
                case "R2":   PanelR2.Visibility    = Visibility.Visible; break;
                case "Log":
                    PanelLog.Visibility = Visibility.Visible;
                    LogScrollViewer.ScrollToEnd();
                    break;
                case "Info": PanelInfo.Visibility  = Visibility.Visible; break;
            }

            var tabs = new Dictionary<string, Button>
            {
                ["Base"] = TabBtnBase, ["L1"] = TabBtnL1, ["R1"] = TabBtnR1,
                ["L2"]   = TabBtnL2,  ["R2"] = TabBtnR2,
                ["Log"]  = TabBtnLog, ["Info"] = TabBtnInfo
            };

            foreach (var (key, btn) in tabs)
            {
                bool active = key == tab;
                btn.Style = active
                    ? (Style)Resources["TabButtonActive"]
                    : (Style)Resources["TabButton"];

                if (!active && key == "Info")
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(57, 130, 20));
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // MAPPING TABELLEN
        // ══════════════════════════════════════════════════════════════════════

        private void BuildMappingTables(Profile p)
        {
            MappingsBase.Children.Clear();
            MappingsL1.Children.Clear();
            MappingsR1.Children.Clear();
            MappingsL2.Children.Clear();
            MappingsR2.Children.Clear();

            AddCategoryHeader(MappingsBase, "STANDARD BUTTONS");
            AddCategoryHeader(MappingsL1, "L1 + BUTTON  →  SKILL");
            AddCategoryHeader(MappingsR1, "R1 + BUTTON  →  SKILL");
            AddCategoryHeader(MappingsL2, "L2 + BUTTON  →  SKILL");
            AddCategoryHeader(MappingsR2, "R2 + BUTTON  →  SKILL");

            foreach (var kv in p.ButtonMappings.OrderBy(k => k.Key))
            {
                if      (kv.Key.StartsWith("L1+")) MappingsL1.Children.Add(MakeMappingRow(kv.Key, kv.Value.Label));
                else if (kv.Key.StartsWith("R1+")) MappingsR1.Children.Add(MakeMappingRow(kv.Key, kv.Value.Label));
                else if (kv.Key.StartsWith("L2+")) MappingsL2.Children.Add(MakeMappingRow(kv.Key, kv.Value.Label));
                else if (kv.Key.StartsWith("R2+")) MappingsR2.Children.Add(MakeMappingRow(kv.Key, kv.Value.Label));
                else                               MappingsBase.Children.Add(MakeMappingRow(kv.Key, kv.Value.Label));
            }

            foreach (var panel in new[] { MappingsL1, MappingsR1, MappingsL2, MappingsR2 })
                if (panel.Children.Count == 1) // nur Header
                    panel.Children.Add(MakeEmptyHint("No mappings for this layer."));
        }

        private static void AddCategoryHeader(StackPanel panel, string title)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            sp.Children.Add(new TextBlock
            {
                Text = title, Foreground = new SolidColorBrush(Color.FromRgb(85, 94, 106)),
                FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4)
            });
            sp.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(33, 38, 45)) });
            panel.Children.Add(sp);
        }

        private static UIElement MakeMappingRow(string key, string label)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock
            {
                Text = key, Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 255)),
                FontFamily = new FontFamily("Consolas"), FontSize = 12
            });
            var lbl = new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(lbl, 1);
            grid.Children.Add(lbl);
            return grid;
        }

        private static UIElement MakeEmptyHint(string text) =>
            new TextBlock
            {
                Text = text, Foreground = new SolidColorBrush(Color.FromRgb(85, 94, 106)),
                FontSize = 11, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 8, 0, 0)
            };

        // ══════════════════════════════════════════════════════════════════════
        // INFO-TAB
        // ══════════════════════════════════════════════════════════════════════

        private void UpdateInfoTab(Profile p)
        {
            InfoClassName.Text   = p.Name.ToUpper();
            InfoClassType.Text   = p.Class.ToUpper();
            InfoCursorSpeed.Text = $"{p.CursorMaxSpeed:F0} px/s";
            InfoActionSpeed.Text = $"{p.ActionSpeed:F1}";
            InfoCoastFrames.Text = $"{p.MovementCoastFrames} Frames";

            InfoSkillList.ItemsSource = p.SkillRecommendations.Count > 0
                ? p.SkillRecommendations
                : new List<string> { "No recommendations – assign F-keys freely in game." };

            InfoTips.Text = string.IsNullOrEmpty(p.ClassTips)
                ? "Set up your F-keys in game to match this class."
                : p.ClassTips.Replace("\\n", Environment.NewLine);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SLIDER → ENGINE LIVE UPDATE
        // ══════════════════════════════════════════════════════════════════════

        private void SensitivitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SensitivityValue == null) return;
            SensitivityValue.Text  = $"{e.NewValue:F0}";
            StatusCursorSpeed.Text = $"{e.NewValue:F0} px/s";
            if (!_suppressSliders)
            {
                _engine.LiveUpdateCursorSpeed((float)e.NewValue);
                SetDirty(true);
            }
        }

        private void DeadzoneSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DeadzoneValue == null) return;
            DeadzoneValue.Text = $"{e.NewValue:F2}";
            if (!_suppressSliders)
            {
                _engine.LiveUpdateDeadzone((float)e.NewValue);
                SetDirty(true);
            }
        }

        private void CurveSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CurveValue == null) return;
            CurveValue.Text = $"{e.NewValue:F1}";
            if (!_suppressSliders)
            {
                _engine.LiveUpdateCurve((float)e.NewValue);
                SetDirty(true);
            }
        }

        private void ActionSpeedSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ActionSpeedValue == null) return;
            ActionSpeedValue.Text = $"{e.NewValue:F1}";
            if (!_suppressSliders)
            {
                _engine.LiveUpdateActionSpeed((float)e.NewValue);
                SetDirty(true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ACTION RPG TOGGLE
        // ══════════════════════════════════════════════════════════════════════

        private void MoveModeToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _actionRpgOn = !_actionRpgOn;
            _engine.LiveUpdateActionRpg(_actionRpgOn);
            UpdateToggleVisual();
            SetDirty(true);
        }

        private void UpdateToggleVisual()
        {
            if (ToggleBg == null || ToggleThumb == null) return;
            if (_actionRpgOn)
            {
                ToggleBg.Background   = new SolidColorBrush(Color.FromRgb(0, 80, 80));
                ToggleBg.BorderBrush  = new SolidColorBrush(Color.FromRgb(0, 255, 255));
                ToggleThumb.Fill      = new SolidColorBrush(Color.FromRgb(0, 255, 255));
                ToggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
                ToggleThumb.Margin    = new Thickness(0, 0, 3, 0);
            }
            else
            {
                ToggleBg.Background   = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                ToggleBg.BorderBrush  = new SolidColorBrush(Color.FromRgb(48, 54, 61));
                ToggleThumb.Fill      = new SolidColorBrush(Color.FromRgb(85, 94, 106));
                ToggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
                ToggleThumb.Margin    = new Thickness(3, 0, 0, 0);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUTTONS
        // ══════════════════════════════════════════════════════════════════════

        private void BtnStart_Click  (object s, RoutedEventArgs e) => _engine.Start();
        private void BtnStop_Click   (object s, RoutedEventArgs e) => _engine.Stop();
        private void BtnMini_Click   (object s, RoutedEventArgs e) => SwitchToMiniMode();

        private void BtnLibrary_Click(object s, RoutedEventArgs e)
        {
            var win = new ProfileLibraryWindow(_profileManager) { Owner = this };
            win.ShowDialog();

            // Always refresh combo – user may have added/deleted profiles in the Library
            ProfileCombo.ItemsSource = null;
            ProfileCombo.ItemsSource = _profileManager.Profiles;

            // If a profile was explicitly selected ("Load"), switch to it
            if (win.SelectedProfile != null)
            {
                var match = _profileManager.Profiles.FirstOrDefault(p => p.Name == win.SelectedProfile.Name);
                if (match != null) ProfileCombo.SelectedItem = match;
            }
        }

        private void BtnRemap_Click(object s, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile p) return;
            var win = new ButtonRemappingWindow(p) { Owner = this };
            if (win.ShowDialog() == true)
            {
                // Reloaded so changes are visible
                BuildMappingTables(p);
                _engine.LoadProfile(p);
                SetDirty(true);
            }
        }

        private void BtnMacro_Click(object s, RoutedEventArgs e)
        {
            var win = new MacroRecorderWindow() { Owner = this };
            win.ShowDialog();
        }

        private void BtnWizard_Click(object s, RoutedEventArgs e)
        {
            var win = new ProfileWizardWindow() { Owner = this };
            if (win.ShowDialog() == true && win.CreatedProfile != null)
            {
                _profileManager.Profiles.Add(win.CreatedProfile);
                _profileManager.Save(win.CreatedProfile);
                ProfileCombo.ItemsSource = null;
                ProfileCombo.ItemsSource = _profileManager.Profiles;
                ProfileCombo.SelectedItem = win.CreatedProfile;
            }
        }

        private void TabBase_Click (object s, RoutedEventArgs e) => SetActiveTab("Base");
        private void TabL1_Click   (object s, RoutedEventArgs e) => SetActiveTab("L1");
        private void TabR1_Click   (object s, RoutedEventArgs e) => SetActiveTab("R1");
        private void TabL2_Click   (object s, RoutedEventArgs e) => SetActiveTab("L2");
        private void TabR2_Click   (object s, RoutedEventArgs e) => SetActiveTab("R2");
        private void TabLog_Click  (object s, RoutedEventArgs e) => SetActiveTab("Log");
        private void TabInfo_Click (object s, RoutedEventArgs e) => SetActiveTab("Info");

        // ══════════════════════════════════════════════════════════════════════
        // MINI MODE
        // ══════════════════════════════════════════════════════════════════════

        public void SwitchToMiniMode()
        {
            if (_isMiniMode) return;
            _isMiniMode = true;
            _miniWindow = new MiniModeWindow { Owner = this };
            _miniWindow.Show();
            this.Hide();
        }

        public void SwitchFromMiniMode()
        {
            if (!_isMiniMode) return;
            _isMiniMode = false;
            this.Show();
            _miniWindow?.Close();
            _miniWindow = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // FENSTER
        // ══════════════════════════════════════════════════════════════════════




        private void InitTrayIcon()
        {
            try
            {
                _trayIcon = new WinForms.NotifyIcon
                {
                    Text    = "RagnaController",
                    Visible = true
                };

                // Icon aus eingebetteter WPF-Resource laden (pack URI) —
                // funktioniert sowohl im Debug- als auch im publish-Ordner,
                // da icon.ico als <Resource> in die Assembly kompiliert wird.
                try
                {
                    var sri = Application.GetResourceStream(
                        new Uri("pack://application:,,,/Assets/icon.ico"));
                    if (sri != null)
                        _trayIcon.Icon = new System.Drawing.Icon(sri.Stream);
                    else
                        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                catch
                {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                // Double-click tray icon → restore window
                _trayIcon.DoubleClick += (_, _) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };

                // Right-click context menu
                var menu = new WinForms.ContextMenuStrip();
                menu.Items.Add("Show RagnaController", null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
                menu.Items.Add("-");
                menu.Items.Add("Exit",                 null, (_, _) => Close());
                _trayIcon.ContextMenuStrip = menu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Tray icon error: {ex.Message}");
            }
        }

        private void QuickSwitchProfile(int delta)
        {
            int count = ProfileCombo.Items.Count;
            if (count == 0) return;
            int next = (ProfileCombo.SelectedIndex + delta + count) % count;
            ProfileCombo.SelectedIndex = next;
            if (ProfileCombo.SelectedItem is Profile p)
            {
                _engine.LoadProfile(p);
                _engine.Log($"[QuickSwitch] → {p.Name}", LogLevel.Info, "Profile");
            }
        }

        // ── Global Hotkey registration ─────────────────────────────────────────
        private void RegisterGlobalHotkeys()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(HotkeyWndProc);
            for (int i = 1; i <= 4; i++)
                RegisterHotKey(hwnd, HOTKEY_ID_BASE + i, MOD_CONTROL, (uint)('0' + i));
            // Ctrl+F = Toggle MiniMode / FullMode
            RegisterHotKey(hwnd, HOTKEY_ID_BASE + 10, MOD_CONTROL, (uint)'F');
        }

        private void UnregisterGlobalHotkeys()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            for (int i = 1; i <= 4; i++)
                UnregisterHotKey(hwnd, HOTKEY_ID_BASE + i);
            UnregisterHotKey(hwnd, HOTKEY_ID_BASE + 10);
        }

        private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int slot = (int)wParam - HOTKEY_ID_BASE; // 1–4 or 10
                // Ctrl+F = Toggle MiniMode
                if (slot == 10)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_isMiniMode) SwitchFromMiniMode();
                        else SwitchToMiniMode();
                    });
                    handled = true;
                }
                if (slot >= 1 && slot <= 4)
                {
                    var profiles = _profileManager.Profiles;
                    int idx = slot - 1;
                    if (idx < profiles.Count)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProfileCombo.SelectedIndex = idx;
                            _engine.LoadProfile(profiles[idx]);
                        });
                        _engine.Log($"[Hotkey] Ctrl+{slot} → {profiles[idx].Name}", LogLevel.Info, "Hotkey");
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void Window_Closing(object s, System.ComponentModel.CancelEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show(
                    "There are unsaved changes to the current profile.\n\nSave now?",
                    "RagnaController", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    BtnSave_Click(s, new RoutedEventArgs());
            }
            // Save window position
            _settings.WindowPosition = $"{Left},{Top}";
            _settings.Save();
            // Finalize log file — wrap in try/catch so a disk error never blocks Stop()
            try { _engine.Logger.ExportSession(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Shutdown] ExportSession failed: {ex.Message}"); }
            _engine.Stop();
            _trayIcon?.Dispose();
            UnregisterGlobalHotkeys();
            _trayIcon = null;
        }

        private void BtnClearLog_Click(object s, RoutedEventArgs e)
        {
            _logBuffer.Clear();
            _engine.Logger.ClearBuffer();
            LogTextBlock.Text = string.Empty;
        }

        private void BtnExportLog_Click(object s, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt",
                    FileName = $"RagnaController_{DateTime.Now:yyyy-MM-dd_HH-mm}.log",
                    DefaultExt = ".log"
                };
                if (dialog.ShowDialog() == true)
                {
                    var path = _engine.Logger.ExportSession();
                    // Copy to user-chosen location
                    System.IO.File.Copy(path, dialog.FileName, overwrite: true);
                    MessageBox.Show($"Log saved:\n{dialog.FileName}", "Export",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void RestoreWindowPosition()
        {
            if (string.IsNullOrEmpty(_settings.WindowPosition)) return;
            try
            {
                var parts = _settings.WindowPosition.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double left) &&
                    double.TryParse(parts[1], out double top))
                {
                    // Ensure window is on a visible monitor
                    var screen = System.Windows.SystemParameters.WorkArea;
                    if (left >= 0 && top >= 0 &&
                        left < screen.Width - 100 &&
                        top  < screen.Height - 100)
                    {
                        Left = left;
                        Top  = top;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Window position error: {ex.Message}"); }
        }

        private void UpdateBatteryDisplay(string level)
        {
            string icon = level switch
            {
                "Full"   => "🔋 FULL",
                "Medium" => "🔋 MEDIUM",
                "Low"    => "⚠ LOW",
                "Empty"  => "❌ EMPTY",
                "Wired"  => "🔌 WIRED",
                _        => ""
            };
            // StatusBattery is optional – only update if present
            if (StatusBattery != null)
            {
                StatusBattery.Text = icon;
                StatusBattery.Foreground = new SolidColorBrush(
                    level == "Low" || level == "Empty"
                        ? Color.FromRgb(255, 68, 68)
                        : Color.FromRgb(57, 255, 20));
            }
        }

        private void BtnMinimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click   (object s, RoutedEventArgs e) => Close();

        // Stubs
        private void BtnSettings_Click(object s, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings, ApplySettings) { Owner = this };
            win.ShowDialog();
        }

        private void ApplySettings(Models.Settings s)
        {
            // Feedback
            _engine.SetSoundEnabled (s.SoundEnabled);
            _engine.SetRumbleEnabled(s.RumbleEnabled);

            // Stick-Visualisierung
            LeftStickCanvas.Visibility  = s.ShowControllerViz ? Visibility.Visible : Visibility.Hidden;
            RightStickCanvas.Visibility = s.ShowControllerViz ? Visibility.Visible : Visibility.Hidden;

            // AdvancedLogger Level
            _engine.Logger.MinimumLevel = (Core.LogLevel)s.LogLevel;
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            // Starts after 3 seconds so the UI is fully loaded first
            await System.Threading.Tasks.Task.Delay(3000);
            await Core.UpdateChecker.CheckAndNotifyAsync(this);
        }

        private void MenuExit_Click     (object s, RoutedEventArgs e) => Close();
        private void MenuMiniMode_Click (object s, RoutedEventArgs e) => SwitchToMiniMode();
    }
}
