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
        private readonly ProfileManager _profileManager;
        private List<Profile> _allProfiles;
        private List<Profile> _filteredProfiles;

        public Profile? SelectedProfile { get; private set; }

        public ProfileLibraryWindow(ProfileManager profileManager)
        {
            InitializeComponent();
            _profileManager = profileManager;
            _allProfiles = new List<Profile>(_profileManager.Profiles);
            _filteredProfiles = new List<Profile>(_allProfiles);
            
            RefreshProfileList();
        }

        // ── Refresh & Filter ──────────────────────────────────────────────────────

        private void RefreshProfileList()
        {
            _allProfiles = new List<Profile>(_profileManager.Profiles);
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            string searchText = SearchBox.Text.ToLower();
            string filterType = ((ComboBoxItem)FilterCombo.SelectedItem)?.Content?.ToString() ?? "All Profiles";

            _filteredProfiles = _allProfiles.Where(p =>
            {
                // Search filter
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                    p.Name.ToLower().Contains(searchText) ||
                    (p.Description?.ToLower().Contains(searchText) ?? false);

                // Type filter
                bool matchesType = filterType switch
                {
                    "Built-in Only" => p.IsBuiltIn,
                    "Custom Only" => !p.IsBuiltIn,
                    "Melee" => p.Class?.Contains("Melee") ?? false,
                    "Ranged" => p.Class?.Contains("Ranged") ?? false,
                    "Mage" => p.Class?.Contains("Mage") ?? false,
                    "Support" => p.Class?.Contains("Support") ?? false,
                    _ => true
                };

                return matchesSearch && matchesType;
            }).ToList();

            ProfilesList.ItemsSource = _filteredProfiles;
            ProfileCount.Text = $"{_filteredProfiles.Count} profile(s) found";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // ── Button Handlers ───────────────────────────────────────────────────────

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Profile profile)
            {
                SelectedProfile = profile;
                DialogResult = true;
                Close();
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Profile profile)
            {
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
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Profile profile)
            {
                if (profile.IsBuiltIn)
                {
                    MessageBox.Show("Cannot delete built-in profiles", "Delete Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Delete profile '{profile.Name}'?\nThis cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _profileManager.Delete(profile);
                        RefreshProfileList();
                        MessageBox.Show($"Profile '{profile.Name}' deleted", "Deleted",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Delete failed:\n{ex.Message}", "Delete Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new ProfileWizardWindow();
            if (wizard.ShowDialog() == true && wizard.CreatedProfile != null)
            {
                _profileManager.Profiles.Add(wizard.CreatedProfile);
                _profileManager.Save(wizard.CreatedProfile);
                RefreshProfileList();
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
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
                    if (profile != null)
                    {
                        RefreshProfileList();
                        MessageBox.Show($"Profile '{profile.Name}' imported successfully!", "Import Complete",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed:\n{ex.Message}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
