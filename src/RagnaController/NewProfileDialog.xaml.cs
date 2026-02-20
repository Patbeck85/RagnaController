using System.Windows;
using System.Windows.Input;

namespace RagnaController
{
    public partial class NewProfileDialog : Window
    {
        public string ProfileName        { get; private set; } = string.Empty;
        public string ProfileDescription { get; private set; } = string.Empty;
        public string ProfileClass       { get; private set; } = "Melee";

        public NewProfileDialog() => InitializeComponent();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Please enter a profile name.", "RagnaController",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ProfileName        = TxtName.Text.Trim();
            ProfileDescription = TxtDescription.Text.Trim();
            ProfileClass       = (CmbClass.SelectedItem as System.Windows.Controls.ComboBoxItem)
                                 ?.Content?.ToString() ?? "Melee";
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
