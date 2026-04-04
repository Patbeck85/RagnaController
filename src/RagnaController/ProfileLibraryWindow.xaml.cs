using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class ProfileLibraryWindow : Window
    {
        private readonly ProfileManager _manager;
        private List<Profile> _cache = new();
        public Profile? SelectedProfile { get; private set; }

        public ProfileLibraryWindow(ProfileManager manager)
        {
            InitializeComponent();
            _manager = manager;
            Loaded += (s, e) => Refresh();
        }

        private void Refresh()
        {
            _cache = _manager.Profiles.ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (SearchBox == null || FilterCombo == null || ProfilesList == null) return;

            string query = SearchBox.Text;
            string type = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Profiles";

            var filtered = _cache.Where(p =>
            {
                bool matchesSearch = string.IsNullOrEmpty(query) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchesType = type switch
                {
                    "Built-in Only" => p.IsBuiltIn,
                    "Custom Only" => !p.IsBuiltIn,
                    "Melee" => p.Class == "Melee",
                    "Ranged" => p.Class == "Ranged",
                    "Mage" => p.Class == "Mage",
                    "Support" => p.Class == "Support",
                    _ => true
                };
                return matchesSearch && matchesType;
            }).ToList();

            ProfilesList.ItemsSource = filtered;
            ProfileCount.Text = $"{filtered.Count} profiles found";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Profile p) { SelectedProfile = p; DialogResult = true; Close(); }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Profile p)
            {
                var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = $"{p.Name}.json" };
                if (sfd.ShowDialog() == true) _manager.Export(p, sfd.FileName);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Profile p && !p.IsBuiltIn)
            {
                if (MessageBox.Show($"Delete {p.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _manager.Delete(p);
                    Refresh();
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" };
            if (ofd.ShowDialog() == true)
            {
                var p = _manager.ImportPreview(ofd.FileName);
                if (p != null) { _manager.AddAndSave(p); Refresh(); }
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new ProfileWizardWindow { Owner = this };
            if (wizard.ShowDialog() == true && wizard.CreatedProfile != null) { _manager.AddAndSave(wizard.CreatedProfile); Refresh(); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }
    }
}