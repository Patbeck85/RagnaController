using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using RagnaController.Core;

namespace RagnaController
{
    public partial class MiniModeWindow : Window
    {
        // Win32 API for click-through overlay (WS_EX_TRANSPARENT)
        private const int GWL_EXSTYLE     = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED    = 0x00080000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);

        private bool _clickThrough = false;

        public MiniModeWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateClickThrough();
        }

        // Click-Through umschalten (Rechtsklick auf X-Button)
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // FIX: Sichere Methode, um das MainWindow zu finden, das dieses Mini-Fenster geöffnet hat.
            if (this.Owner is MainWindow mainOwner)
            {
                mainOwner.SwitchFromMiniMode();
            }
            else
            {
                // Fallback, falls der Owner aus irgendeinem Grund verloren ging
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow)
                    {
                        mainWindow.SwitchFromMiniMode();
                        return;
                    }
                }
                // Absoluter Notfall-Fallback
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _clickThrough = !_clickThrough;
            UpdateClickThrough();
            // Visuelles Feedback: Rand blinkt kurz
            BorderBrush = _clickThrough
                ? new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xFF))
                : new SolidColorBrush(Color.FromRgb(0xE5, 0xB8, 0x42));
        }

        private void UpdateClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (_clickThrough)
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }

        public void UpdateState(string profile, string state, bool active, CombatState combat)
        {
            Dispatcher.Invoke(() =>
            {
                if (ProfileText == null) return;
                ProfileText.Text = profile.ToUpper();
                StateText.Text   = state;
                StatusBrush.Color  = active ? Color.FromRgb(0x3D, 0xDB, 0x6E) : Color.FromRgb(0xFF, 0x3A, 0x52);
                StateIcon.Text = combat switch
                {
                    CombatState.Seeking   => "⟳",
                    CombatState.Engaged   => "●",
                    CombatState.Attacking => "▶",
                    _                     => "■"
                };
                Color iconCol = active
                    ? (combat switch
                    {
                        CombatState.Seeking   => Color.FromRgb(0xFF, 0xB8, 0x00),
                        CombatState.Attacking => Color.FromRgb(0xFF, 0x3A, 0x52),
                        _                     => Color.FromRgb(0x3D, 0xDB, 0x6E)
                    })
                    : Color.FromRgb(0x8B, 0x97, 0xCC);
                StateIconBrush.Color = iconCol;
                StateTextBrush.Color = iconCol;
            });
        }
    }
}