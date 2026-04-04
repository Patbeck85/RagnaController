using System;
using System.Windows;
using System.Windows.Input;
using RagnaController.Core;

namespace RagnaController
{
    public partial class MacroRecorderWindow : Window
    {
        private readonly MacroRecorder _recorder = new();
        public Macro? RecordedMacro { get; private set; }

        public MacroRecorderWindow()
        {
            InitializeComponent();
            _recorder.StepRecorded += (s) => Dispatcher.Invoke(() => StepsList.Items.Add(s));
            _recorder.RecordingStopped += () => Dispatcher.Invoke(() => {
                BtnSave.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnRecord.IsEnabled = true;
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e) {
            StepsList.Items.Clear();
            _recorder.Start();
            BtnRecord.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnSave.IsEnabled = false;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) {
            RecordedMacro = _recorder.Stop(MacroNameText.Text);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e) {
            _recorder.Cancel();
            StepsList.Items.Clear();
            RecordedMacro = null;
            BtnSave.IsEnabled = false;
            BtnStop.IsEnabled = false;
            BtnRecord.IsEnabled = true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) {
            if (RecordedMacro != null) {
                MacroRecorder.SaveMacro(RecordedMacro);
                DialogResult = true;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            string macroDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RagnaController", "Macros");
            System.IO.Directory.CreateDirectory(macroDir);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title            = "Select macro to edit",
                Filter           = "Makro-Dateien|*.json",
                InitialDirectory = macroDir
            };
            if (dlg.ShowDialog() == true)
                new MacroEditorWindow(dlg.FileName) { Owner = this }.ShowDialog();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (_recorder.IsRecording) {
                if (e.Key == Key.Escape) return;
                VirtualKey vk = (VirtualKey)KeyInterop.VirtualKeyFromKey(e.Key);
                _recorder.RecordKey(vk);
                e.Handled = true;
            }
        }
    }
}