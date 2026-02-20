using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using RagnaController.Core;

namespace RagnaController
{
    public partial class MacroEditorWindow : Window
    {
        private Macro _macro;
        private readonly string _filePath;
        private ObservableCollection<MacroStepViewModel> _steps;

        public MacroEditorWindow(string macroFilePath)
        {
            InitializeComponent();
            _filePath = macroFilePath;
            LoadMacro();
        }

        private void LoadMacro()
        {
            try
            {
                string json = File.ReadAllText(_filePath);
                _macro = JsonSerializer.Deserialize<Macro>(json) ?? new Macro();
                
                NameText.Text = _macro.Name;
                MacroInfo.Text = $"Editing: {Path.GetFileName(_filePath)}";
                
                _steps = new ObservableCollection<MacroStepViewModel>(
                    _macro.Steps.Select((s, i) => new MacroStepViewModel(i + 1, s))
                );
                
                StepsGrid.ItemsSource = _steps;
                UpdateDuration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load macro:\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void UpdateDuration()
        {
            int total = _steps.Sum(s => s.DelayMs);
            DurationText.Text = $"Duration: {total}ms";
            _macro.TotalDurationMs = total;
        }

        // ── Step Management ───────────────────────────────────────────────────────

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MacroStepViewModel step)
            {
                int index = _steps.IndexOf(step);
                if (index > 0)
                {
                    _steps.Move(index, index - 1);
                    ReindexSteps();
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MacroStepViewModel step)
            {
                int index = _steps.IndexOf(step);
                if (index < _steps.Count - 1)
                {
                    _steps.Move(index, index + 1);
                    ReindexSteps();
                }
            }
        }

        private void BtnDeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MacroStepViewModel step)
            {
                var result = MessageBox.Show(
                    $"Delete step {step.Index}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _steps.Remove(step);
                    ReindexSteps();
                    UpdateDuration();
                }
            }
        }

        private void ReindexSteps()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].Index = i + 1;
            }
        }

        // ── Macro Operations ──────────────────────────────────────────────────────

        private void BtnSpeedUp_Click(object sender, RoutedEventArgs e)
        {
            foreach (var step in _steps)
            {
                step.DelayMs = Math.Max(30, step.DelayMs / 2);
            }
            UpdateDuration();
            MessageBox.Show("Macro sped up by 2×", "Speed Up", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSlowDown_Click(object sender, RoutedEventArgs e)
        {
            foreach (var step in _steps)
            {
                step.DelayMs = step.DelayMs * 2;
            }
            UpdateDuration();
            MessageBox.Show("Macro slowed down by 2×", "Slow Down", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            int removed = 0;
            var optimized = _steps.Where(s => s.DelayMs >= 30).ToList();
            removed = _steps.Count - optimized.Count;
            
            _steps.Clear();
            foreach (var step in optimized)
            {
                _steps.Add(step);
            }
            
            ReindexSteps();
            UpdateDuration();
            MessageBox.Show($"Removed {removed} step(s) with delays <30ms", "Optimize",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            // Build preview string
            var preview = string.Join("\n", _steps.Select(s => 
                $"{s.Index}. {s.Type} {s.Key} (wait {s.DelayMs}ms)"));
            
            MessageBox.Show($"Macro Preview:\n\n{preview}\n\nTotal: {_macro.TotalDurationMs}ms",
                "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Save & Cancel ─────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _macro.Name = NameText.Text;
                _macro.Steps = _steps.Select(vm => vm.ToMacroStep()).ToList();
                UpdateDuration();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_macro, options);
                File.WriteAllText(_filePath, json);

                MessageBox.Show("Macro saved successfully!", "Save Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save macro:\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Discard changes?",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }
    }

    // ── View Model ────────────────────────────────────────────────────────────────

    public class MacroStepViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private int _index;
        private int _delayMs;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public MacroStepType Type { get; set; }
        public VirtualKey Key { get; set; }
        
        public int DelayMs
        {
            get => _delayMs;
            set { _delayMs = value; OnPropertyChanged(nameof(DelayMs)); }
        }

        public MacroStepViewModel(int index, MacroStep step)
        {
            Index = index;
            Type = step.Type;
            Key = step.Key;
            DelayMs = step.DelayMs;
        }

        public MacroStep ToMacroStep()
        {
            return new MacroStep
            {
                Type = Type,
                Key = Key,
                DelayMs = DelayMs
            };
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
