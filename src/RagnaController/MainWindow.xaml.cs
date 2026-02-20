using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using RagnaController.Core;
using RagnaController.Models;
using RagnaController.Profiles;
namespace RagnaController
{
    public partial class MainWindow : Window
    {
        // ── Fields ────────────────────────────────────────────────────────────────
        private readonly HybridEngine    _engine         = new();
        private readonly ProfileManager  _profileManager = new();
        private readonly Settings        _settings       = Settings.Load();
        private readonly HotkeyManager   _hotkeys        = new();
        private readonly FeedbackSystem  _feedback;
        private readonly AdvancedLogger  _logger         = new();
        private readonly MacroRecorder   _macroRecorder  = new();

        private MiniModeWindow? _miniWindow;
        private bool _isMiniMode = false;

        private readonly StringBuilder   _logBuffer      = new();
        private int                      _logLineCount   = 0;
        private const int                MaxLogLines     = 500;

        private string _activeTab = "Base";

        // ── Constructor ───────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            
            _feedback = new FeedbackSystem(_engine.Controller);
            _feedback.SoundEnabled = _settings.SoundEnabled;
            _feedback.RumbleEnabled = _settings.RumbleEnabled;
            _logger.MinimumLevel = (LogLevel)_settings.LogLevel;
            
            SubscribeEngineEvents();
            PopulateProfiles();
            SelectLastProfile();
            UpdateEngineStatus(EngineStatus.Stopped);
            SetActiveTab("Base");

            // Register hotkeys
            Loaded += async (s, e) =>
            {
                if (_hotkeys.Register(this))
                {
                    _logger.Log(LogLevel.Info, "Hotkeys", "Global hotkeys registered (Ctrl+1-4)");
                }
                _hotkeys.ProfileHotkeyPressed += OnProfileHotkeyPressed;
                
                if (_settings.StartInMiniMode)
                    SwitchToMiniMode();
                    
                // Check for updates (async, non-blocking)
                _ = CheckForUpdatesAsync();
            };

            if (_settings.AutoStart)
                BtnStart_Click(this, new RoutedEventArgs());
        }

        // ── Window dragging ───────────────────────────────────────────────────────
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── Engine Events ─────────────────────────────────────────────────────────
        private void SubscribeEngineEvents()
        {
            _engine.StatusChanged   += s    => Dispatcher.Invoke(() => UpdateEngineStatus(s));
            _engine.SnapshotUpdated += snap => Dispatcher.Invoke(() => UpdateControllerViz(snap));
        }

        private void UpdateEngineStatus(EngineStatus status)
        {
            bool running = status == EngineStatus.Running;

            BtnStart.IsEnabled = !running;
            BtnStop.IsEnabled  =  running;

            Color dotColor;
            string label;
            DropShadowEffect? glow;

            switch (status)
            {
                case EngineStatus.Running:
                    dotColor = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    label    = "ENGINE RUNNING";
                    glow     = new DropShadowEffect { Color = Color.FromRgb(0x3D, 0xDB, 0x6E), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
                    _feedback.Trigger(FeedbackType.Success);
                    _logger.Log(LogLevel.Info, "Engine", "Engine started");
                    break;
                case EngineStatus.NoController:
                    dotColor = Color.FromRgb(0xFF, 0xB8, 0x00);
                    label    = "NO CONTROLLER DETECTED";
                    glow     = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xB8, 0x00), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
                    _feedback.Trigger(FeedbackType.Warning);
                    _logger.Log(LogLevel.Warning, "Engine", "No controller detected");
                    break;
                default:
                    dotColor = Color.FromRgb(0xFF, 0x3A, 0x52);
                    label    = "ENGINE STOPPED";
                    glow     = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0x3A, 0x52), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
                    _logger.Log(LogLevel.Info, "Engine", "Engine stopped");
                    break;
            }

            StatusDot.Fill    = new SolidColorBrush(dotColor);
            StatusText.Text   = label;
            EnginePulse.Fill  = new SolidColorBrush(dotColor);
            EnginePulse.Effect = glow;
            StatusDot.Effect  = glow;

            Log($"[Engine] {_engine.StatusText}");
            UpdateMiniWindow();
        }

        private void UpdateControllerViz(ControllerSnapshot snap)
        {
            // Controller connected indicator with type
            ControllerDot.Fill = new SolidColorBrush(Color.FromRgb(0x3D, 0xDB, 0x6E));
            ControllerDot.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x3D, 0xDB, 0x6E), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9
            };
            
            // Display controller type in title bar
            string displayName = snap.ControllerType switch
            {
                "Xbox"          => "XBOX CONTROLLER",
                "PlayStation 4" => "PLAYSTATION 4",
                "PlayStation 5" => "PLAYSTATION 5",
                "Generic"       => snap.ControllerName.ToUpper(),
                _               => "CONTROLLER CONNECTED"
            };
            ControllerStatusText.Text = displayName;

            // Left stick (canvas is 52x52, center=26, radius=19)
            const double lR = 19.0;
            Canvas.SetLeft(LeftStickDot,  26 + snap.LeftX  * lR - 7);
            Canvas.SetTop (LeftStickDot,  26 - snap.LeftY  * lR - 7);

            // Right stick (canvas 42x42, center=21, radius=15)
            const double rR = 15.0;
            Canvas.SetLeft(RightStickDot, 21 + snap.RightX * rR - 5);
            Canvas.SetTop (RightStickDot, 21 - snap.RightY * rR - 5);

            // Layer badge
            string layerLabel;
            Color  layerColor;
            Color  layerBg;

            if (snap.L2)
            {
                layerLabel = "● L2 LAYER ACTIVE";
                layerColor = Color.FromRgb(0x3D, 0xDB, 0x6E);
                layerBg    = Color.FromRgb(0x08, 0x18, 0x0A);
            }
            else if (snap.R2)
            {
                layerLabel = "● R2 LAYER ACTIVE";
                layerColor = Color.FromRgb(0xFF, 0x3A, 0x52);
                layerBg    = Color.FromRgb(0x1A, 0x08, 0x0A);
            }
            else
            {
                layerLabel = "● BASE LAYER";
                layerColor = Color.FromRgb(0x00, 0xCF, 0xFF);
                layerBg    = Color.FromRgb(0x0A, 0x1A, 0x28);
            }

            LayerText.Text    = layerLabel;
            LayerTextColor.Color  = layerColor;
            LayerBadgeBG.Color    = layerBg;

            // Stat header values
            StatLayer.Text = snap.L2 ? "L2" : snap.R2 ? "R2" : "BASE";
            StatLayer.Foreground = new SolidColorBrush(layerColor);
            StatLayer.Effect = new DropShadowEffect
            {
                Color = layerColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7
            };

            // ── Combat State HUD ─────────────────────────────────────────────────
            if (snap.SupportPhase != SupportPhase.Idle || _engine.Support.IsActive)
                UpdateSupportHUD(snap.SupportPhase, snap.StateLabel, snap.SupportHealCount, snap.SupportRezzCount);
            else if (snap.MagePhase != MagePhase.Idle || _engine.Mage.IsActive)
                UpdateMageHUD(snap.MagePhase, snap.StateLabel, snap.MageSPWarning, snap.MageCastCount);
            else if (snap.KitePhase != KitePhase.Idle || _engine.Kite.IsActive)
                UpdateKiteHUD(snap.KitePhase, snap.StateLabel);
            else
                UpdateCombatHUD(snap.CombatState, snap.StateLabel);
        }

        private void UpdateCombatHUD(CombatState state, string label)
        {
            Color dotColor, textColor, bgColor, borderColor;
            string hint;

            switch (state)
            {
                case CombatState.Seeking:
                    dotColor   = Color.FromRgb(0xFF, 0xB8, 0x00);
                    textColor  = Color.FromRgb(0xFF, 0xB8, 0x00);
                    bgColor    = Color.FromRgb(0x14, 0x10, 0x04);
                    borderColor= Color.FromRgb(0x3A, 0x28, 0x00);
                    hint       = "Pressing Tab — scanning for monsters...";
                    break;

                case CombatState.Engaged:
                    dotColor   = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    bgColor    = Color.FromRgb(0x06, 0x14, 0x08);
                    borderColor= Color.FromRgb(0x10, 0x38, 0x18);
                    hint       = "Target locked — engaging...";
                    break;

                case CombatState.Attacking:
                    // Could be kite engine or auto-target — show kite phase if active
                    dotColor   = Color.FromRgb(0xFF, 0x3A, 0x52);
                    textColor  = Color.FromRgb(0xFF, 0x3A, 0x52);
                    bgColor    = Color.FromRgb(0x14, 0x04, 0x06);
                    borderColor= Color.FromRgb(0x38, 0x08, 0x10);
                    hint       = "Auto-attacking!  R3 = snap lock  ·  L3 = stop";
                    break;

                default: // Idle
                    dotColor   = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    bgColor    = Color.FromRgb(0x0A, 0x0E, 0x16);
                    borderColor= Color.FromRgb(0x1E, 0x28, 0x40);
                    hint       = "Press L3 to enter combat mode";
                    break;
            }

            CombatDotColor.Color   = dotColor;
            CombatStateColor.Color = textColor;
            CombatHUDBG.Color      = bgColor;
            CombatHUDBorder.Color  = borderColor;
            CombatStateText.Text   = label;
            CombatHint.Text        = hint;

            CombatDot.Effect = state == CombatState.Idle ? null : new DropShadowEffect
            {
                Color = dotColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9
            };
        }

        private void UpdateKiteHUD(KitePhase phase, string label)
        {
            Color dotColor, textColor, bgColor, borderColor;
            string hint;

            switch (phase)
            {
                case KitePhase.Locking:
                    dotColor   = Color.FromRgb(0x00, 0xCF, 0xFF);
                    textColor  = Color.FromRgb(0x00, 0xCF, 0xFF);
                    bgColor    = Color.FromRgb(0x04, 0x14, 0x1C);
                    borderColor= Color.FromRgb(0x08, 0x38, 0x50);
                    hint       = "Right-clicking to lock target...";
                    break;

                case KitePhase.Attacking:
                    dotColor   = Color.FromRgb(0xFF, 0x3A, 0x52);
                    textColor  = Color.FromRgb(0xFF, 0x3A, 0x52);
                    bgColor    = Color.FromRgb(0x14, 0x04, 0x06);
                    borderColor= Color.FromRgb(0x40, 0x08, 0x12);
                    hint       = "Firing!  R2 = hold ground  ·  L2 = retreat NOW";
                    break;

                case KitePhase.Retreating:
                    dotColor   = Color.FromRgb(0xFF, 0xB8, 0x00);
                    textColor  = Color.FromRgb(0xFF, 0xB8, 0x00);
                    bgColor    = Color.FromRgb(0x14, 0x10, 0x02);
                    borderColor= Color.FromRgb(0x40, 0x30, 0x04);
                    hint       = "Kiting backwards...  R2 = stop retreat";
                    break;

                case KitePhase.Pivoting:
                    dotColor   = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    textColor  = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    bgColor    = Color.FromRgb(0x0C, 0x08, 0x18);
                    borderColor= Color.FromRgb(0x28, 0x18, 0x48);
                    hint       = "Turning back to monster...";
                    break;

                case KitePhase.Relocking:
                    dotColor   = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    bgColor    = Color.FromRgb(0x06, 0x14, 0x08);
                    borderColor= Color.FromRgb(0x10, 0x38, 0x18);
                    hint       = "Re-locking target...";
                    break;

                default:
                    dotColor   = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    bgColor    = Color.FromRgb(0x0A, 0x0E, 0x16);
                    borderColor= Color.FromRgb(0x1E, 0x28, 0x40);
                    hint       = "Press L3 to start kiting";
                    break;
            }

            CombatDotColor.Color   = dotColor;
            CombatStateColor.Color = textColor;
            CombatHUDBG.Color      = bgColor;
            CombatHUDBorder.Color  = borderColor;
            CombatStateText.Text   = label;
            CombatHint.Text        = hint;

            CombatDot.Effect = phase == KitePhase.Idle ? null : new DropShadowEffect
            {
                Color = dotColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9
            };
        }

        private void UpdateMageHUD(MagePhase phase, string label, bool spWarning, int castCount)
        {
            Color dotColor, textColor, bgColor, borderColor;
            string hint;

            switch (phase)
            {
                case MagePhase.Aiming:
                    dotColor   = Color.FromRgb(0x00, 0xCF, 0xFF);
                    textColor  = Color.FromRgb(0x00, 0xCF, 0xFF);
                    bgColor    = Color.FromRgb(0x04, 0x14, 0x1C);
                    borderColor= Color.FromRgb(0x08, 0x38, 0x50);
                    hint       = "Ground: R3 = place  ·  Bolt: R2 + R3 = lock";
                    break;

                case MagePhase.Casting:
                    dotColor   = Color.FromRgb(0xFF, 0x3A, 0x52);
                    textColor  = Color.FromRgb(0xFF, 0x3A, 0x52);
                    bgColor    = Color.FromRgb(0x14, 0x04, 0x06);
                    borderColor= Color.FromRgb(0x40, 0x08, 0x12);
                    hint       = "Ground spell placed!  L2 = defensive";
                    break;

                case MagePhase.BoltLocked:
                case MagePhase.BoltSpamming:
                    dotColor   = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    textColor  = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    bgColor    = Color.FromRgb(0x0C, 0x08, 0x18);
                    borderColor= Color.FromRgb(0x28, 0x18, 0x48);
                    hint       = spWarning ? $"⚠ LOW SP? ({castCount} casts)  ·  L2 = defensive"
                                           : $"Bolt spamming ({castCount} casts)  ·  L2 = defensive";
                    break;

                default:
                    dotColor   = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    bgColor    = Color.FromRgb(0x0A, 0x0E, 0x16);
                    borderColor= Color.FromRgb(0x1E, 0x28, 0x40);
                    hint       = "Press L3 to enter mage mode";
                    break;
            }

            // SP warning override colors
            if (spWarning && phase != MagePhase.Idle)
            {
                dotColor    = Color.FromRgb(0xFF, 0xB8, 0x00);
                textColor   = Color.FromRgb(0xFF, 0xB8, 0x00);
                borderColor = Color.FromRgb(0x40, 0x30, 0x04);
            }

            CombatDotColor.Color   = dotColor;
            CombatStateColor.Color = textColor;
            CombatHUDBG.Color      = bgColor;
            CombatHUDBorder.Color  = borderColor;
            CombatStateText.Text   = label;
            CombatHint.Text        = hint;

            CombatDot.Effect = phase == MagePhase.Idle ? null : new DropShadowEffect
            {
                Color = dotColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9
            };
        }

        private void UpdateSupportHUD(SupportPhase phase, string label, int healCount, int rezzCount)
        {
            Color dotColor, textColor, bgColor, borderColor;
            string hint;

            switch (phase)
            {
                case SupportPhase.TargetingParty:
                    dotColor   = Color.FromRgb(0x00, 0xCF, 0xFF);
                    textColor  = Color.FromRgb(0x00, 0xCF, 0xFF);
                    bgColor    = Color.FromRgb(0x04, 0x14, 0x1C);
                    borderColor= Color.FromRgb(0x08, 0x38, 0x50);
                    hint       = $"R3 = target+heal  ·  RB = tab party  ·  {healCount} heals";
                    break;

                case SupportPhase.Healing:
                    dotColor   = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0xDB, 0x6E);
                    bgColor    = Color.FromRgb(0x06, 0x14, 0x08);
                    borderColor= Color.FromRgb(0x10, 0x38, 0x18);
                    hint       = $"Healing party  ·  {healCount} heals total";
                    break;

                case SupportPhase.SelfHealing:
                    dotColor   = Color.FromRgb(0xFF, 0xB8, 0x00);
                    textColor  = Color.FromRgb(0xFF, 0xB8, 0x00);
                    bgColor    = Color.FromRgb(0x14, 0x10, 0x02);
                    borderColor= Color.FromRgb(0x40, 0x30, 0x04);
                    hint       = "Self-heal!  L3 = emergency self-heal";
                    break;

                case SupportPhase.Rezzing:
                    dotColor   = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    textColor  = Color.FromRgb(0x9F, 0x7A, 0xFF);
                    bgColor    = Color.FromRgb(0x0C, 0x08, 0x18);
                    borderColor= Color.FromRgb(0x28, 0x18, 0x48);
                    hint       = $"Resurrection!  {rezzCount} rezzes total";
                    break;

                case SupportPhase.PlacingSanctuary:
                    dotColor   = Color.FromRgb(0xFF, 0xB8, 0x00);
                    textColor  = Color.FromRgb(0xFF, 0xB8, 0x00);
                    bgColor    = Color.FromRgb(0x14, 0x10, 0x02);
                    borderColor= Color.FromRgb(0x40, 0x30, 0x04);
                    hint       = "Sanctuary placed!  R2 + R3 = ground-target";
                    break;

                case SupportPhase.AutoCycling:
                    dotColor   = Color.FromRgb(0x3A, 0x8E, 0xFF);
                    textColor  = Color.FromRgb(0x3A, 0x8E, 0xFF);
                    bgColor    = Color.FromRgb(0x04, 0x0A, 0x14);
                    borderColor= Color.FromRgb(0x08, 0x20, 0x40);
                    hint       = "Auto-cycling party for heal check...";
                    break;

                default:
                    dotColor   = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    textColor  = Color.FromRgb(0x3D, 0x4A, 0x6E);
                    bgColor    = Color.FromRgb(0x0A, 0x0E, 0x16);
                    borderColor= Color.FromRgb(0x1E, 0x28, 0x40);
                    hint       = "Press L3 to enter support mode";
                    break;
            }

            CombatDotColor.Color   = dotColor;
            CombatStateColor.Color = textColor;
            CombatHUDBG.Color      = bgColor;
            CombatHUDBorder.Color  = borderColor;
            CombatStateText.Text   = label;
            CombatHint.Text        = hint;

            CombatDot.Effect = phase == SupportPhase.Idle ? null : new DropShadowEffect
            {
                Color = dotColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9
            };
        }

        // ── Profiles ──────────────────────────────────────────────────────────────
        private void PopulateProfiles()
        {
            ProfileCombo.ItemsSource       = _profileManager.Profiles;
            ProfileCombo.DisplayMemberPath = "Name";
        }

        private void SelectLastProfile()
        {
            var profile = _profileManager.Profiles
                .FirstOrDefault(p => p.Name == _settings.LastProfileName)
                ?? _profileManager.Profiles.FirstOrDefault();
            ProfileCombo.SelectedItem = profile;
        }

        private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile profile) return;

            SensitivitySlider.Value  = profile.MouseSensitivity;
            DeadzoneSlider.Value     = profile.Deadzone;
            CurveSlider.Value        = profile.MovementCurve;
            ActionSpeedSlider.Value  = profile.ActionSpeed;
            ApplyMovementModeUI(profile.ActionRpgMode);

            _engine.LoadProfile(profile);
            BuildMappingTables(profile);

            _settings.LastProfileName = profile.Name;
            _settings.Save();

            Log($"[Profile] Loaded: {profile.Name}");
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProfileDialog { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var profile = new Profile
            {
                Name        = dialog.ProfileName,
                Description = dialog.ProfileDescription,
                Class       = dialog.ProfileClass,
                IsBuiltIn   = false
            };

            _profileManager.Profiles.Add(profile);
            _profileManager.Save(profile);
            ProfileCombo.Items.Refresh();
            ProfileCombo.SelectedItem = profile;
            Log($"[Profile] Created: {profile.Name}");
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile profile) return;
            if (profile.IsBuiltIn)
            {
                MessageBox.Show("Built-in profiles cannot be deleted.", "RagnaController",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"Delete \"{profile.Name}\"?", "RagnaController",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _profileManager.Delete(profile);
            ProfileCombo.Items.Refresh();
            ProfileCombo.SelectedIndex = 0;
            Log($"[Profile] Deleted: {profile.Name}");
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not Profile profile) return;
            var dlg = new SaveFileDialog { Filter = "JSON Profile|*.json", FileName = profile.Name + ".json" };
            if (dlg.ShowDialog() != true) return;
            _profileManager.Export(profile, dlg.FileName);
            Log($"[Profile] Exported to {dlg.FileName}");
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON Profile|*.json" };
            if (dlg.ShowDialog() != true) return;
            var profile = _profileManager.Import(dlg.FileName);
            if (profile == null) { MessageBox.Show("Invalid profile file.", "RagnaController", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            ProfileCombo.Items.Refresh();
            ProfileCombo.SelectedItem = profile;
            Log($"[Profile] Imported: {profile.Name}");
        }

        // ── Mapping Tables ────────────────────────────────────────────────────────
        private void BuildMappingTables(Profile profile)
        {
            MappingsBase.Children.Clear();
            MappingsL2.Children.Clear();
            MappingsR2.Children.Clear();

            foreach (var kv in profile.ButtonMappings)
            {
                string key    = kv.Key;
                bool   isL2   = key.StartsWith("L2+");
                bool   isR2   = key.StartsWith("R2+");
                string button = isL2 ? key[3..] : isR2 ? key[3..] : key;

                var row = CreateMappingCard(button, kv.Value, isL2 ? "L2" : isR2 ? "R2" : "BASE");

                if      (isL2) MappingsL2.Children.Add(row);
                else if (isR2) MappingsR2.Children.Add(row);
                else           MappingsBase.Children.Add(row);
            }
        }

        private UIElement CreateMappingCard(string button, ButtonAction action, string layer)
        {
            // Determine button color
            var btnColor = button switch
            {
                "A"             => Color.FromRgb(0x3D, 0xDB, 0x6E),
                "B"             => Color.FromRgb(0xFF, 0x3A, 0x52),
                "X"             => Color.FromRgb(0x3A, 0x8E, 0xFF),
                "Y"             => Color.FromRgb(0xFF, 0xB8, 0x00),
                "LeftShoulder"  or "RightShoulder" => Color.FromRgb(0x9F, 0x7A, 0xFF),
                _ when button.StartsWith("DPad")   => Color.FromRgb(0x8B, 0x97, 0xCC),
                _                                  => Color.FromRgb(0x00, 0xCF, 0xFF)
            };

            var card = new Border
            {
                Background   = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x24)),
                BorderBrush  = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x3C)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(14, 10, 14, 10),
                Margin       = new Thickness(0, 0, 0, 5),
                Effect       = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.4 }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Button badge — colored circle with label
            var badge = new Grid { VerticalAlignment = VerticalAlignment.Center };
            badge.Children.Add(new Ellipse
            {
                Width  = 34, Height = 34,
                Fill   = new SolidColorBrush(Color.FromArgb(30,
                    btnColor.R, btnColor.G, btnColor.B)),
                Stroke = new SolidColorBrush(btnColor),
                StrokeThickness = 1.5,
                Effect = new DropShadowEffect
                {
                    Color = btnColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.5
                }
            });
            badge.Children.Add(new TextBlock
            {
                Text              = ButtonShortName(button),
                FontSize          = 9,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(btnColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(badge, 0);

            // Arrow
            var arrow = new TextBlock
            {
                Text              = "→",
                FontSize          = 16,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(arrow, 1);

            // Action label
            var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelStack.Children.Add(new TextBlock
            {
                Text       = action.Label,
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xFF))
            });
            if (action.Type == ActionType.Key)
                labelStack.Children.Add(new TextBlock
                {
                    Text       = $"Key: {action.Key}",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                    Margin     = new Thickness(0, 1, 0, 0)
                });
            Grid.SetColumn(labelStack, 2);

            // Tags
            var tags = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (action.TurboEnabled)
                tags.Children.Add(MakeTag("⚡ TURBO", Color.FromRgb(0xFF, 0xB8, 0x00),
                    Color.FromArgb(50, 0xFF, 0xB8, 0)));

            string typeLabel = action.Type switch
            {
                ActionType.LeftClick  => "LMB",
                ActionType.RightClick => "RMB",
                ActionType.Scroll     => "SCROLL",
                _                     => "KEY"
            };
            tags.Children.Add(MakeTag(typeLabel,
                Color.FromRgb(0x3D, 0x4A, 0x6E),
                Color.FromArgb(40, 0x3D, 0x4A, 0x6E)));
            Grid.SetColumn(tags, 3);

            grid.Children.Add(badge);
            grid.Children.Add(arrow);
            grid.Children.Add(labelStack);
            grid.Children.Add(tags);

            // Hover effect
            card.MouseEnter += (_, _) =>
            {
                card.BorderBrush = new SolidColorBrush(btnColor) { Opacity = 0.5 };
            };
            card.MouseLeave += (_, _) =>
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x3C));
            };

            card.Child = grid;
            return card;
        }

        private static Border MakeTag(string text, Color fg, Color bg)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding      = new Thickness(6, 2, 6, 2),
                Margin       = new Thickness(4, 0, 0, 0),
                Background   = new SolidColorBrush(bg)
            };
            b.Child = new TextBlock
            {
                Text       = text,
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(fg)
            };
            return b;
        }

        private static string ButtonShortName(string button) => button switch
        {
            "A"                => "A",
            "B"                => "B",
            "X"                => "X",
            "Y"                => "Y",
            "LeftShoulder"     => "LB",
            "RightShoulder"    => "RB",
            "DPadUp"           => "▲",
            "DPadDown"         => "▼",
            "DPadLeft"         => "◄",
            "DPadRight"        => "►",
            "Start"            => "⊞",
            "Back"             => "⊟",
            "LeftThumb"        => "L3",
            "RightThumb"       => "R3",
            _                  => button.Length > 4 ? button[..4] : button
        };

        // ── Sliders ───────────────────────────────────────────────────────────────
        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SensitivityValue == null) return;
            SensitivityValue.Text = e.NewValue.ToString("F1");
            if (StatSens != null) StatSens.Text = e.NewValue.ToString("F1");
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                p.MouseSensitivity = (float)e.NewValue;
                if (_engine.IsRunning) _engine.LoadProfile(p);
            }
        }

        private void DeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DeadzoneValue == null) return;
            DeadzoneValue.Text = e.NewValue.ToString("F2");
            if (StatDead != null) StatDead.Text = e.NewValue.ToString("F2");
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                p.Deadzone = (float)e.NewValue;
                if (_engine.IsRunning) _engine.LoadProfile(p);
            }
        }

        private void CurveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CurveValue == null) return;
            CurveValue.Text = e.NewValue.ToString("F1");
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                p.MovementCurve = (float)e.NewValue;
                if (_engine.IsRunning) _engine.LoadProfile(p);
            }
        }

        private void ActionSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ActionSpeedValue == null) return;
            ActionSpeedValue.Text = e.NewValue.ToString("F1");
            if (ProfileCombo?.SelectedItem is Profile p)
            {
                p.ActionSpeed = (float)e.NewValue;
                if (_engine.IsRunning) _engine.LoadProfile(p);
            }
        }

        private void MoveModeToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ProfileCombo?.SelectedItem is not Profile p) return;
            p.ActionRpgMode = !p.ActionRpgMode;
            ApplyMovementModeUI(p.ActionRpgMode);
            if (_engine.IsRunning) _engine.LoadProfile(p);
        }

        private void ApplyMovementModeUI(bool actionRpg)
        {
            if (MoveModeLabel == null) return;
            if (actionRpg)
            {
                MoveModeLabel.Text    = "Action RPG";
                MoveModeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCF, 0xFF));
                MoveModeDesc.Text     = "LS holds LMB + drifts cursor";
                ToggleBg.Background   = new SolidColorBrush(Color.FromRgb(0x00, 0xCF, 0xFF));
                ToggleThumb.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                ToggleBg.Effect = new DropShadowEffect { Color = Color.FromRgb(0x00, 0xCF, 0xFF), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 };
                LblActionSpeed.Visibility  = Visibility.Visible;
                ActionSpeedRow.Visibility  = Visibility.Visible;
            }
            else
            {
                MoveModeLabel.Text    = "Classic";
                MoveModeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x97, 0xCC));
                MoveModeDesc.Text     = "LS moves cursor, manual click";
                ToggleBg.Background   = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E));
                ToggleThumb.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                ToggleBg.Effect = null;
                LblActionSpeed.Visibility  = Visibility.Collapsed;
                ActionSpeedRow.Visibility  = Visibility.Collapsed;
            }
        }

        // ── Engine ────────────────────────────────────────────────────────────────
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is Profile profile)
                _engine.LoadProfile(profile);
            _engine.Start();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => _engine.Stop();

        // ── Tabs ──────────────────────────────────────────────────────────────────
        private void SetActiveTab(string tab)
        {
            _activeTab = tab;
            PanelBase.Visibility = Visibility.Collapsed;
            PanelL2.Visibility   = Visibility.Collapsed;
            PanelR2.Visibility   = Visibility.Collapsed;
            PanelLog.Visibility  = Visibility.Collapsed;

            // Reset all tab buttons to ghost
            TabBtnBase.Style = FindResource("ConsoleGhostBtn") as Style;
            TabBtnL2.Style   = FindResource("ConsoleGhostBtn") as Style;
            TabBtnR2.Style   = FindResource("ConsoleGhostBtn") as Style;
            TabBtnLog.Style  = FindResource("ConsoleGhostBtn") as Style;

            switch (tab)
            {
                case "Base":
                    PanelBase.Visibility = Visibility.Visible;
                    TabBtnBase.Style = FindResource("ConsolePrimaryBtn") as Style;
                    break;
                case "L2":
                    PanelL2.Visibility = Visibility.Visible;
                    TabBtnL2.Style = FindResource("ConsolePrimaryBtn") as Style;
                    break;
                case "R2":
                    PanelR2.Visibility = Visibility.Visible;
                    TabBtnR2.Style = FindResource("ConsolePrimaryBtn") as Style;
                    break;
                case "Log":
                    PanelLog.Visibility = Visibility.Visible;
                    TabBtnLog.Style = FindResource("ConsolePrimaryBtn") as Style;
                    break;
            }
        }

        private void TabBase_Click(object sender, RoutedEventArgs e) => SetActiveTab("Base");
        private void TabL2_Click  (object sender, RoutedEventArgs e) => SetActiveTab("L2");
        private void TabR2_Click  (object sender, RoutedEventArgs e) => SetActiveTab("R2");
        private void TabLog_Click (object sender, RoutedEventArgs e) => SetActiveTab("Log");

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logBuffer.Clear();
            _logLineCount = 0;
            if (LogTextBlock != null) LogTextBlock.Text = string.Empty;
        }

        // ── Logging ───────────────────────────────────────────────────────────────
        private void Log(string message)
        {
            if (_logLineCount >= MaxLogLines)
            {
                int nl = _logBuffer.ToString().IndexOf('\n');
                if (nl >= 0) _logBuffer.Remove(0, nl + 1);
                else _logBuffer.Clear();
                _logLineCount--;
            }
            _logBuffer.AppendLine($"[{DateTime.Now:HH:mm:ss.ff}]  {message}");
            _logLineCount++;
            if (LogTextBlock != null) LogTextBlock.Text = _logBuffer.ToString();
        }

        // ── Window ────────────────────────────────────────────────────────────────
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _engine.Stop();
            
            // Save current settings
            _settings.SoundEnabled = _feedback.SoundEnabled;
            _settings.RumbleEnabled = _feedback.RumbleEnabled;
            _settings.LogLevel = (int)_logger.MinimumLevel;
            _settings.Save();
            
            _hotkeys.Dispose();

            if (_isMiniMode && _miniWindow != null)
                _miniWindow.Close();
                
            _logger.Log(LogLevel.Info, "App", "Application closing");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+F = Toggle Mini Mode
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_isMiniMode)
                    SwitchFromMiniMode();
                else
                    SwitchToMiniMode();
                e.Handled = true;
            }
        }

        // ── Menu Handlers ─────────────────────────────────────────────────────────
        private void MenuMiniMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchToMiniMode();
        }

        private void MenuExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = _logger.ExportSession();
                MessageBox.Show($"Session log exported to:\n{path}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.Log(LogLevel.Info, "Logger", $"Session exported to {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export log:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuSound_CheckChanged(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem != null)
            {
                _feedback.SoundEnabled = menuItem.IsChecked;
                _logger.Log(LogLevel.Info, "Settings", $"Sound feedback: {menuItem.IsChecked}");
            }
        }

        private void MenuRumble_CheckChanged(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem != null)
            {
                _feedback.RumbleEnabled = menuItem.IsChecked;
                _logger.Log(LogLevel.Info, "Settings", $"Rumble feedback: {menuItem.IsChecked}");
            }
        }

        // ── Update Checking ───────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                _logger.Log(LogLevel.Info, "Update", "Checking for updates...");
                var update = await UpdateChecker.CheckForUpdatesAsync();
                
                if (update != null)
                {
                    _logger.Log(LogLevel.Info, "Update", $"Update available: {update.LatestVersion}");
                    Dispatcher.Invoke(() => UpdateChecker.ShowUpdateNotification(update));
                }
                else
                {
                    _logger.Log(LogLevel.Info, "Update", "App is up to date");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, "Update", $"Update check failed: {ex.Message}");
            }
        }

        // ── v1.3 Feature Handlers ─────────────────────────────────────────────────

        public void OpenButtonRemapper()
        {
            var profile = ProfileCombo.SelectedItem as Profile;
            if (profile == null)
            {
                MessageBox.Show("Please select a profile first", "No Profile Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new ButtonRemappingWindow(profile);
            if (window.ShowDialog() == true)
            {
                _profileManager.Save(profile);
                _logger.Log(LogLevel.Info, "Remap", $"Button mappings updated for {profile.Name}");
            }
        }

        public void OpenMacroRecorder()
        {
            var window = new MacroRecorderWindow();
            if (window.ShowDialog() == true && window.RecordedMacro != null)
            {
                _logger.Log(LogLevel.Info, "Macro", $"Macro recorded: {window.RecordedMacro.Name}");
                MessageBox.Show("Macro saved! You can now bind it to a button.", "Macro Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void OpenProfileWizard()
        {
            var window = new ProfileWizardWindow();
            if (window.ShowDialog() == true && window.CreatedProfile != null)
            {
                _profileManager.Profiles.Add(window.CreatedProfile);
                _profileManager.Save(window.CreatedProfile);
                _logger.Log(LogLevel.Info, "Wizard", $"Profile created: {window.CreatedProfile.Name}");
                
                // Refresh profile list
                PopulateProfiles();
                ProfileCombo.SelectedItem = window.CreatedProfile;
            }
        }

        public void ExportCurrentProfile()
        {
            var profile = ProfileCombo.SelectedItem as Profile;
            if (profile == null)
            {
                MessageBox.Show("Please select a profile first", "No Profile Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = $"{profile.Name}_export_{DateTime.Now:yyyy-MM-dd}.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _profileManager.Export(profile, dialog.FileName);
                    MessageBox.Show($"Profile exported to:\n{dialog.FileName}", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.Log(LogLevel.Info, "Export", $"Profile exported: {profile.Name}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ImportProfile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Import Profile"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var profile = _profileManager.Import(dialog.FileName);
                    if (profile == null)
                    {
                        MessageBox.Show("Failed to import profile", "Import Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    _logger.Log(LogLevel.Info, "Import", $"Profile imported: {profile.Name}");
                    MessageBox.Show($"Profile '{profile.Name}' imported successfully!", "Import Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    PopulateProfiles();
                    ProfileCombo.SelectedItem = profile;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed:\n{ex.Message}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ── v1.4 Feature Handlers ─────────────────────────────────────────────────

        public void OpenProfileLibrary()
        {
            var library = new ProfileLibraryWindow(_profileManager);
            if (library.ShowDialog() == true && library.SelectedProfile != null)
            {
                ProfileCombo.SelectedItem = library.SelectedProfile;
                _logger.Log(LogLevel.Info, "Library", $"Profile loaded from library: {library.SelectedProfile.Name}");
            }
        }

        public void OpenMacroEditor()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Select Macro to Edit",
                InitialDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RagnaController", "Macros")
            };

            if (dialog.ShowDialog() == true)
            {
                var editor = new MacroEditorWindow(dialog.FileName);
                editor.ShowDialog();
                _logger.Log(LogLevel.Info, "MacroEditor", $"Macro edited: {dialog.FileName}");
            }
        }

        // ── Menu Bar Handlers ─────────────────────────────────────────────────────

        // File Menu
        private void MenuNewProfile_Click(object sender, RoutedEventArgs e)
        {
            OpenProfileWizard();
        }

        private void MenuImportProfile_Click(object sender, RoutedEventArgs e)
        {
            ImportProfile();
        }

        private void MenuExportProfile_Click(object sender, RoutedEventArgs e)
        {
            ExportCurrentProfile();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Edit Menu
        private void MenuRemapButtons_Click(object sender, RoutedEventArgs e)
        {
            OpenButtonRemapper();
        }

        private void MenuEditProfile_Click(object sender, RoutedEventArgs e)
        {
            var profile = ProfileCombo.SelectedItem as Profile;
            if (profile == null)
            {
                MessageBox.Show("Please select a profile first", "No Profile Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Profile editing UI coming in future update.\nFor now, use Button Remapper or edit JSON directly.",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings dialog coming in future update.\nFor now, use View menu toggles.",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Tools Menu
        private void MenuRecordMacro_Click(object sender, RoutedEventArgs e)
        {
            OpenMacroRecorder();
        }

        private void MenuEditMacro_Click(object sender, RoutedEventArgs e)
        {
            OpenMacroEditor();
        }

        private void MenuProfileLibrary_Click(object sender, RoutedEventArgs e)
        {
            OpenProfileLibrary();
        }

        private void MenuMacroBrowser_Click(object sender, RoutedEventArgs e)
        {
            string macrosFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RagnaController", "Macros");
            
            if (System.IO.Directory.Exists(macrosFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", macrosFolder);
            }
            else
            {
                MessageBox.Show("No macros folder found.\nRecord a macro first to create the folder.",
                    "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // View Menu
        private void MenuMiniMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isMiniMode)
                SwitchFromMiniMode();
            else
                SwitchToMiniMode();
        }

        private void MenuExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exportPath = _logger.ExportSession();
                MessageBox.Show($"Session log exported to:\n{exportPath}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export log:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Help Menu
        private void MenuDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/yourusername/RagnaController/wiki",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Could not open documentation.\nPlease visit:\nhttps://github.com/yourusername/RagnaController/wiki",
                    "Documentation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuKeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            string shortcuts = @"KEYBOARD SHORTCUTS

Profile Management:
  Ctrl+1 → Switch to Profile 1 (Melee)
  Ctrl+2 → Switch to Profile 2 (Ranged)
  Ctrl+3 → Switch to Profile 3 (Mage)
  Ctrl+4 → Switch to Profile 4 (Support)

View:
  Ctrl+F → Toggle Mini Mode

General:
  F1 → Help
  Esc → Close current dialog";

            MessageBox.Show(shortcuts, "Keyboard Shortcuts",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            _ = CheckForUpdatesAsync();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string about = @"RAGNA CONTROLLER v1.4

Universal gamepad controller for Ragnarok Online

Features:
• 4 Combat Engines (Melee/Ranged/Mage/Support)
• Universal Controller Support (Xbox/PS4/PS5)
• Macro Recording & Playback
• Profile Management
• Button Remapping
• Live Controller Preview

Built with .NET 8 + WPF
© 2026 - Licensed under MIT

For updates and documentation:
github.com/yourusername/RagnaController";

            MessageBox.Show(about, "About RagnaController",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Hotkey Handling ───────────────────────────────────────────────────────
        private void OnProfileHotkeyPressed(int profileIndex)
        {
            if (profileIndex < 1 || profileIndex > _profileManager.Profiles.Count) return;

            var profile = _profileManager.Profiles[profileIndex - 1];
            ProfileCombo.SelectedItem = profile;
            
            _feedback.Trigger(FeedbackType.ProfileSwitched);
            _logger.Log(LogLevel.Info, "Hotkey", $"Switched to profile: {profile.Name}");
            
            Log($"[Hotkey] Ctrl+{profileIndex} → {profile.Name}");
        }

        // ── Mini Mode ─────────────────────────────────────────────────────────────
        public void SwitchToMiniMode()
        {
            if (_isMiniMode) return;

            _isMiniMode = true;
            _miniWindow = new MiniModeWindow
            {
                Left = this.Left + (this.Width - 280) / 2,
                Top = this.Top + 50
            };

            _miniWindow.Show();
            this.Hide();

            _logger.Log(LogLevel.Info, "UI", "Switched to Mini Mode");
        }

        public void SwitchFromMiniMode()
        {
            if (!_isMiniMode) return;

            _isMiniMode = false;
            this.Show();
            
            if (_miniWindow != null)
            {
                _miniWindow.Close();
                _miniWindow = null;
            }

            _logger.Log(LogLevel.Info, "UI", "Switched to Full Mode");
        }

        // Update mini window if active
        private void UpdateMiniWindow()
        {
            if (!_isMiniMode || _miniWindow == null) return;

            var profile = ProfileCombo.SelectedItem as Profile;
            _miniWindow.UpdateState(
                profile?.Name ?? "Unknown",
                _engine.StatusText,
                _engine.IsRunning,
                _engine.AutoTarget.State
            );
        }
    }
}
