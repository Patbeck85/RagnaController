using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class ProfileWizardWindow : Window
    {
        private int _currentStep = 1;
        private const int TotalSteps = 4;

        public Profile? CreatedProfile { get; private set; }

        public ProfileWizardWindow()
        {
            InitializeComponent();
            PopulateKeyBindingCombos();
            UpdateStepDisplay();
        }

        private void PopulateKeyBindingCombos()
        {
            var commonKeys = new (string Label, VirtualKey Key)[]
            {
                ("Z", VirtualKey.Z), ("X", VirtualKey.X), ("C", VirtualKey.C), ("V", VirtualKey.V),
                ("A", VirtualKey.A), ("S", VirtualKey.S), ("D", VirtualKey.D), ("F", VirtualKey.F),
                ("Q", VirtualKey.Q), ("W", VirtualKey.W), ("E", VirtualKey.E), ("R", VirtualKey.R),
                ("1", VirtualKey.Num1), ("2", VirtualKey.Num2), ("3", VirtualKey.Num3), ("4", VirtualKey.Num4),
                ("Space", VirtualKey.Space), ("Shift", VirtualKey.ShiftLeft),
                ("F1", VirtualKey.F1), ("F2", VirtualKey.F2), ("F3", VirtualKey.F3), ("F4", VirtualKey.F4)
            };

            foreach (var combo in new[] { AButtonCombo, BButtonCombo, XButtonCombo, YButtonCombo })
            {
                foreach (var (label, key) in commonKeys)
                {
                    combo.Items.Add(new ComboBoxItem { Content = label, Tag = key });
                }
                combo.SelectedIndex = 0;
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCurrentStep()) return;

            if (_currentStep < TotalSteps)
            {
                _currentStep++;
                UpdateStepDisplay();
            }
            else
            {
                CreateProfile();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepDisplay();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Discard profile creation?",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        // ── Step Management ───────────────────────────────────────────────────────

        private void UpdateStepDisplay()
        {
            // Hide all panels
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Collapsed;
            Step3Panel.Visibility = Visibility.Collapsed;
            Step4Panel.Visibility = Visibility.Collapsed;

            // Reset dots
            var inactiveBrush = (Brush)FindResource("BG3Brush");
            var activeBrush = (Brush)FindResource("NeonBrush");
            Step1Dot.Background = inactiveBrush;
            Step2Dot.Background = inactiveBrush;
            Step3Dot.Background = inactiveBrush;
            Step4Dot.Background = inactiveBrush;

            // Show current step
            switch (_currentStep)
            {
                case 1:
                    Step1Panel.Visibility = Visibility.Visible;
                    Step1Dot.Background = activeBrush;
                    StepDescription.Text = "Tell us about your character class and playstyle";
                    BtnBack.IsEnabled = false;
                    BtnNext.Content = "Next →";
                    break;

                case 2:
                    Step2Panel.Visibility = Visibility.Visible;
                    Step2Dot.Background = activeBrush;
                    StepDescription.Text = "Choose which combat systems to enable";
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Next →";
                    break;

                case 3:
                    Step3Panel.Visibility = Visibility.Visible;
                    Step3Dot.Background = activeBrush;
                    StepDescription.Text = "Assign your most-used skills to face buttons";
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Next →";
                    break;

                case 4:
                    Step4Panel.Visibility = Visibility.Visible;
                    Step4Dot.Background = activeBrush;
                    StepDescription.Text = "Review your profile before creating";
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Create Profile";
                    UpdateReviewPanel();
                    break;
            }
        }

        private bool ValidateCurrentStep()
        {
            switch (_currentStep)
            {
                case 1:
                    if (string.IsNullOrWhiteSpace(ProfileNameText.Text))
                    {
                        MessageBox.Show("Please enter a profile name", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;

                case 2:
                    // No validation needed - engines are optional
                    return true;

                case 3:
                    // No validation - defaults are fine
                    return true;

                case 4:
                    return true;

                default:
                    return true;
            }
        }

        // ── Class Template Selection ──────────────────────────────────────────────

        private void ClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassCombo.SelectedItem is not ComboBoxItem item) return;
            string classType = item.Tag?.ToString() ?? "Custom";

            // Auto-select appropriate engines based on class
            EnableMeleeCheck.IsChecked = classType == "Melee";
            EnableKiteCheck.IsChecked = classType == "Ranged";
            EnableMageCheck.IsChecked = classType == "Mage";
            EnableSupportCheck.IsChecked = classType == "Support";
        }

        // ── Review Panel ──────────────────────────────────────────────────────────

        private void UpdateReviewPanel()
        {
            ReviewName.Text = ProfileNameText.Text;

            if (ClassCombo.SelectedItem is ComboBoxItem classItem)
                ReviewClass.Text = classItem.Content?.ToString() ?? "Custom";

            var engines = new List<string>();
            if (EnableMeleeCheck.IsChecked == true) engines.Add("Auto-Target");
            if (EnableKiteCheck.IsChecked == true) engines.Add("Kite Engine");
            if (EnableMageCheck.IsChecked == true) engines.Add("Mage System");
            if (EnableSupportCheck.IsChecked == true) engines.Add("Support Mode");

            ReviewEngine.Text = engines.Count > 0
                ? string.Join(", ", engines)
                : "Standard (Manual control)";
        }

        // ── Profile Creation ──────────────────────────────────────────────────────

        private void CreateProfile()
        {
            try
            {
                var profile = new Profile
                {
                    Name = ProfileNameText.Text.Trim(),
                    Description = DescriptionText.Text.Trim(),
                    Class = GetSelectedClass(),
                    IsBuiltIn = false,
                    
                    // Combat engines
                    AutoAttackEnabled = EnableMeleeCheck.IsChecked == true,
                    KiteEnabled = EnableKiteCheck.IsChecked == true,
                    MageEnabled = EnableMageCheck.IsChecked == true,
                    SupportEnabled = EnableSupportCheck.IsChecked == true,
                    
                    // Basic settings
                    MouseSensitivity = 1.0f,
                    Deadzone = 0.15f,
                    MovementCurve = 1.2f,
                    ActionRpgMode = true,
                    ActionSpeed = 4.5f,
                    
                    // Button mappings
                    ButtonMappings = CreateButtonMappings()
                };

                CreatedProfile = profile;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetSelectedClass()
        {
            if (ClassCombo.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString() ?? "Custom";
                return tag == "Custom" ? "Custom" : tag;
            }
            return "Custom";
        }

        private Dictionary<string, ButtonAction> CreateButtonMappings()
        {
            var mappings = new Dictionary<string, ButtonAction>();

            // Face buttons
            AddMapping(mappings, "A", AButtonCombo);
            AddMapping(mappings, "B", BButtonCombo);
            AddMapping(mappings, "X", XButtonCombo);
            AddMapping(mappings, "Y", YButtonCombo);

            // Add default special buttons
            mappings["Start"] = new ButtonAction
            {
                Type = ActionType.Key,
                Key = VirtualKey.Escape,
                Label = "Menu"
            };

            mappings["RightShoulder"] = new ButtonAction
            {
                Type = ActionType.Key,
                Key = VirtualKey.F1,
                Label = "Potion"
            };

            return mappings;
        }

        private void AddMapping(Dictionary<string, ButtonAction> mappings, string button, ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is VirtualKey key)
            {
                mappings[button] = new ButtonAction
                {
                    Type = ActionType.Key,
                    Key = key,
                    Label = item.Content?.ToString() ?? button
                };
            }
        }
    }
}
