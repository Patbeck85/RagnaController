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
        private bool _isListeningForKey;
        private string? _selectedMacroPath;

        public ButtonRemappingWindow(Profile profile)
        {
            InitializeComponent();
            _profile = profile;
            
            PopulateKeyCombo();
            KeyDown += Window_KeyDown;
            Loaded += (s, e) => Focus();
            
            // Initialize controller preview
            InitializeControllerPreview();
        }

        private void InitializeControllerPreview()
        {
            var preview = new ControllerPreview();
            PreviewContainer.Child = preview;
            
            // TODO: Wire up live controller input in future update
            // For now, preview is static
        }

        private void PopulateKeyCombo()
        {
            // Add common keys
            var keys = new[]
            {
                ("Z", VirtualKey.Z), ("X", VirtualKey.X), ("C", VirtualKey.C), ("V", VirtualKey.V),
                ("A", VirtualKey.A), ("S", VirtualKey.S), ("D", VirtualKey.D), ("F", VirtualKey.F),
                ("Q", VirtualKey.Q), ("W", VirtualKey.W), ("E", VirtualKey.E), ("R", VirtualKey.R),
                ("1", VirtualKey.Num1), ("2", VirtualKey.Num2), ("3", VirtualKey.Num3), ("4", VirtualKey.Num4),
                ("F1", VirtualKey.F1), ("F2", VirtualKey.F2), ("F3", VirtualKey.F3), ("F4", VirtualKey.F4),
                ("F5", VirtualKey.F5), ("F6", VirtualKey.F6), ("F7", VirtualKey.F7), ("F8", VirtualKey.F8),
                ("Tab", VirtualKey.Tab), ("Space", VirtualKey.Space), ("Shift", VirtualKey.ShiftLeft),
                ("Ctrl", VirtualKey.ControlLeft), ("Alt", VirtualKey.AltLeft), ("Esc", VirtualKey.Escape)
            };

            foreach (var (name, vk) in keys)
            {
                KeyCombo.Items.Add(new ComboBoxItem { Content = name, Tag = vk });
            }

            KeyCombo.SelectedIndex = 0;
        }

        // ── Button Selection ──────────────────────────────────────────────────────

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string buttonName) return;

            _selectedButton = buttonName;
            SelectedButtonText.Text = buttonName;

            // Load existing mapping if any
            if (_profile.ButtonMappings.TryGetValue(buttonName, out var action))
            {
                LoadExistingMapping(action);
            }
            else
            {
                ResetMapping();
            }
        }

        private void LoadExistingMapping(ButtonAction action)
        {
            // Set action type
            ActionTypeCombo.SelectedIndex = action.Type switch
            {
                ActionType.Key => 0,
                ActionType.LeftClick => 1,
                ActionType.RightClick => 2,
                _ => 0
            };

            // Set key if applicable
            if (action.Type == ActionType.Key)
            {
                var matchingItem = KeyCombo.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag is VirtualKey vk && vk == action.Key);
                if (matchingItem != null)
                    KeyCombo.SelectedItem = matchingItem;
            }

            // Set turbo
            TurboCheckbox.IsChecked = action.TurboEnabled;
            TurboIntervalText.Text = action.TurboIntervalMs.ToString();
            
            // Set turbo mode
            TurboModeCombo.SelectedIndex = action.Mode switch
            {
                TurboMode.Standard => 0,
                TurboMode.Burst => 1,
                TurboMode.Rhythmic => 2,
                TurboMode.Adaptive => 3,
                _ => 0
            };
        }

        private void ResetMapping()
        {
            ActionTypeCombo.SelectedIndex = 0;
            KeyCombo.SelectedIndex = 0;
            TurboCheckbox.IsChecked = false;
            TurboIntervalText.Text = "100";
            TurboModeCombo.SelectedIndex = 0;
        }

        // ── Input Handling ────────────────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_selectedButton == null || ActionTypeCombo.SelectedIndex != 0) return;
            if (e.Key == Key.Escape) return; // Ignore ESC

            // Map WPF Key to VirtualKey
            var vk = MapWpfKeyToVirtualKey(e.Key);
            if (vk == VirtualKey.None) return;

            // Find matching item in combo or add new
            var matchingItem = KeyCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is VirtualKey key && key == vk);

            if (matchingItem != null)
            {
                KeyCombo.SelectedItem = matchingItem;
            }
            else
            {
                // Add custom key
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
            Key.D0 => VirtualKey.Num0, Key.D1 => VirtualKey.Num1, Key.D2 => VirtualKey.Num2,
            Key.D3 => VirtualKey.Num3, Key.D4 => VirtualKey.Num4, Key.D5 => VirtualKey.Num5,
            Key.D6 => VirtualKey.Num6, Key.D7 => VirtualKey.Num7, Key.D8 => VirtualKey.Num8,
            Key.D9 => VirtualKey.Num9,
            Key.F1 => VirtualKey.F1, Key.F2 => VirtualKey.F2, Key.F3 => VirtualKey.F3,
            Key.F4 => VirtualKey.F4, Key.F5 => VirtualKey.F5, Key.F6 => VirtualKey.F6,
            Key.F7 => VirtualKey.F7, Key.F8 => VirtualKey.F8, Key.F9 => VirtualKey.F9,
            Key.F10 => VirtualKey.F10, Key.F11 => VirtualKey.F11, Key.F12 => VirtualKey.F12,
            Key.Space => VirtualKey.Space, Key.Tab => VirtualKey.Tab,
            Key.LeftShift => VirtualKey.ShiftLeft, Key.LeftCtrl => VirtualKey.ControlLeft,
            Key.LeftAlt => VirtualKey.AltLeft, Key.Escape => VirtualKey.Escape,
            _ => VirtualKey.None
        };

        // ── UI Event Handlers ─────────────────────────────────────────────────────

        private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
            
            string actionType = item.Tag?.ToString() ?? "Key";
            
            // Show/hide panels based on action type
            KeySelectionPanel.Visibility = actionType == "Key" ? Visibility.Visible : Visibility.Collapsed;
            MacroSelectionPanel.Visibility = actionType == "Macro" ? Visibility.Visible : Visibility.Collapsed;
            
            // Disable turbo for macro actions
            if (actionType == "Macro")
            {
                TurboCheckbox.IsEnabled = false;
                TurboCheckbox.IsChecked = false;
            }
            else
            {
                TurboCheckbox.IsEnabled = true;
            }
        }

        private void TurboCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            TurboPanel.Visibility = TurboCheckbox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedButton == null)
            {
                MessageBox.Show("Please select a button first", "No Button Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var actionType = GetSelectedActionType();

            // Build action based on type
            var action = new ButtonAction
            {
                Type = actionType,
                Label = _selectedButton
            };

            if (actionType == ActionType.Key)
            {
                if (KeyCombo.SelectedItem is ComboBoxItem keyItem && keyItem.Tag is VirtualKey vk)
                    action.Key = vk;
                else
                {
                    MessageBox.Show("Please select a key", "No Key Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (actionType == ActionType.LeftClick || actionType == ActionType.RightClick)
            {
                // Click actions don't need additional config
            }

            // Check if macro mode
            if (ActionTypeCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Macro")
            {
                if (string.IsNullOrEmpty(_selectedMacroPath))
                {
                    MessageBox.Show("Please select a macro file", "No Macro Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                action.MacroFilePath = _selectedMacroPath;
                action.Label = System.IO.Path.GetFileNameWithoutExtension(_selectedMacroPath);
            }

            // Turbo settings (only for non-macro actions)
            if (item?.Tag?.ToString() != "Macro")
            {
                action.TurboEnabled = TurboCheckbox.IsChecked == true;
                if (action.TurboEnabled)
                {
                    if (int.TryParse(TurboIntervalText.Text, out int interval))
                        action.TurboIntervalMs = Math.Max(30, interval);

                    action.Mode = GetSelectedTurboMode();
                }
            }

            // Apply to profile
            _profile.ButtonMappings[_selectedButton] = action;

            string actionDesc = action.IsMacro ? $"Macro: {action.Label}" : $"{action.Type}";
            MessageBox.Show($"Mapping applied for {_selectedButton}\n→ {actionDesc}", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedButton == null) return;

            _profile.ButtonMappings.Remove(_selectedButton);
            ResetMapping();

            MessageBox.Show($"Mapping cleared for {_selectedButton}", "Cleared",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Macro Binding ─────────────────────────────────────────────────────────

        private void BtnBrowseMacro_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Select Macro File",
                InitialDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RagnaController", "Macros")
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedMacroPath = dialog.FileName;
                MacroPathText.Text = System.IO.Path.GetFileName(_selectedMacroPath);
                
                // Load macro info
                try
                {
                    string json = System.IO.File.ReadAllText(_selectedMacroPath);
                    var macro = System.Text.Json.JsonSerializer.Deserialize<Core.Macro>(json);
                    if (macro != null)
                    {
                        MacroInfoText.Text = $"{macro.Steps.Count} steps, {macro.TotalDurationMs}ms duration";
                    }
                }
                catch
                {
                    MacroInfoText.Text = "Could not load macro info";
                }
            }
        }

        private void BtnRecordNewMacro_Click(object sender, RoutedEventArgs e)
        {
            var recorder = new MacroRecorderWindow();
            if (recorder.ShowDialog() == true && recorder.RecordedMacro != null)
            {
                // Find the saved file
                string macrosFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RagnaController", "Macros");
                
                var files = System.IO.Directory.GetFiles(macrosFolder, "*.json")
                    .OrderByDescending(f => System.IO.File.GetLastWriteTime(f));
                
                if (files.Any())
                {
                    _selectedMacroPath = files.First();
                    MacroPathText.Text = System.IO.Path.GetFileName(_selectedMacroPath);
                    MacroInfoText.Text = $"{recorder.RecordedMacro.Steps.Count} steps, {recorder.RecordedMacro.TotalDurationMs}ms";
                }
            }
        }

        // ── Save & Cancel ─────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private ActionType GetSelectedActionType()
        {
            if (ActionTypeCombo.SelectedItem is not ComboBoxItem item) return ActionType.Key;
            return item.Tag?.ToString() switch
            {
                "Key" => ActionType.Key,
                "LeftClick" => ActionType.LeftClick,
                "RightClick" => ActionType.RightClick,
                _ => ActionType.Key
            };
        }

        private TurboMode GetSelectedTurboMode()
        {
            if (TurboModeCombo.SelectedItem is not ComboBoxItem item) return TurboMode.Standard;
            return item.Tag?.ToString() switch
            {
                "Standard" => TurboMode.Standard,
                "Burst" => TurboMode.Burst,
                "Rhythmic" => TurboMode.Rhythmic,
                "Adaptive" => TurboMode.Adaptive,
                _ => TurboMode.Standard
            };
        }
    }
}
