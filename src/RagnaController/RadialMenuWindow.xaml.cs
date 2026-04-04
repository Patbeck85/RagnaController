using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class RadialMenuWindow : Window
    {
        private readonly List<RadialItem> _items;
        private int _selectedIndex = -1;
        private readonly List<Border> _visualItems = new();

        public RadialMenuWindow(List<RadialItem> items)
        {
            InitializeComponent();
            _items = items;
            DrawItems();
        }

        private void DrawItems()
        {
            if (ItemsCanvas == null) return;
            ItemsCanvas.Children.Clear();
            _visualItems.Clear();
            if (_items == null || _items.Count == 0) return;

            double angleStep = 360.0 / _items.Count;
            for (int i = 0; i < _items.Count; i++)
            {
                double angle = i * angleStep - 90;
                double rad   = angle * Math.PI / 180.0;

                bool hasImage = !string.IsNullOrEmpty(_items[i].ImagePath) && File.Exists(_items[i].ImagePath);
                double itemH  = hasImage ? 52 : 36;
                double x = 170 + Math.Cos(rad) * 128 - 50;
                double y = 170 + Math.Sin(rad) * 128 - (itemH / 2);

                var stack = new StackPanel
                {
                    VerticalAlignment   = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Load image if path exists
                if (hasImage)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource   = new Uri(_items[i].ImagePath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelHeight = 28;
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new Image
                        {
                            Source  = bmp,
                            Width   = 28, Height = 28,
                            Stretch = Stretch.Uniform,
                            Margin  = new Thickness(0, 0, 0, 3)
                        };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                        stack.Children.Add(img);
                    }
                    catch { /* Bild nicht ladbar → trotzdem Text zeigen */ }
                }

                stack.Children.Add(new TextBlock
                {
                    Text                = _items[i].Name,
                    Foreground          = new SolidColorBrush(Color.FromRgb(125, 139, 158)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize            = hasImage ? 9 : 11,
                    FontWeight          = FontWeights.Bold
                });

                var b = new Border
                {
                    Width           = 100,
                    Height          = itemH,
                    Background      = new SolidColorBrush(Color.FromArgb(100, 18, 22, 32)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(42, 50, 69)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(6),
                    Child           = stack
                };

                Canvas.SetLeft(b, x);
                Canvas.SetTop(b, y);
                ItemsCanvas.Children.Add(b);
                _visualItems.Add(b);
            }
        }

        public void UpdateSelection(float x, float y)
        {
            if (RootGrid == null) return;
            if (RootGrid.Opacity < 1) RootGrid.Opacity = 1;
            float mag = MathF.Sqrt(x * x + y * y);

            if (mag < 0.4f)
            {
                _selectedIndex  = -1;
                SelectedText.Text = "SELECT ITEM";
                Reset();
                return;
            }

            double angle = (Math.Atan2(-y, x) * 180.0 / Math.PI + 450) % 360;
            _selectedIndex = (int)(angle / (360.0 / _items.Count));
            if (_selectedIndex >= _items.Count) _selectedIndex = _items.Count - 1;

            Reset();
            if (_selectedIndex >= 0 && _selectedIndex < _visualItems.Count)
            {
                var b = _visualItems[_selectedIndex];
                b.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 184, 66));
                b.Background  = new SolidColorBrush(Color.FromArgb(100, 229, 184, 66));
                SetTextColor(b, Color.FromRgb(240, 244, 248));
                SelectedText.Text = _items[_selectedIndex].Name;
            }
        }

        private void Reset()
        {
            foreach (var b in _visualItems)
            {
                b.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 50, 69));
                b.Background  = new SolidColorBrush(Color.FromArgb(100, 18, 22, 32));
                SetTextColor(b, Color.FromRgb(125, 139, 158));
            }
        }

        private static void SetTextColor(Border b, Color c)
        {
            if (b.Child is StackPanel sp)
                foreach (var child in sp.Children)
                    if (child is TextBlock t) t.Foreground = new SolidColorBrush(c);
        }

        public void ExecuteAndClose()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            {
                var item = _items[_selectedIndex];
                if (item.IsEmote && !string.IsNullOrWhiteSpace(item.Command))
                    _ = InputSimulator.SendChatString(item.Command);
                else if (item.Key != VirtualKey.None)
                    InputSimulator.TapKey(item.Key);
            }
            Close();
        }
    }
}
