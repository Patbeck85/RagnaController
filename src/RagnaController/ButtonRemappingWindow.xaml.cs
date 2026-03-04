using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class ButtonRemappingWindow : Window
    {
        private readonly Profile _profile;
        private string? _selectedButton;
        private string? _selectedMacroPath;

        public ButtonRemappingWindow(Profile profile)
        {
            InitializeComponent();
            _profile = profile;
            
            PopulateKeyCombo();
            KeyDown += Window_KeyDown;
            Loaded += (s, e) => Focus();
            
            InitializeControllerPreview();
        }

        private void InitializeControllerPreview()
        {
            var preview = new ControllerPreview();
            PreviewContainer.Child = preview;
        }

        private void PopulateKeyCombo()
        {
            var keys = new[]
            {
                (Name: "Z", Key: VirtualKey.Z), (Name: "X", Key: VirtualKey.X), 
                (Name: "C", Key: VirtualKey.C), (Name: "V", Key: VirtualKey.V),
                (Name: "A", Key: VirtualKey.A), (Name: "S", Key: VirtualKey.S), 
                (Name: "D", Key: VirtualKey.D), (Name: "F", Key: VirtualKey.F),
                (Name: "Q", Key: VirtualKey.Q), (Name: "W", Key: VirtualKey.W), 
                (Name: "E", Key: VirtualKey.E), (Name: "R", Key: VirtualKey.R),
                (Name: "1", Key: VirtualKey.Num1), (Name: "2", Key: VirtualKey.Num2), 
                (Name: "3", Key: VirtualKey.Num3), (Name: "4", Key: VirtualKey.Num4),
                (Name: "F1", Key: VirtualKey.F1), (Name: "F2", Key: VirtualKey.F2), 
                (Name: "F3", Key: VirtualKey.F3), (Name: "F4", Key: VirtualKey.F4),
                (Name: "Tab", Key: VirtualKey.Tab), (Name: "Space", Key: VirtualKey.Space), 
                (Name: "Shift", Key: VirtualKey.ShiftLeft), (Name: "Ctrl", Key: VirtualKey.ControlLeft), 
                (Name: "Alt", Key: VirtualKey.AltLeft), (Name: "Esc", Key: VirtualKey.Escape)
            };

            foreach (var k in keys)
            {
                KeyCombo.Items.Add(new ComboBoxItem { Content = k.Name, Tag = k.Key });
            }
            KeyCombo.SelectedIndex = 0;
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string buttonName) return;
            _selectedButton = buttonName;
            SelectedButtonText.Text = buttonName;

            if (_profile.ButtonMappings.TryGetValue(buttonName, out var action))
                LoadExistingMapping(action);
            else
                ResetMapping();
        }

        private void LoadExistingMapping(ButtonAction action)
        {
            ActionTypeCombo.SelectedIndex = action.Type switch
            {
                ActionType.Key => 0,
                ActionType.LeftClick => 1,
                ActionType.RightClick => 2,
                _ => 0
            };

            if (action.Type == ActionType.Key)
            {
                var matchingItem = KeyCombo.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag is VirtualKey vk && vk == action.Key);
                if (matchingItem != null) KeyCombo.SelectedItem = matchingItem;
            }

            TurboCheckbox.IsChecked       = action.TurboEnabled;
            GroundSpellCheckbox.IsChecked = action.IsGroundSpell;
            TurboIntervalText.Text = action.TurboIntervalMs.ToString();
            TurboModeCombo.SelectedIndex = action.Mode switch
            {
                TurboMode.Burst     => 1,
                TurboMode.Rhythmic  => 2,
                TurboMode.Adaptive  => 3,
                _                   => 0
            };
        }

        private void ResetMapping()
        {
            ActionTypeCombo.SelectedIndex = 0;
            KeyCombo.SelectedIndex = 0;
            TurboCheckbox.IsChecked       = false;
            GroundSpellCheckbox.IsChecked = false;
            TurboIntervalText.Text = "100";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_selectedButton == null || ActionTypeCombo.SelectedIndex != 0) return;
            if (e.Key == Key.Escape) return;

            var vk = MapWpfKeyToVirtualKey(e.Key);
            if (vk == VirtualKey.None) return;

            var matchingItem = KeyCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is VirtualKey key && key == vk);

            if (matchingItem != null)
                KeyCombo.SelectedItem = matchingItem;
            else
            {
                var newItem = new ComboBoxItem { Content = e.Key.ToString(), Tag = vk };
                KeyCombo.Items.Add(newItem);
                KeyCombo.SelectedItem = newItem;
            }
            e.Handled = true;
        }

        private VirtualKey MapWpfKeyToVirtualKey(Key key) => key switch
        {
            Key.A => VirtualKey.A, Key.B => VirtualKey.B, Key.C => VirtualKey.C, Key.D => VirtualKey.D,
            Key.E => VirtualKey.E, Key.F => VirtualKey.F, Key.G => VirtualKey.G, Key.H => VirtualKey.H,
            Key.I => VirtualKey.I, Key.J => VirtualKey.J, Key.K => VirtualKey.K, Key.L => VirtualKey.L,
            Key.M => VirtualKey.M, Key.N => VirtualKey.N, Key.O => VirtualKey.O, Key.P => VirtualKey.P,
            Key.Q => VirtualKey.Q, Key.R => VirtualKey.R, Key.S => VirtualKey.S, Key.T => VirtualKey.T,
            Key.U => VirtualKey.U, Key.V => VirtualKey.V, Key.W => VirtualKey.W, Key.X => VirtualKey.X,
            Key.Y => VirtualKey.Y, Key.Z => VirtualKey.Z,
            Key.D0 => VirtualKey.Num0, Key.D1 => VirtualKey.Num1, Key.Space => VirtualKey.Space, 
            Key.Tab => VirtualKey.Tab, Key.LeftShift => VirtualKey.ShiftLeft, 
            Key.LeftCtrl => VirtualKey.ControlLeft, Key.Escape => VirtualKey.Escape,
            _ => VirtualKey.None
        };

        private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard: fires during InitializeComponent() before panels exist
            if (KeySelectionPanel == null || MacroSelectionPanel == null) return;
            if (ActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
            string actionType = item.Tag?.ToString() ?? "Key";
            KeySelectionPanel.Visibility   = actionType == "Key"   ? Visibility.Visible : Visibility.Collapsed;
            MacroSelectionPanel.Visibility = actionType == "Macro" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TurboCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (TurboPanel != null)
                TurboPanel.Visibility = (TurboCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }


        private void GroundSpellCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            // No extra panel needed — just a simple on/off flag
            // Visual feedback: dim the checkbox label if not in Mage mode
        }
        private bool ApplyMappingCore()
        {
            if (_selectedButton == null) return false;

            var selectedComboItem = ActionTypeCombo.SelectedItem as ComboBoxItem;
            string actionTag = selectedComboItem?.Tag?.ToString() ?? "Key";
            var actionType = GetSelectedActionType();

            var action = new ButtonAction { Type = actionType, Label = _selectedButton };

            if (actionType == ActionType.Key && KeyCombo.SelectedItem is ComboBoxItem keyItem && keyItem.Tag is VirtualKey vk)
            {
                action.Key = vk;
            }

            if (actionTag == "Macro")
            {
                if (string.IsNullOrEmpty(_selectedMacroPath))
                {
                    MessageBox.Show("Please select a macro file first.");
                    return false;
                }
                action.MacroFilePath = _selectedMacroPath;
                action.Label = System.IO.Path.GetFileNameWithoutExtension(_selectedMacroPath);
            }
            else
            {
                action.TurboEnabled   = TurboCheckbox.IsChecked    == true;
                action.IsGroundSpell  = GroundSpellCheckbox.IsChecked == true;
                action.Mode           = GetSelectedTurboMode();
                if (int.TryParse(TurboIntervalText.Text, out int interval))
                    action.TurboIntervalMs = Math.Max(30, interval);
            }

            _profile.ButtonMappings[_selectedButton] = action;
            return true;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyMappingCore())
                MessageBox.Show($"Mapping saved for {_selectedButton}");
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedButton == null) return;
            _profile.ButtonMappings.Remove(_selectedButton);
            ResetMapping();
            MessageBox.Show("Mapping cleared.");
        }

        private void BtnBrowseMacro_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Macro Files (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                _selectedMacroPath = dialog.FileName;
                if (MacroPathText != null) MacroPathText.Text = System.IO.Path.GetFileName(_selectedMacroPath);
            }
        }

        private void BtnRecordNewMacro_Click(object sender, RoutedEventArgs e)
        {
            var recorder = new MacroRecorderWindow();
            if (recorder.ShowDialog() == true && recorder.RecordedMacro != null)
            {
                _selectedMacroPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RagnaController", "Macros", recorder.RecordedMacro.Name + ".json");
                if (MacroPathText != null) MacroPathText.Text = recorder.RecordedMacro.Name + ".json";
            }
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var selectedType = GetSelectedActionType();
            switch (selectedType)
            {
                case ActionType.Key:
                    if (KeyCombo.SelectedItem is ComboBoxItem ki && ki.Tag is VirtualKey vk && vk != VirtualKey.None)
                    {
                        InputSimulator.TapKey(vk);
                        MessageBox.Show($"Key '{vk}' sent.", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                case ActionType.LeftClick:
                    InputSimulator.LeftClick();
                    MessageBox.Show("Left click sent.", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case ActionType.RightClick:
                    InputSimulator.RightClick();
                    MessageBox.Show("Right click sent.", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Silently apply current mapping before closing (no MessageBox)
            if (_selectedButton != null)
                ApplyMappingCore();
            this.DialogResult = true;
            this.Close();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; this.Close(); }

        private ActionType GetSelectedActionType()
        {
            if (ActionTypeCombo.SelectedItem is not ComboBoxItem item) return ActionType.Key;
            return item.Tag?.ToString() switch 
            { 
                "LeftClick" => ActionType.LeftClick, 
                "RightClick" => ActionType.RightClick, 
                "Macro" => ActionType.Key,
                _ => ActionType.Key 
            };
        }

        private TurboMode GetSelectedTurboMode()
        {
            return TurboModeCombo.SelectedIndex switch
            {
                1 => TurboMode.Burst,
                2 => TurboMode.Rhythmic,
                3 => TurboMode.Adaptive,
                _ => TurboMode.Standard
            };
        }
    }
}