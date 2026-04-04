using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Effects;

namespace RagnaController
{
    public class ControllerPreview : Canvas
    {
        private readonly Dictionary<string, Shape> _shapes = new();
        private readonly Dictionary<string, TextBlock> _labels = new();
        private string? _highlight;
        private readonly SolidColorBrush _body = new(Color.FromRgb(15, 18, 25)), _border = new(Color.FromRgb(40, 50, 70)), _off = new(Color.FromRgb(25, 30, 40)), _gold = new(Color.FromRgb(212, 168, 50)), _dim = new(Color.FromRgb(80, 90, 110));
        
        public ControllerPreview() { Width = 500; Height = 320; Background = Brushes.Transparent; Init(); }

        private void Init() {
            Path b = new() { Stroke = _border, StrokeThickness = 3, Fill = _body, Data = Geometry.Parse("M 130,70 L 370,70 C 450,70 490,120 490,180 C 490,270 420,300 370,250 L 320,230 L 180,230 L 130,250 C 80,300 10,270 10,180 C 10,120 50,70 130,70") };
            Children.Add(b);
            Btn("LeftTrigger", 90, 15, 90, 40, "LT"); Btn("RightTrigger", 320, 15, 90, 40, "RT");
            Btn("LeftShoulder", 90, 60, 80, 20, "LB"); Btn("RightShoulder", 330, 60, 80, 20, "RB");
            Circ("LeftStick", 140, 165, 75, "L3", _border); Circ("RightStick", 285, 165, 75, "R3", _border);
            Circ("A", 390, 160, 42, "A", new(Color.FromRgb(61, 219, 110))); Circ("B", 440, 120, 42, "B", new(Color.FromRgb(255, 58, 82)));
            Circ("X", 340, 120, 42, "X", new(Color.FromRgb(58, 142, 255))); Circ("Y", 390, 80, 42, "Y", new(Color.FromRgb(255, 184, 0)));
            Btn("DPadUp", 70, 105, 40, 40, "↑"); Btn("DPadDown", 70, 190, 40, 40, "↓");
            Btn("DPadLeft", 35, 147, 40, 40, "←"); Btn("DPadRight", 105, 147, 40, 40, "→");
            Btn("Back", 210, 115, 35, 20, "SEL"); Btn("Start", 255, 115, 35, 20, "STA");
        }

        private void Circ(string id, double x, double y, double s, string l, SolidColorBrush c) {
            Ellipse e = new() { Width = s, Height = s, Fill = _off, Stroke = c, StrokeThickness = 2.5 };
            SetLeft(e, x); SetTop(e, y); _shapes[id] = e; Children.Add(e);
            TextBlock t = new() { Text = l, Foreground = c, FontSize = 12, FontWeight = FontWeights.Bold, Width = s, TextAlignment = TextAlignment.Center };
            SetLeft(t, x); SetTop(t, y + (s / 2) - 9); _labels[id] = t; Children.Add(t);
        }

        private void Btn(string id, double x, double y, double w, double h, string l) {
            Rectangle r = new() { Width = w, Height = h, Fill = _off, Stroke = _border, StrokeThickness = 2, RadiusX = 6, RadiusY = 6 };
            SetLeft(r, x); SetTop(r, y); _shapes[id] = r; Children.Add(r);
            TextBlock t = new() { Text = l, Foreground = _dim, FontSize = 11, FontWeight = FontWeights.Bold, Width = w, TextAlignment = TextAlignment.Center };
            SetLeft(t, x); SetTop(t, y + (h / 2) - 9); _labels[id] = t; Children.Add(t);
        }

        public void HighlightButton(string id) {
            if (_highlight != null && _shapes.TryGetValue(_highlight, out var o)) { o.Fill = _off; o.Effect = null; if (_labels.TryGetValue(_highlight, out var l)) l.Foreground = o is Ellipse ? o.Stroke : _dim; }
            string mapped = id == "LeftThumb" ? "LeftStick" : id == "RightThumb" ? "RightStick" : id;
            if (_shapes.TryGetValue(mapped, out var s)) { _highlight = mapped; s.Fill = new SolidColorBrush(Color.FromArgb(60, 212, 168, 50)); s.Effect = new DropShadowEffect { Color = Color.FromRgb(212, 168, 50), BlurRadius = 20, ShadowDepth = 0, Opacity = 0.95 }; if (_labels.TryGetValue(mapped, out var l)) l.Foreground = _gold; }
        }
    }
}