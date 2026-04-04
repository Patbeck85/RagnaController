using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class ProfileWizardWindow : Window
    {
        private int _step = 1;
        public Profile? CreatedProfile { get; private set; }

        public ProfileWizardWindow()
        {
            InitializeComponent();
            PopulateCombos();
            UpdateUI();
        }

        private void PopulateCombos()
        {
            var keys = new[] { VirtualKey.Z, VirtualKey.X, VirtualKey.C, VirtualKey.V, VirtualKey.A, VirtualKey.S, VirtualKey.D, VirtualKey.F, VirtualKey.Q, VirtualKey.W, VirtualKey.E, VirtualKey.R, VirtualKey.F1, VirtualKey.F2, VirtualKey.F3, VirtualKey.F4 };
            foreach (var combo in new[] { AButtonCombo, BButtonCombo, XButtonCombo, YButtonCombo })
            {
                if (combo == null) continue;
                foreach (var k in keys) combo.Items.Add(new ComboBoxItem { Content = k.ToString(), Tag = k });
                combo.SelectedIndex = 0;
            }
        }

        private void UpdateUI()
        {
            if (Step1Panel == null) return;

            Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
            
            BtnBack.IsEnabled = _step > 1;
            BtnNext.Content = _step == 4 ? "CREATE" : "NEXT →";
            
            Brush gold = (Brush)FindResource("GoldBrush");
            Brush dim = (Brush)FindResource("BorderBrush");
            
            if (Step1Dot != null) Step1Dot.Fill = _step >= 1 ? gold : dim;
            if (Step2Dot != null) Step2Dot.Fill = _step >= 2 ? gold : dim;
            if (Step3Dot != null) Step3Dot.Fill = _step >= 3 ? gold : dim;
            if (Step4Dot != null) Step4Dot.Fill = _step >= 4 ? gold : dim;

            if (_step == 4) { 
                ReviewName.Text = ProfileNameText.Text.ToUpper(); 
                var selectedItem = ClassCombo.SelectedItem as ComboBoxItem;
                string className = selectedItem?.Content?.ToString() ?? "Unknown";
                ReviewClass.Text = "CLASS: " + className.ToUpper(); 
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 1 && string.IsNullOrWhiteSpace(ProfileNameText.Text)) return;
            if (_step < 4) { _step++; UpdateUI(); }
            else Finish();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) 
        { 
            if (_step > 1) { _step--; UpdateUI(); } 
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void ClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EnableMeleeCheck == null) return;
            string tag = (ClassCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            EnableMeleeCheck.IsChecked = tag == "Melee";
            EnableKiteCheck.IsChecked = tag == "Ranged";
            EnableMageCheck.IsChecked = tag == "Mage";
            EnableSupportCheck.IsChecked = tag == "Support";
        }

        private void Finish()
        {
            CreatedProfile = new Profile {
                Name = ProfileNameText.Text,
                Class = (ClassCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Melee",
                AutoAttackEnabled = EnableMeleeCheck.IsChecked == true,
                KiteEnabled = EnableKiteCheck.IsChecked == true,
                MageEnabled = EnableMageCheck.IsChecked == true,
                SupportEnabled = EnableSupportCheck.IsChecked == true,
                ButtonMappings = new Dictionary<string, ButtonAction> {
                    { "A", new ButtonAction { Type = ActionType.Key, Key = (VirtualKey)((ComboBoxItem)AButtonCombo.SelectedItem).Tag, Label = "Quick Key A" } },
                    { "B", new ButtonAction { Type = ActionType.Key, Key = (VirtualKey)((ComboBoxItem)BButtonCombo.SelectedItem).Tag, Label = "Quick Key B" } },
                    { "X", new ButtonAction { Type = ActionType.Key, Key = (VirtualKey)((ComboBoxItem)XButtonCombo.SelectedItem).Tag, Label = "Quick Key X" } },
                    { "Y", new ButtonAction { Type = ActionType.Key, Key = (VirtualKey)((ComboBoxItem)YButtonCombo.SelectedItem).Tag, Label = "Quick Key Y" } }
                }
            };
            DialogResult = true; Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) 
        { 
            if (e.LeftButton == MouseButtonState.Pressed) DragMove(); 
        }
    }
}