using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;

namespace RagnaController
{
    /// <summary>
    /// Live controller preview - shows button presses in real-time
    /// </summary>
    public class ControllerPreview : Canvas
    {
        private readonly Dictionary<string, Ellipse> _buttonIndicators = new();
        private readonly SolidColorBrush _inactiveBrush = new(Color.FromRgb(0x1A, 0x20, 0x30));
        private readonly SolidColorBrush _activeBrush = new(Color.FromRgb(0x00, 0xCF, 0xFF));

        public ControllerPreview()
        {
            Width = 400;
            Height = 250;
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x10, 0x17));

            InitializeController();
        }

        private void InitializeController()
        {
            // Controller body outline
            var body = new Ellipse
            {
                Width = 320,
                Height = 180,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(body, 40);
            Canvas.SetTop(body, 35);
            Children.Add(body);

            // Face buttons (right side)
            AddButton("A", 280, 130, Color.FromRgb(0x3D, 0xDB, 0x6E));
            AddButton("B", 310, 100, Color.FromRgb(0xFF, 0x3A, 0x52));
            AddButton("X", 250, 100, Color.FromRgb(0x3A, 0x8E, 0xFF));
            AddButton("Y", 280, 70, Color.FromRgb(0xFF, 0xB8, 0x00));

            // D-Pad (left side)
            AddButton("DPadUp", 120, 80, Color.FromRgb(0x8B, 0x97, 0xCC));
            AddButton("DPadDown", 120, 130, Color.FromRgb(0x8B, 0x97, 0xCC));
            AddButton("DPadLeft", 95, 105, Color.FromRgb(0x8B, 0x97, 0xCC));
            AddButton("DPadRight", 145, 105, Color.FromRgb(0x8B, 0x97, 0xCC));

            // Shoulders
            AddButton("LeftShoulder", 80, 30, Color.FromRgb(0x9F, 0x7A, 0xFF));
            AddButton("RightShoulder", 300, 30, Color.FromRgb(0x9F, 0x7A, 0xFF));

            // Special buttons
            AddButton("Start", 230, 105, Color.FromRgb(0x8B, 0x97, 0xCC));
            AddButton("Back", 170, 105, Color.FromRgb(0x8B, 0x97, 0xCC));

            // Sticks (show as larger circles)
            var leftStick = new Ellipse
            {
                Width = 30,
                Height = 30,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                StrokeThickness = 2,
                Fill = _inactiveBrush
            };
            Canvas.SetLeft(leftStick, 130);
            Canvas.SetTop(leftStick, 140);
            Children.Add(leftStick);
            _buttonIndicators["LeftStick"] = leftStick;

            var rightStick = new Ellipse
            {
                Width = 30,
                Height = 30,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                StrokeThickness = 2,
                Fill = _inactiveBrush
            };
            Canvas.SetLeft(rightStick, 240);
            Canvas.SetTop(rightStick, 140);
            Children.Add(rightStick);
            _buttonIndicators["RightStick"] = rightStick;

            // Labels
            var label = new TextBlock
            {
                Text = "LIVE PREVIEW",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x6E)),
                CharacterSpacing = 150
            };
            Canvas.SetLeft(label, 140);
            Canvas.SetTop(label, 215);
            Children.Add(label);
        }

        private void AddButton(string name, double x, double y, Color color)
        {
            var button = new Ellipse
            {
                Width = 24,
                Height = 24,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                Fill = _inactiveBrush
            };
            Canvas.SetLeft(button, x - 12);
            Canvas.SetTop(button, y - 12);
            Children.Add(button);
            _buttonIndicators[name] = button;

            // Add label
            var label = new TextBlock
            {
                Text = GetButtonLabel(name),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };
            Canvas.SetLeft(label, x - 6);
            Canvas.SetTop(label, y - 5);
            Children.Add(label);
        }

        private string GetButtonLabel(string buttonName) => buttonName switch
        {
            "A" => "A",
            "B" => "B",
            "X" => "X",
            "Y" => "Y",
            "DPadUp" => "↑",
            "DPadDown" => "↓",
            "DPadLeft" => "←",
            "DPadRight" => "→",
            "LeftShoulder" => "LB",
            "RightShoulder" => "RB",
            "Start" => "▶",
            "Back" => "◀",
            _ => ""
        };

        public void UpdateButton(string buttonName, bool isPressed)
        {
            if (_buttonIndicators.TryGetValue(buttonName, out var indicator))
            {
                indicator.Fill = isPressed ? _activeBrush : _inactiveBrush;
                
                if (isPressed)
                {
                    indicator.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0x00, 0xCF, 0xFF),
                        BlurRadius = 12,
                        ShadowDepth = 0,
                        Opacity = 0.8
                    };
                }
                else
                {
                    indicator.Effect = null;
                }
            }
        }
    }
}
