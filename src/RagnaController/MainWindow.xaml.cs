using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RagnaController.Core;
using RagnaController.Models;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class MainWindow : Window
    {
        private readonly HybridEngine   _engine   = new();
        private readonly ProfileManager _manager  = new();
        private readonly Settings       _settings = Settings.Load();
        private bool _isRenewal = true, _isMiniMode = false, _actionRpgOn = true, _suppressSliders = true;
        private bool _isDirty = false;
        private ControllerPreview? _livePreview;
        private string? _lastHighlight;
        private readonly List<string> _logBuffer = new();
        private MiniModeWindow? _miniWindow;

        public MainWindow()
        {
            InitializeComponent();

            // --- Admin-Warnung ---
            if (!IsRunningAsAdmin())
                AdminWarningBanner.Visibility = System.Windows.Visibility.Visible;

            // --- Status ---
            _engine.StatusChanged += (s) => Dispatcher.Invoke(() =>
            {
                if (StatusText != null)
                {
                    StatusText.Text       = s == EngineStatus.Running ? "RUNNING"
                                         : s == EngineStatus.NoController ? "NO CONTROLLER"
                                         : "PAUSED";
                    StatusText.Foreground = s == EngineStatus.Running ? Brushes.Lime : Brushes.OrangeRed;
                }
                // Status-Dot + AutoStatusText in Toolbar
                if (AutoStatusText != null)
                {
                    if (s == EngineStatus.Running)
                        AutoStatusText.Text = _engine.FocusLockEnabled && _engine.IsFocusLocked
                            ? "Running — ⛔ switch to RO"
                            : _engine.ControllerName + " — active";
                    else
                        AutoStatusText.Text = s == EngineStatus.Stopped ? "Paused"
                                                                         : "Waiting for controller…";
                }
                if (StatusDot != null)
                {
                    StatusDot.Fill = s == EngineStatus.Running
                        ? Brushes.Lime
                        : s == EngineStatus.Stopped ? Brushes.Orange
                        : new SolidColorBrush(Color.FromRgb(85, 94, 106));
                }
                if (BtnPause != null)
                {
                    BtnPause.IsEnabled = s == EngineStatus.Running || s == EngineStatus.Stopped;
                    bool paused = _engine.IsPaused;
                    // Update label text — preserves the SVG icon StackPanel structure
                    if (BtnPauseLabel != null) BtnPauseLabel.Text = paused ? "Resume" : "Pause";
                    // Swap icon: play triangle for Resume, pause bars for Pause
                    if (BtnPauseIcon  != null) BtnPauseIcon.Data  = System.Windows.Media.Geometry.Parse(
                        paused ? "M4 2L13 8L4 14Z" : "M5 2V14M11 2V14");
                    BtnPause.Foreground = paused
                        ? System.Windows.Media.Brushes.Lime
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 148, 158));
                }
            });

            // --- Snapshot ---
            _engine.SnapshotUpdated += (snap) => Dispatcher.Invoke(() =>
            {
                if (_isMiniMode && _miniWindow != null)
                    _miniWindow.UpdateState(
                        ProfileCombo?.SelectedItem is Profile p ? p.Name : "-",
                        snap.StateLabel, _engine.IsRunning, snap.CombatState);

                if (StatusLayer  != null)  StatusLayer.Text  = snap.LayerText;
                if (StatusPanic  != null) { StatusPanic.Text  = snap.PanicActive  ? "🆘 PANIC"    : "◌ PANIC OFF";  StatusPanic.Foreground  = snap.PanicActive  ? Brushes.Red    : Brushes.Gray; }
                if (StatusVacuum != null) { StatusVacuum.Text = snap.VacuumActive ? "🌀 VACUUM"   : "◌ VACUUM OFF"; StatusVacuum.Foreground = snap.VacuumActive ? Brushes.Cyan   : Brushes.Gray; }
                if (StatusCombo  != null) { StatusCombo.Text  = snap.ComboActive  ? $"⚡ {snap.ComboLabel}" : "◌ COMBO OFF"; StatusCombo.Foreground = snap.ComboActive ? Brushes.Yellow : Brushes.Gray; }
                if (TickLatencyText != null)
                {
                    string dpiInfo = snap.WindowTracked
                        ? $" | RO {snap.WindowDpiScale:F2}x DPI"
                        : " | RO: not found";
                    TickLatencyText.Text = snap.TickMs.ToString("F1") + "ms" + dpiInfo;
                }

                // Show FOCUS LOCK status in the status bar — clear when unlocked
                if (_engine.FocusLockEnabled && _engine.IsFocusLocked)
                {
                    if (AutoStatusText != null) AutoStatusText.Text = "⛔ FOCUS LOCK — switch to RO";
                    if (StatusDot != null) StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0x99, 0x00));
                }
                else if (_engine.IsRunning)
                {
                    // Restore normal running state colours if FocusLock just cleared
                    if (StatusDot != null) StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0x76));
                }

                if (LeftStickDot  != null) { Canvas.SetLeft(LeftStickDot,  20 + snap.LeftX  * 20); Canvas.SetTop(LeftStickDot,  20 - snap.LeftY  * 20); }
                if (RightStickDot != null) { Canvas.SetLeft(RightStickDot, 20 + snap.RightX * 20); Canvas.SetTop(RightStickDot, 20 - snap.RightY * 20); }

                // Live controller preview — highlight active layer button
                if (_livePreview != null)
                {
                    string h = snap.L1 ? "LeftShoulder" : snap.R1 ? "RightShoulder"
                             : snap.L2 ? "LeftTrigger"  : snap.R2 ? "RightTrigger" : "";
                    if (h != _lastHighlight)
                    {
                        if (!string.IsNullOrEmpty(h)) _livePreview.HighlightButton(h);
                        else _livePreview.HighlightButton(""); // clear
                        _lastHighlight = h;
                    }
                }
            });

            // --- ProfileQuickSwitch (Start+DPad) ---
            _engine.ProfileQuickSwitch += delta => Dispatcher.Invoke(() =>
            {
                if (_manager.Profiles.Count == 0) return;
                int idx  = _manager.Profiles.IndexOf(ProfileCombo.SelectedItem as Profile ?? _manager.Profiles[0]);
                int next = (idx + delta + _manager.Profiles.Count) % _manager.Profiles.Count;
                ProfileCombo.SelectedItem = _manager.Profiles[next];
            });

            // --- Log-Tab ---
            _engine.LogMessage += msg => Dispatcher.Invoke(() =>
            {
                if (LogTextBlockTab == null) return;
                _logBuffer.Add(msg);
                if (_logBuffer.Count > 500) _logBuffer.RemoveRange(0, 100);
                ApplyLogFilter();
                LogScrollViewer?.ScrollToEnd();
            });

            // --- Voice Status ---
            _engine.VoiceStatusChanged += msg => Dispatcher.Invoke(() =>
            {
                if (VoiceStatusText != null)
                {
                    VoiceStatusText.Text       = msg;
                    VoiceStatusText.Foreground = msg.StartsWith("🎤")
                        ? Brushes.Lime : new SolidColorBrush(Color.FromRgb(85, 94, 106));
                }
            });
            // Battery display — visual fill bar in header
            _engine.BatteryChanged += level => Dispatcher.Invoke(() =>
            {
                if (BatteryFill == null || BatteryLevelText == null) return;
                var (fillWidth, fillColor, label) = level switch
                {
                    "Full"  => (22.0, Color.FromRgb( 57, 255,  20), "Full"),
                    "High"  => (18.0, Color.FromRgb( 57, 255,  20), "High"),
                    "Mid"   => (12.0, Color.FromRgb(255, 184,   0), "Mid"),
                    "Low"   => ( 5.0, Color.FromRgb(255,  58,  82), "Low!"),
                    "Empty" => ( 2.0, Color.FromRgb(255,  58,  82), "Empty"),
                    _       => ( 0.0, Color.FromRgb( 85,  94, 106), "–")
                };
                BatteryFill.Width      = fillWidth;
                BatteryFill.Background = new SolidColorBrush(fillColor);
                BatteryLevelText.Text       = label;
                BatteryLevelText.Foreground = new SolidColorBrush(fillColor);
            });

            // --- Controller-Name + Disconnect ---
            _engine.ControllerConnected += name => Dispatcher.Invoke(() =>
            {
                if (ControllerNameText != null)
                {
                    ControllerNameText.Text       = name;
                    ControllerNameText.Foreground = Brushes.Lime;
                    ControllerNameText.Tag        = true;
                }
            });
            _engine.ControllerDisconnected += () => Dispatcher.Invoke(() =>
            {
                if (ControllerNameText != null)
                {
                    ControllerNameText.Text       = "No Controller";
                    ControllerNameText.Foreground = new SolidColorBrush(Color.FromRgb(85, 94, 106));
                    ControllerNameText.Tag        = false;
                }
                if (BatteryFill     != null) BatteryFill.Width = 0;
                if (BatteryLevelText != null) BatteryLevelText.Text = "–";
            });

            // Controller escape from Mini-Mode click-through trap (Start + Back)
            _engine.RestoreMainWindowRequested += () => Dispatcher.Invoke(() =>
            {
                if (_isMiniMode) SwitchFromMiniMode();
            });
            this.Loaded += (s, e) =>
            {
                _suppressSliders = true;
                ProfileCombo.ItemsSource = _manager.Profiles;
                var lastP = _manager.Profiles.FirstOrDefault(p => p.Name == _settings.LastProfileName);
                ProfileCombo.SelectedItem = lastP ?? _manager.Profiles.FirstOrDefault();
                if (GameModeCombo != null)
                {
                    foreach (ComboBoxItem item in GameModeCombo.Items)
                        if (item.Tag?.ToString() == _settings.LastGameMode) { GameModeCombo.SelectedItem = item; break; }
                }
                if (PreviewContainer != null)
                {
                    PreviewContainer.Children.Clear();
                    var livePreview = new ControllerPreview();
                    PreviewContainer.Children.Add(livePreview);
                    _livePreview = livePreview;
                }
                _engine.SetSoundEnabled(_settings.SoundEnabled);
                _engine.SetRumbleEnabled(_settings.RumbleEnabled);
                _engine.FocusLockEnabled = _settings.FocusLockEnabled;
                _engine.FocusLockProcess = _settings.FocusLockProcess;
                _suppressSliders = false;
                UpdateToggleVisual();
                UpdateEngine();

                // StartInMiniMode
                if (_settings.StartInMiniMode) SwitchToMiniMode();

                // Update-Check im Hintergrund
                _ = Core.UpdateChecker.CheckAndNotifyAsync(this);
            };
        }

        // --------------------------------------------------------
        //  Navigation / Window
        // --------------------------------------------------------
        public void SwitchFromMiniMode()
        {
            _isMiniMode = false;
            Show(); WindowState = WindowState.Normal; Activate();
            _miniWindow = null;
        }

        private void SwitchToMiniMode()
        {
            if (_isMiniMode) return;
            _isMiniMode = true;
            _miniWindow = new MiniModeWindow { Owner = this };
            _miniWindow.Show();
            Hide();
        }

        private void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnMinimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object s, RoutedEventArgs e)    => Close();

        private void Window_Closing(object s, System.ComponentModel.CancelEventArgs e)
        {
            _engine.Shutdown();
            if (ProfileCombo?.SelectedItem is Profile p)     _settings.LastProfileName = p.Name;
            if (GameModeCombo?.SelectedItem is ComboBoxItem ci) _settings.LastGameMode = ci.Tag?.ToString() ?? "Ren";
            _settings.Save();
        }

        // --------------------------------------------------------
        // Profile / Engine
        // --------------------------------------------------------
        private void GameModeCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (!_suppressSliders && GameModeCombo?.SelectedItem is ComboBoxItem i)
            {
                _isRenewal = i.Tag?.ToString() == "Ren";
                _engine.ApplyGameMode(_isRenewal);
                UpdateEngine();
            }
        }

        private void ProfileCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (!_suppressSliders && ProfileCombo?.SelectedItem is Profile p) UpdateEngine();
        }

        private void UpdateEngine()
        {
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                _suppressSliders = true;
                if (SensitivitySlider  != null) SensitivitySlider.Value  = p.CursorMaxSpeed;
                if (DeadzoneSlider     != null) DeadzoneSlider.Value     = p.Deadzone;
                if (CurveSlider        != null) CurveSlider.Value        = p.MovementCurve;
                if (ActionSpeedSlider  != null) ActionSpeedSlider.Value  = p.ActionSpeed;
                UpdateDeadzoneRing((float)p.Deadzone);
                _suppressSliders = false;
                _engine.LoadProfile(p);
                if (ClassInfoText != null) ClassInfoText.Text = p.Class.ToUpper() + " (" + (_isRenewal ? "RE" : "PRE") + ")";
                if (StatusProfile != null) StatusProfile.Text = p.Name;
                if (ClassBadgeText != null)
                {
                    string icon = p.Class?.ToLower() switch
                    {
                        "mage" or "wizard" or "sage" or "high wizard" or "professor" => "✨",
                        "archer" or "hunter" or "sniper" or "bard" or "dancer" or "clown" or "gypsy" => "🏹",
                        "thief" or "assassin" or "rogue" or "assassin cross" or "stalker" => "🗡",
                        "acolyte" or "priest" or "monk" or "high priest" or "champion" => "🛡",
                        "merchant" or "blacksmith" or "alchemist" or "whitesmith" or "creator" => "⚒",
                        "gunslinger" => "🔫",
                        "ninja" => "🌙",
                        _ => "⚔"
                    };
                    ClassBadgeText.Text = $"{icon} {p.Class?.ToUpper() ?? "–"}";
                }
                if (InfoClassName != null) InfoClassName.Text    = p.Name;
                if (InfoClassType != null) InfoClassType.Text    = p.Class?.ToUpper() ?? "–";
                if (InfoTips      != null) InfoTips.Text         = p.ClassTips;
                if (InfoSkillList != null) InfoSkillList.ItemsSource = p.SkillRecommendations;
                BuildMappingTables(p);
                SetTab("Base"); // reset to BASE tab on profile switch
            }
        }

        private void BuildMappingTables(Profile p)
        {
            if (MappingsBase == null) return;
            MappingsBase.Children.Clear(); MappingsL1.Children.Clear(); MappingsR1.Children.Clear();
            MappingsL2.Children.Clear();   MappingsR2.Children.Clear();
            foreach (var kv in p.ButtonMappings.OrderBy(k => k.Key))
            {
                var row = MakeMappingRow(kv.Key, kv.Value);
                if      (kv.Key.StartsWith("L1+")) MappingsL1.Children.Add(row);
                else if (kv.Key.StartsWith("R1+")) MappingsR1.Children.Add(row);
                else if (kv.Key.StartsWith("L2+")) MappingsL2.Children.Add(row);
                else if (kv.Key.StartsWith("R2+")) MappingsR2.Children.Add(row);
                else                               MappingsBase.Children.Add(row);
            }
        }

        private UIElement MakeMappingRow(string k, ButtonAction a)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(new TextBlock { Text = k, Foreground = Brushes.Cyan, FontSize = 11 });

            string label = a.TurboEnabled ? $"{a.Label} ⚡" : a.Label;
            var fg  = a.TurboEnabled
                ? new SolidColorBrush(Color.FromRgb(229, 184, 66))  // Gold for turbo
                : Brushes.White;
            var lbl = new TextBlock { Text = label, Foreground = fg, FontSize = 11 };
            if (a.TurboEnabled)
                lbl.ToolTip = $"Turbo active — {1000.0 / Math.Max(a.TurboIntervalMs, 1):F1}/sec ({a.TurboIntervalMs}ms)";

            Grid.SetColumn(lbl, 1);
            g.Children.Add(lbl);
            return g;
        }

        // --------------------------------------------------------
        //  Toolbar Buttons
        // --------------------------------------------------------
        private void BtnPause_Click(object s, RoutedEventArgs e)
        {
            if (_engine.IsPaused) _engine.Resume();
            else                  _engine.Pause();
        }

        private void ResetBtn_MouseEnter(object s, MouseEventArgs e)
        { if (s is System.Windows.Controls.Button b) b.Foreground = Brushes.White; }
        private void ResetBtn_MouseLeave(object s, MouseEventArgs e)
        { if (s is System.Windows.Controls.Button b) b.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x45, 0x60)); }

        private void BtnRemap_Click(object s, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is Profile p)
            {
                new ButtonRemappingWindow(p).ShowDialog();
                _manager.SaveProfile(p);
                ClearDirty();
                BuildMappingTables(p);
            }
        }

        private void BtnMacro_Click(object s, RoutedEventArgs e) => new MacroRecorderWindow().ShowDialog();

        private void BtnRadial_Click(object s, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is Profile p)
            {
                var win = new RadialSetupWindow(p.RadialMenuItems) { Owner = this };
                if (win.ShowDialog() == true) _manager.SaveProfile(p);
            }
        }

        // --- Combo-Sequenz-Editor ---
        private void BtnCombo_Click(object s, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is Profile p)
            {
                var win = new ComboEditorWindow(p) { Owner = this };
                if (win.ShowDialog() == true)
                {
                    _manager.SaveProfile(p);
                    _engine.LoadProfile(p); // Apply updated combo sequence live
                }
            }
        }

        // Profile Library
        private void BtnLibrary_Click(object s, RoutedEventArgs e)
        {
            var win = new ProfileLibraryWindow(_manager) { Owner = this };
            if (win.ShowDialog() == true && win.SelectedProfile != null)
            {
                // Load profile from library and select it in the combo box
                var existing = _manager.Profiles.FirstOrDefault(p => p.Name == win.SelectedProfile.Name);
                if (existing != null) ProfileCombo.SelectedItem = existing;
            }
        }

        private void BtnWizard_Click(object s, RoutedEventArgs e)
        {
            var w = new ProfileWizardWindow();
            if (w.ShowDialog() == true && w.CreatedProfile != null)
            {
                _manager.AddAndSave(w.CreatedProfile);
                ProfileCombo.ItemsSource = null;
                ProfileCombo.ItemsSource = _manager.Profiles;
                ProfileCombo.SelectedItem = _manager.Profiles.LastOrDefault();
            }
        }

        private void BtnSettings_Click(object s, RoutedEventArgs e)
        {
            new SettingsWindow(_settings, saved =>
            {
                _engine.SetSoundEnabled(saved.SoundEnabled);
                _engine.SetRumbleEnabled(saved.RumbleEnabled);
                _engine.FocusLockEnabled = saved.FocusLockEnabled;
                _engine.FocusLockProcess = saved.FocusLockProcess;
            }).ShowDialog();
        }

        private void BtnMini_Click(object s, RoutedEventArgs e) => SwitchToMiniMode();

        // --------------------------------------------------------
        //  Toggles + Sliders
        // --------------------------------------------------------
        private void MoveModeToggle_Click(object s, MouseButtonEventArgs e)
        {
            _actionRpgOn = !_actionRpgOn;
            _engine.LiveUpdateActionRpg(_actionRpgOn);
            UpdateToggleVisual();
        }

        private void UpdateToggleVisual()
        {
            if (ToggleBg == null || ToggleThumb == null) return;
            if (_actionRpgOn)
            {
                ToggleBg.Background             = new SolidColorBrush(Color.FromRgb(0, 80, 80));
                ToggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
                ToggleThumb.Margin              = new Thickness(0, 0, 3, 0);
                ToggleThumb.Fill                = Brushes.Cyan;
            }
            else
            {
                ToggleBg.Background             = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                ToggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
                ToggleThumb.Margin              = new Thickness(3, 0, 0, 0);
                ToggleThumb.Fill                = new SolidColorBrush(Color.FromRgb(85, 94, 106));
            }
        }

        private void DeadzoneSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DeadzoneValue != null) DeadzoneValue.Text = e.NewValue.ToString("F2");
            UpdateDeadzoneRing((float)e.NewValue);
            if (!_suppressSliders) { _engine.LiveUpdateDeadzone((float)e.NewValue); MarkDirty(); }
        }

        // Deadzone ring: the visualizer is 50×50px and represents -1..+1 range (radius = 25px).
        // ring diameter = deadzone * 2 * 25 = deadzone * 50
        private void UpdateDeadzoneRing(float deadzone)
        {
            double diameter = Math.Max(4, deadzone * 50.0);
            if (LeftDeadzoneRing  != null) { LeftDeadzoneRing.Width  = diameter; LeftDeadzoneRing.Height  = diameter; }
            if (RightDeadzoneRing != null) { RightDeadzoneRing.Width = diameter; RightDeadzoneRing.Height = diameter; }
        }
        private void CurveSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)       { if (CurveValue       != null) CurveValue.Text       = e.NewValue.ToString("F2"); if (!_suppressSliders) { _engine.LiveUpdateCurve((float)e.NewValue);          MarkDirty(); } }
        private void ActionSpeedSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (ActionSpeedValue != null) ActionSpeedValue.Text = e.NewValue.ToString("F2"); if (!_suppressSliders) { _engine.LiveUpdateActionSpeed((float)e.NewValue);    MarkDirty(); } }
        private void SensitivitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (SensitivityValue != null) SensitivityValue.Text = e.NewValue.ToString("F0"); if (!_suppressSliders) { _engine.LiveUpdateCursorSpeed((float)e.NewValue);   MarkDirty(); } }

        // Double-click slider label to reset to profile default
        private void DeadzoneLabel_DblClick(object s, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                _suppressSliders = true;
                DeadzoneSlider.Value = p.Deadzone;
                _suppressSliders = false;
                _engine.LiveUpdateDeadzone((float)p.Deadzone);
                UpdateDeadzoneRing((float)p.Deadzone);
            }
        }
        private void CurveLabel_DblClick(object s, MouseButtonEventArgs e)       { if (e.ClickCount != 2) return; if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; CurveSlider.Value        = p.MovementCurve;   _suppressSliders = false; _engine.LiveUpdateCurve((float)p.MovementCurve); } }
        private void ActionSpeedLabel_DblClick(object s, MouseButtonEventArgs e) { if (e.ClickCount != 2) return; if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; ActionSpeedSlider.Value  = p.ActionSpeed;     _suppressSliders = false; _engine.LiveUpdateActionSpeed((float)p.ActionSpeed); } }
        private void SensitivityLabel_DblClick(object s, MouseButtonEventArgs e) { if (e.ClickCount != 2) return; if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; SensitivitySlider.Value  = p.CursorMaxSpeed;  _suppressSliders = false; _engine.LiveUpdateCursorSpeed((float)p.CursorMaxSpeed); } }

        // ↺ Reset button Click handlers (RoutedEventArgs — separate from the double-click TextBlock handlers)
        private void DeadzoneReset_Click(object s, RoutedEventArgs e)     { if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; DeadzoneSlider.Value     = p.Deadzone;        _suppressSliders = false; _engine.LiveUpdateDeadzone((float)p.Deadzone);         UpdateDeadzoneRing((float)p.Deadzone); } }
        private void CurveReset_Click(object s, RoutedEventArgs e)        { if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; CurveSlider.Value        = p.MovementCurve;   _suppressSliders = false; _engine.LiveUpdateCurve((float)p.MovementCurve); } }
        private void ActionSpeedReset_Click(object s, RoutedEventArgs e)  { if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; ActionSpeedSlider.Value  = p.ActionSpeed;     _suppressSliders = false; _engine.LiveUpdateActionSpeed((float)p.ActionSpeed); } }
        private void SensitivityReset_Click(object s, RoutedEventArgs e)  { if (ProfileCombo?.SelectedItem is Profile p) { _suppressSliders = true; SensitivitySlider.Value  = p.CursorMaxSpeed;  _suppressSliders = false; _engine.LiveUpdateCursorSpeed((float)p.CursorMaxSpeed); } }

        // ── Dirty flag (unsaved changes) ────────────────────────
        private void MarkDirty()
        {
            if (_isDirty) return;
            _isDirty = true;
            if (ProfileCombo?.SelectedItem is Profile p && ClassBadgeText != null)
                ClassBadgeText.Text = ClassBadgeText.Text.TrimEnd('*', ' ') + " *";
        }

        private void ClearDirty()
        {
            _isDirty = false;
            // Sternchen entfernen
            if (ClassBadgeText != null)
                ClassBadgeText.Text = ClassBadgeText.Text.TrimEnd('*', ' ');
        }

        // ── Admin-Check ─────────────────────────────────────────────────
        private static bool IsRunningAsAdmin()
        {
            try
            {
                var id  = System.Security.Principal.WindowsIdentity.GetCurrent();
                var pri = new System.Security.Principal.WindowsPrincipal(id);
                return pri.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void BtnRestartAsAdmin_Click(object s, RoutedEventArgs e)
        {
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exe == null) return;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe, UseShellExecute = true, Verb = "runas"
                });
                Application.Current.Shutdown();
            }
            catch { /* Nutzer hat UAC abgebrochen */ }
        }

        // --------------------------------------------------------
        //  Tabs
        // --------------------------------------------------------
        private void TabBase_Click(object s, RoutedEventArgs e) => SetTab("Base");
        private void TabL1_Click(object s, RoutedEventArgs e)   => SetTab("L1");
        private void TabR1_Click(object s, RoutedEventArgs e)   => SetTab("R1");
        private void TabL2_Click(object s, RoutedEventArgs e)   => SetTab("L2");
        private void TabR2_Click(object s, RoutedEventArgs e)   => SetTab("R2");
        private void TabInfo_Click(object s, RoutedEventArgs e) => SetTab("Info");
        private void TabLog_Click(object s, RoutedEventArgs e)  => SetTab("Log");

        private void SetTab(string t)
        {
            if (PanelBase == null) return;
            PanelBase.Visibility = t == "Base" ? Visibility.Visible : Visibility.Collapsed;
            PanelL1.Visibility   = t == "L1"   ? Visibility.Visible : Visibility.Collapsed;
            PanelR1.Visibility   = t == "R1"   ? Visibility.Visible : Visibility.Collapsed;
            PanelL2.Visibility   = t == "L2"   ? Visibility.Visible : Visibility.Collapsed;
            PanelR2.Visibility   = t == "R2"   ? Visibility.Visible : Visibility.Collapsed;
            PanelInfo.Visibility = t == "Info" ? Visibility.Visible : Visibility.Collapsed;
            PanelLog.Visibility  = t == "Log"  ? Visibility.Visible : Visibility.Collapsed;

            // Update tab button styles — active tab gets gold underline, others get ghost style
            var normal = (Style)FindResource("TabButton");
            var active = (Style)FindResource("TabButtonActive");
            if (TabBtnBase != null) TabBtnBase.Style = t == "Base" ? active : normal;
            if (TabBtnL1   != null) TabBtnL1.Style   = t == "L1"   ? active : normal;
            if (TabBtnR1   != null) TabBtnR1.Style   = t == "R1"   ? active : normal;
            if (TabBtnL2   != null) TabBtnL2.Style   = t == "L2"   ? active : normal;
            if (TabBtnR2   != null) TabBtnR2.Style   = t == "R2"   ? active : normal;
            if (TabBtnInfo != null) TabBtnInfo.Style  = t == "Info" ? active : normal;
            if (TabBtnLog  != null) TabBtnLog.Style   = t == "Log"  ? active : normal;
        }

        // --------------------------------------------------------
        //  Log Tab — Clear + Export
        // --------------------------------------------------------
        private void BtnLogClear_Click(object s, RoutedEventArgs e)
        {
            _logBuffer.Clear();
            if (LogTextBlockTab != null) LogTextBlockTab.Text = "";
        }

        private void LogFilter_Changed(object s, RoutedEventArgs e) => ApplyLogFilter();

        private void ApplyLogFilter()
        {
            if (LogTextBlockTab == null) return;
            bool showEngine  = LogFilterEngine?.IsChecked  == true;
            bool showInput   = LogFilterInput?.IsChecked   == true;
            bool showProfile = LogFilterProfile?.IsChecked == true;
            bool showErrors  = LogFilterErrors?.IsChecked  == true;

            var filtered = _logBuffer.Where(line =>
            {
                if (showErrors  && (line.Contains("ERR") || line.Contains("⚠") || line.Contains("disconnected"))) return true;
                if (showProfile && (line.Contains("Profil") || line.Contains("profil") || line.Contains("Layer") || line.Contains("Controller verbunden"))) return true;
                if (showInput   && (line.Contains("Taste") || line.Contains("Click") || line.Contains("Key") || line.Contains("Turbo"))) return true;
                if (showEngine  && !(line.Contains("Profil") || line.Contains("Taste") || line.Contains("ERR") || line.Contains("⚠"))) return true;
                return false;
            });

            LogTextBlockTab.Text = string.Join("\n", filtered);
        }

        private void BtnLogExport_Click(object s, RoutedEventArgs e)
        {
            string text = LogTextBlockTab?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title  = "Export log",
                Filter = "Textdatei|*.txt",
                FileName = $"RagnaController_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };
            if (dlg.ShowDialog() == true)
            {
                try { File.WriteAllText(dlg.FileName, text); }
                catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
            }
        }
    }
}
