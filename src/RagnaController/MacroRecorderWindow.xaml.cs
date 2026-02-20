using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Text.Json;
using RagnaController.Core;

namespace RagnaController
{
    public partial class MacroRecorderWindow : Window
    {
        private readonly MacroRecorder _recorder;
        private Macro? _recordedMacro;

        public Macro? RecordedMacro => _recordedMacro;

        public MacroRecorderWindow()
        {
            InitializeComponent();
            
            _recorder = new MacroRecorder();
            _recorder.RecordingStarted += OnRecordingStarted;
            _recorder.RecordingStopped += OnRecordingStopped;
            _recorder.StepRecorded += OnStepRecorded;

            UpdateUI();
        }

        // ── Recording Controls ────────────────────────────────────────────────────

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            _recorder.StartRecording();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            string macroName = string.IsNullOrWhiteSpace(MacroNameText.Text)
                ? "Untitled Macro"
                : MacroNameText.Text;

            _recordedMacro = _recorder.StopRecording(macroName);
            UpdateUI();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_recordedMacro == null || _recordedMacro.Steps.Count == 0)
            {
                MessageBox.Show("No macro recorded", "Cannot Save",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Save to file
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RagnaController", "Macros");
                Directory.CreateDirectory(folder);

                string fileName = $"{SanitizeFileName(_recordedMacro.Name)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
                string filePath = Path.Combine(folder, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_recordedMacro, options);
                File.WriteAllText(filePath, json);

                MessageBox.Show($"Macro saved to:\n{filePath}", "Macro Saved",
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

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_recorder.IsRecording)
            {
                _recorder.CancelRecording();
            }

            _recordedMacro = null;
            StepsList.Items.Clear();
            UpdateUI();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_recorder.IsRecording)
            {
                var result = MessageBox.Show(
                    "Recording in progress. Stop and discard?",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No) return;
                
                _recorder.CancelRecording();
            }

            DialogResult = false;
            Close();
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnRecordingStarted()
        {
            Dispatcher.Invoke(() =>
            {
                StepsList.Items.Clear();
                BtnRecord.IsEnabled = false;
                BtnStop.IsEnabled = true;
                BtnSave.IsEnabled = false;
                
                RecordingDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x3A, 0x52));
                StatusText.Text = "Recording...";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x3A, 0x52));
                
                EmptyState.Visibility = Visibility.Collapsed;
            });
        }

        private void OnRecordingStopped()
        {
            Dispatcher.Invoke(() =>
            {
                BtnRecord.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnSave.IsEnabled = StepsList.Items.Count > 0;
                
                RecordingDot.Fill = new SolidColorBrush(Color.FromRgb(0x3D, 0xDB, 0x6E));
                StatusText.Text = "Recording Complete";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDB, 0x6E));
            });
        }

        private void OnStepRecorded(MacroStep step)
        {
            Dispatcher.Invoke(() =>
            {
                int stepNumber = StepsList.Items.Count + 1;
                var stepPanel = CreateStepPanel(stepNumber, step);
                StepsList.Items.Add(stepPanel);

                StepCountText.Text = $"({stepNumber} steps)";
                
                // Auto-scroll to bottom
                if (StepsList.Items.Count > 0)
                    StepsList.ScrollIntoView(StepsList.Items[StepsList.Items.Count - 1]);
            });
        }

        // ── UI Creation ───────────────────────────────────────────────────────────

        private Grid CreateStepPanel(int stepNumber, MacroStep step)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // Step Number
            var numberText = new TextBlock
            {
                Text = stepNumber.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("NeonDimBrush")
            };
            Grid.SetColumn(numberText, 0);
            grid.Children.Add(numberText);

            // Action
            var actionText = new TextBlock
            {
                Text = step.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextHiBrush"),
                FontWeight = FontWeights.Medium
            };
            Grid.SetColumn(actionText, 1);
            grid.Children.Add(actionText);

            // Delay
            var delayText = new TextBlock
            {
                Text = $"{step.DelayMs} ms",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextLowBrush")
            };
            Grid.SetColumn(delayText, 2);
            grid.Children.Add(delayText);

            // Delete Button
            var deleteBtn = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                Tag = stepNumber - 1,
                Style = (Style)FindResource("ConsoleDangerBtn"),
                Padding = new Thickness(0),
                FontSize = 12
            };
            deleteBtn.Click += (s, e) => DeleteStep((int)deleteBtn.Tag);
            Grid.SetColumn(deleteBtn, 3);
            grid.Children.Add(deleteBtn);

            return grid;
        }

        private void DeleteStep(int index)
        {
            // Not yet implemented - would need to modify Macro.Steps
            MessageBox.Show("Step editing coming in future update", "Not Available",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateUI()
        {
            if (StepsList.Items.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                BtnSave.IsEnabled = false;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                BtnSave.IsEnabled = !_recorder.IsRecording;
            }

            StepCountText.Text = $"({StepsList.Items.Count} steps)";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // ── Keyboard Input for Recording ──────────────────────────────────────────

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (!_recorder.IsRecording) return;
            if (e.Key == System.Windows.Input.Key.Escape) return; // Don't record ESC

            var vk = MapWpfKeyToVirtualKey(e.Key);
            if (vk != VirtualKey.None)
            {
                _recorder.RecordKeyPress(vk);
                e.Handled = true;
            }
        }

        private VirtualKey MapWpfKeyToVirtualKey(System.Windows.Input.Key key) => key switch
        {
            System.Windows.Input.Key.A => VirtualKey.A,
            System.Windows.Input.Key.B => VirtualKey.B,
            System.Windows.Input.Key.C => VirtualKey.C,
            System.Windows.Input.Key.D => VirtualKey.D,
            System.Windows.Input.Key.E => VirtualKey.E,
            System.Windows.Input.Key.F => VirtualKey.F,
            System.Windows.Input.Key.X => VirtualKey.X,
            System.Windows.Input.Key.Y => VirtualKey.Y,
            System.Windows.Input.Key.Z => VirtualKey.Z,
            System.Windows.Input.Key.Space => VirtualKey.Space,
            System.Windows.Input.Key.D1 => VirtualKey.Num1,
            System.Windows.Input.Key.D2 => VirtualKey.Num2,
            System.Windows.Input.Key.D3 => VirtualKey.Num3,
            System.Windows.Input.Key.D4 => VirtualKey.Num4,
            System.Windows.Input.Key.F1 => VirtualKey.F1,
            System.Windows.Input.Key.F2 => VirtualKey.F2,
            System.Windows.Input.Key.F3 => VirtualKey.F3,
            System.Windows.Input.Key.F4 => VirtualKey.F4,
            _ => VirtualKey.None
        };
    }
}
