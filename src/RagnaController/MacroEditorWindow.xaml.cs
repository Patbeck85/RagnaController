using System;
using System.Collections.Generic;
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
        private Macro _macro = null!;
        private readonly string _filePath;
        private ObservableCollection<MacroStepViewModel> _steps = null!;

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
                if (!File.Exists(_filePath))
                {
                    _macro = new Macro { Name = "New Macro", Steps = new List<MacroStep>() };
                }
                else
                {
                    string json = File.ReadAllText(_filePath);
                    _macro = JsonSerializer.Deserialize<Macro>(json) ?? new Macro { Steps = new List<MacroStep>() };
                }
                
                NameText.Text      = _macro.Name;
                LoopCountText.Text = _macro.LoopCount.ToString();
                MacroInfo.Text     = $"Editing: {Path.GetFileName(_filePath)}";
                
                _steps = new ObservableCollection<MacroStepViewModel>(
                    _macro.Steps.Select((s, i) => new MacroStepViewModel(i + 1, s))
                );
                
                StepsGrid.ItemsSource = _steps;
                UpdateDuration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load macro:\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void UpdateDuration()
        {
            if (_steps == null) return;
            int total = _steps.Sum(s => s.DelayMs);
            DurationText.Text = $"Duration: {total}ms";
            if (_macro != null) _macro.TotalDurationMs = total;
        }

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
                if (MessageBox.Show($"Delete step {step.Index}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _steps.Remove(step);
                    ReindexSteps();
                    UpdateDuration();
                }
            }
        }

        private void ReindexSteps()
        {
            for (int i = 0; i < _steps.Count; i++) _steps[i].Index = i + 1;
        }

        private void BtnSpeedUp_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _steps) s.DelayMs = Math.Max(30, s.DelayMs / 2);
            UpdateDuration();
        }

        private void BtnSlowDown_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _steps) s.DelayMs *= 2;
            UpdateDuration();
        }

        private void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            var optimized = _steps.Where(s => s.DelayMs >= 30).ToList();
            _steps.Clear();
            foreach (var s in optimized) _steps.Add(s);
            ReindexSteps();
            UpdateDuration();
        }

        private void BtnAddStep_Click(object sender, RoutedEventArgs e)
        {
            // Add a default 200ms delay step at end
            var newStep = new MacroStep { Type = MacroStepType.Delay, Key = VirtualKey.None, DelayMs = 200 };
            var vm = new MacroStepViewModel(_steps.Count + 1, newStep);
            _steps.Add(vm);
            UpdateDuration();
            // Scroll to new item
            StepsGrid.ScrollIntoView(vm);
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            var p = string.Join("\n", _steps.Take(10).Select(s => s.Type == MacroStepType.KeyPress || s.Type == MacroStepType.Delay
                ? $"{s.Index}. {s.Type}: {s.Key} ({s.DelayMs}ms)"
                : $"{s.Index}. {s.Type} ({s.DelayMs}ms)"));
            MessageBox.Show($"Preview (first 10):\n{p}", "Preview");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _macro.Name      = NameText.Text;
                _macro.LoopCount = int.TryParse(LoopCountText.Text, out int lc) ? Math.Max(0, lc) : 1;
                _macro.Steps     = _steps.Select(vm => vm.ToMacroStep()).ToList();
                File.WriteAllText(_filePath, JsonSerializer.Serialize(_macro, new JsonSerializerOptions { WriteIndented = true }));
                DialogResult = true;
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; this.Close(); }
    }

    public class MacroStepViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private int _index;
        private int _delayMs;
        public int Index { get => _index; set { _index = value; OnPropertyChanged(nameof(Index)); } }
        public MacroStepType Type { get; set; }
        public VirtualKey Key { get; set; }
        public int DelayMs { get => _delayMs; set { _delayMs = value; OnPropertyChanged(nameof(DelayMs)); } }

        public MacroStepViewModel(int index, MacroStep step)
        {
            _index = index;
            Type = step.Type;
            Key = step.Key;
            _delayMs = step.DelayMs;
        }

        public MacroStep ToMacroStep() => new MacroStep { Type = Type, Key = Key, DelayMs = DelayMs };

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}