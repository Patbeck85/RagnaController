using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RagnaController.Controller;
using RagnaController.Core;
using RagnaController.Profiles;
using SharpDX.XInput;

namespace RagnaController
{
    public partial class ButtonRemappingWindow : Window
    {
        private readonly Profile _profile;
        private string? _selectedBaseButton;
        private string _currentLayerPrefix = "";
        private string? _selectedMacroPath;
        private ControllerPreview? _preview;
        private bool _quickBindListening = false;

        // Controller auto-select polling
        private readonly ControllerService _ctrl = new();
        private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
        private GamepadButtonFlags _lastButtons;
        private byte _lastLT, _lastRT;

        // XInput button flags mapped to XAML Tag names
        private static readonly (GamepadButtonFlags flag, string tag)[] ButtonMap =
        {
            (GamepadButtonFlags.A,             "A"),
            (GamepadButtonFlags.B,             "B"),
            (GamepadButtonFlags.X,             "X"),
            (GamepadButtonFlags.Y,             "Y"),
            (GamepadButtonFlags.LeftShoulder,  "LeftShoulder"),
            (GamepadButtonFlags.RightShoulder, "RightShoulder"),
            (GamepadButtonFlags.LeftThumb,     "LeftThumb"),
            (GamepadButtonFlags.RightThumb,    "RightThumb"),
            (GamepadButtonFlags.DPadUp,        "DPadUp"),
            (GamepadButtonFlags.DPadDown,      "DPadDown"),
            (GamepadButtonFlags.DPadLeft,      "DPadLeft"),
            (GamepadButtonFlags.DPadRight,     "DPadRight"),
            (GamepadButtonFlags.Start,         "Start"),
            (GamepadButtonFlags.Back,          "Back"),
        };

        public ButtonRemappingWindow(Profile profile)
        {
            InitializeComponent();
            _profile = profile;
            PopulateKeyCombo();
            _preview = new ControllerPreview();
            PreviewContainer.Content = _preview;
            KeyDown += Window_KeyDown;
            _pollTimer.Tick += PollController;
            _pollTimer.Start();
            Closed += (_, _) => { _pollTimer.Stop(); _ctrl.Dispose(); };
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        // Polls the controller and auto-selects the pressed button in the UI (rising edge only)
        private void PollController(object? sender, EventArgs e)
        {
            var gp = _ctrl.GetGamepad();
            if (gp == null) return;

            var current = gp.Value.Buttons;
            byte lt = gp.Value.LeftTrigger;
            byte rt = gp.Value.RightTrigger;
            bool ltNow  = lt > 128, ltPrev = _lastLT > 128;
            bool rtNow  = rt > 128, rtPrev = _lastRT > 128;

            var pressed = current & ~_lastButtons;
            foreach (var (flag, tag) in ButtonMap)
                if (pressed.HasFlag(flag)) { AutoSelectButton(tag); break; }

            if (ltNow && !ltPrev) AutoSelectButton("LeftTrigger");
            if (rtNow && !rtPrev) AutoSelectButton("RightTrigger");

            _lastButtons = current;
            _lastLT = lt;
            _lastRT = rt;
        }

        private void AutoSelectButton(string tag)
        {
            _selectedBaseButton = tag;
            _preview?.HighlightButton(tag);
            RefreshMappingDisplay();

            if (SelectedButtonText == null) return;
            SelectedButtonText.Foreground = System.Windows.Media.Brushes.Yellow;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            t.Tick += (_, _) => { SelectedButtonText.Foreground = System.Windows.Media.Brushes.White; t.Stop(); };
            t.Start();
        }

        private void PopulateKeyCombo()
        {
            var keys = Enum.GetValues(typeof(VirtualKey)).Cast<VirtualKey>()
                           .Where(k => k != VirtualKey.None).OrderBy(k => k.ToString());
            foreach (var k in keys)
                KeyCombo.Items.Add(new ComboBoxItem { Content = k.ToString(), Tag = k });
            KeyCombo.SelectedIndex = 0;
        }

        private void LayerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _currentLayerPrefix = btn.Tag?.ToString() ?? "";
            foreach (Button b in new[] { LayerBase, LayerL1, LayerR1, LayerL2, LayerR2 })
                if (b != null) b.Style = (Style)FindResource(b == btn ? "LayerButtonActive" : "LayerButton");
            if (_selectedBaseButton != null) RefreshMappingDisplay();
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string name)
            {
                _selectedBaseButton = name;
                _preview?.HighlightButton(name);
                RefreshMappingDisplay();
            }
        }

        private void RefreshMappingDisplay()
        {
            string fullKey = _currentLayerPrefix + _selectedBaseButton;
            SelectedButtonText.Text = fullKey;
            if (_profile.ButtonMappings.TryGetValue(fullKey, out var action)) LoadMapping(action);
            else ResetFields();
        }

        private void LoadMapping(ButtonAction a)
        {
            ActionTypeCombo.SelectedIndex = a.Type switch
            {
                ActionType.LeftClick    => 1,
                ActionType.RightClick   => 2,
                _ when a.IsMacro        => 3,
                ActionType.Combo        => 4,
                ActionType.SwitchWindow => 5,
                _                       => 0
            };
            KeyCombo.SelectedItem = KeyCombo.Items.Cast<ComboBoxItem>()
                                              .FirstOrDefault(i => (VirtualKey)i.Tag == a.Key);
            TurboCheckbox.IsChecked       = a.TurboEnabled;
            GroundSpellCheckbox.IsChecked = a.IsGroundSpell;
            TurboIntervalText.Text        = a.TurboIntervalMs.ToString();
            TurboModeCombo.SelectedIndex  = (int)a.Mode;
            if (a.IsMacro) { _selectedMacroPath = a.MacroFilePath; MacroPathText.Text = System.IO.Path.GetFileName(a.MacroFilePath); }
            if (a.Type == ActionType.SwitchWindow && WindowTargetText != null)
                WindowTargetText.Text = a.WindowTarget;
        }

        private void ResetFields()
        {
            ActionTypeCombo.SelectedIndex = 0;
            TurboCheckbox.IsChecked = false;
            GroundSpellCheckbox.IsChecked = false;
            TurboIntervalText.Text = "100";
            MacroPathText.Text = "";
            _selectedMacroPath = null;
            if (WindowTargetText != null) WindowTargetText.Text = "ragexe";
        }

        private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KeySelectionPanel == null || MacroSelectionPanel == null) return;
            int idx = ActionTypeCombo.SelectedIndex;
            KeySelectionPanel.Visibility    = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
            MacroSelectionPanel.Visibility  = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (WindowTargetPanel != null)
                WindowTargetPanel.Visibility = idx == 5 ? Visibility.Visible : Visibility.Collapsed;
            if (TurboCheckbox != null) TurboCheckbox.IsEnabled = idx != 4 && idx != 5; // Combo and SwitchWindow drive their own timing
        }

        private void TurboCheckbox_Changed(object sender, RoutedEventArgs e)
            => TurboPanel.Visibility = TurboCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBaseButton == null) return;
            string fullKey = _currentLayerPrefix + _selectedBaseButton;
            var a = new ButtonAction { Label = _selectedBaseButton };
            int typeIdx = ActionTypeCombo.SelectedIndex;

            if      (typeIdx == 0) a.Key = (VirtualKey)((ComboBoxItem)KeyCombo.SelectedItem).Tag;
            else if (typeIdx == 1) a.Type = ActionType.LeftClick;
            else if (typeIdx == 2) a.Type = ActionType.RightClick;
            else if (typeIdx == 3)
            {
                if (string.IsNullOrEmpty(_selectedMacroPath)) return;
                a.MacroFilePath = _selectedMacroPath;
                a.Label = MacroPathText.Text;
            }
            else if (typeIdx == 4) { a.Type = ActionType.Combo; a.Label = "Class Combo Sequence"; }
            else if (typeIdx == 5)
            {
                a.Type        = ActionType.SwitchWindow;
                a.WindowTarget = WindowTargetText?.Text?.Trim() is { Length: > 0 } t ? t : "ragexe";
                a.Label       = $"⇄ {a.WindowTarget}";
            }

            a.TurboEnabled    = TurboCheckbox.IsChecked == true && typeIdx != 4 && typeIdx != 5;
            a.IsGroundSpell   = GroundSpellCheckbox.IsChecked == true;
            a.Mode            = (TurboMode)TurboModeCombo.SelectedIndex;
            if (int.TryParse(TurboIntervalText.Text, out int ms)) a.TurboIntervalMs = Math.Max(30, ms);
            _profile.ButtonMappings[fullKey] = a;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBaseButton != null)
            {
                _profile.ButtonMappings.Remove(_currentLayerPrefix + _selectedBaseButton);
                ResetFields();
            }
        }

        private void BtnBrowseMacro_Click(object sender, RoutedEventArgs e)
        {
            var d = new Microsoft.Win32.OpenFileDialog { Filter = "Macro|*.json" };
            if (d.ShowDialog() == true) { _selectedMacroPath = d.FileName; MacroPathText.Text = d.SafeFileName; }
        }

        private void BtnTurboTest_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TurboIntervalText.Text, out int ms) || ms < 10) ms = 100;
            TurboFreqText.Text = $"≈ {1000.0 / ms:F1} / sec  ({ms} ms)";
            if (BtnTurboTest == null) return;
            BtnTurboTest.IsEnabled = false;
            System.Threading.Tasks.Task.Delay(600).ContinueWith(_ =>
                Dispatcher.Invoke(() => BtnTurboTest.IsEnabled = true));
        }

        private void BtnQuickBind_Click(object sender, RoutedEventArgs e)
        {
            _quickBindListening = !_quickBindListening;
            if (BtnQuickBind == null) return;
            if (_quickBindListening)
            {
                BtnQuickBind.Content     = "🔴  LISTENING — press a key...";
                BtnQuickBind.Foreground  = System.Windows.Media.Brushes.Red;
                BtnQuickBind.BorderBrush = System.Windows.Media.Brushes.Red;
                Focus();
            }
            else
            {
                BtnQuickBind.Content = "⌨  PRESS A KEY";
                var blue = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3A, 0x8E, 0xFF));
                BtnQuickBind.Foreground  = blue;
                BtnQuickBind.BorderBrush = blue;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_selectedBaseButton == null || ActionTypeCombo.SelectedIndex != 0 || e.Key == Key.Escape)
            {
                if (e.Key == Key.Escape && _quickBindListening)
                    BtnQuickBind_Click(this, new RoutedEventArgs());
                return;
            }
            var vk = MapKey(e.Key);
            if (vk == VirtualKey.None) return;

            var item = KeyCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (VirtualKey)i.Tag == vk);
            if (item == null) return;

            KeyCombo.SelectedItem = item;
            if (_quickBindListening)
            {
                BtnApply_Click(this, new RoutedEventArgs());
                BtnQuickBind_Click(this, new RoutedEventArgs());
                if (QuickBindHint != null) QuickBindHint.Text = $"✓ Assigned: {vk}";
            }
            e.Handled = true;
        }

        private static VirtualKey MapKey(Key k) => k switch
        {
            Key.A => VirtualKey.A,  Key.B => VirtualKey.B,  Key.C => VirtualKey.C,
            Key.D => VirtualKey.D,  Key.E => VirtualKey.E,  Key.F => VirtualKey.F,
            Key.G => VirtualKey.G,  Key.H => VirtualKey.H,  Key.I => VirtualKey.I,
            Key.J => VirtualKey.J,  Key.K => VirtualKey.K,  Key.L => VirtualKey.L,
            Key.M => VirtualKey.M,  Key.N => VirtualKey.N,  Key.O => VirtualKey.O,
            Key.P => VirtualKey.P,  Key.Q => VirtualKey.Q,  Key.R => VirtualKey.R,
            Key.S => VirtualKey.S,  Key.T => VirtualKey.T,  Key.U => VirtualKey.U,
            Key.V => VirtualKey.V,  Key.W => VirtualKey.W,  Key.X => VirtualKey.X,
            Key.Y => VirtualKey.Y,  Key.Z => VirtualKey.Z,
            Key.F1  => VirtualKey.F1,  Key.F2  => VirtualKey.F2,  Key.F3  => VirtualKey.F3,
            Key.F4  => VirtualKey.F4,  Key.F5  => VirtualKey.F5,  Key.F6  => VirtualKey.F6,
            Key.F7  => VirtualKey.F7,  Key.F8  => VirtualKey.F8,  Key.F9  => VirtualKey.F9,
            Key.F10 => VirtualKey.F10, Key.F11 => VirtualKey.F11, Key.F12 => VirtualKey.F12,
            Key.Space => VirtualKey.Space, Key.Tab => VirtualKey.Tab, Key.Enter => VirtualKey.Enter,
            _ => VirtualKey.None
        };
    }
}
