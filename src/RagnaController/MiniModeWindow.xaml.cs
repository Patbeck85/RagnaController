using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RagnaController.Core;

namespace RagnaController
{
    public partial class MiniModeWindow : Window
    {
        public MiniModeWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Switch back to full mode
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.SwitchFromMiniMode();
        }

        public void UpdateState(string profile, string state, bool isActive, CombatState combatState)
        {
            Dispatcher.Invoke(() =>
            {
                ProfileText.Text = profile.ToUpper();
                StateText.Text = state;

                if (isActive)
                {
                    StatusColor.Color = Color.FromRgb(0x3D, 0xDB, 0x6E); // Green
                    StateIconColor.Color = GetStateColor(combatState);
                    StateIcon.Text = GetStateIcon(combatState);
                }
                else
                {
                    StatusColor.Color = Color.FromRgb(0xFF, 0x3A, 0x52); // Red
                    StateIconColor.Color = Color.FromRgb(0x8B, 0x97, 0xCC);
                    StateIcon.Text = "■";
                }

                StateTextColor.Color = StateIconColor.Color;
            });
        }

        private Color GetStateColor(CombatState state) => state switch
        {
            CombatState.Seeking   => Color.FromRgb(0xFF, 0xB8, 0x00),
            CombatState.Engaged   => Color.FromRgb(0x3D, 0xDB, 0x6E),
            CombatState.Attacking => Color.FromRgb(0xFF, 0x3A, 0x52),
            _                     => Color.FromRgb(0x3D, 0x4A, 0x6E)
        };

        private string GetStateIcon(CombatState state) => state switch
        {
            CombatState.Seeking   => "⟳",
            CombatState.Engaged   => "●",
            CombatState.Attacking => "▶",
            _                     => "■"
        };
    }
}
