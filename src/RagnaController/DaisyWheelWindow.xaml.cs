using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RagnaController.Core;

namespace RagnaController
{
    /// <summary>
    /// Circular on-screen keyboard (Daisy Wheel).
    ///
    /// 8 sectors, 4 characters each (A/B/X/Y button):
    ///   Sektor 0 (Oben):         A B C D
    ///   Sektor 1 (Oben-Rechts):  E F G H
    ///   Sektor 2 (Rechts):       I J K L
    ///   Sektor 3 (Unten-Rechts): M N O P
    ///   Sektor 4 (Unten):        Q R S T
    ///   Sektor 5 (Unten-Links):  U V W X
    ///   Sektor 6 (Links):        Y Z , .
    ///   Sektor 7 (Oben-Links):   ! ? 1 2
    ///
    /// Steuerung:
    ///   L-Stick  → select sector (deadzone 0.3)
    ///   A/B/X/Y  → Buchstaben 0/1/2/3 des aktiven Sektors tippen
    ///   L3       → Backspace
    ///   R3       → Leerzeichen
    ///   Start    → send text to RO chat and close window
    ///   B (ohne Sektor) → Abbrechen
    /// </summary>
    public partial class DaisyWheelWindow : Window
    {
        // 8 sectors × 4 characters each
        private static readonly string[,] Sectors =
        {
            { "A", "B", "C", "D" },   // 0 Oben
            { "E", "F", "G", "H" },   // 1 Oben-Rechts
            { "I", "J", "K", "L" },   // 2 Rechts
            { "M", "N", "O", "P" },   // 3 Unten-Rechts
            { "Q", "R", "S", "T" },   // 4 Unten
            { "U", "V", "W", "X" },   // 5 Unten-Links
            { "Y", "Z", ",", "." },   // 6 Links
            { "!", "?", "1", "2" },   // 7 Oben-Links
        };

        // Colors matching A/B/X/Y button convention
        private static readonly Color[] BtnColors =
        {
            Color.FromRgb(61,  219, 110),  // A – green
            Color.FromRgb(255,  58,  82),  // B – Rot
            Color.FromRgb( 58, 142, 255),  // X – Blau
            Color.FromRgb(255, 184,   0),  // Y – Gelb
        };

        private static readonly string[] BtnLabels = { "A", "B", "X", "Y" };

        private const double CenterX = 250, CenterY = 250;
        private const double InnerR  = 90,  OuterR  = 195;
        private const double SmallR  = 175; // Radius for character dots

        private int  _activeSector  = -1;
        private string _currentText = "";

        // Visuelle Elemente pro Sektor
        private readonly List<Path>       _sectorPaths = new();
        private readonly List<TextBlock>  _sectorLabels = new();   // Richtungs-Label (N, NE …)
        private readonly List<Border[]>   _sectorBtns  = new();   // 4 Tasten-Dots pro Sektor

        // Used for rising-edge detection in UpdateInput
        private bool _prevA, _prevB, _prevX, _prevY, _prevL3, _prevR3, _prevStart;

        public DaisyWheelWindow()
        {
            InitializeComponent();
            DrawWheel();
        }

        // -------------------------------------------------------
        //  Zeichnen
        // -------------------------------------------------------
        private void DrawWheel()
        {
            WheelCanvas.Children.Clear();
            _sectorPaths.Clear();
            _sectorLabels.Clear();
            _sectorBtns.Clear();

            for (int s = 0; s < 8; s++)
            {
                double startAngle = s * 45.0 - 90.0 - 22.5; // -90 so sector 0 is at top
                double endAngle   = startAngle + 45.0;
                double midAngle   = (startAngle + endAngle) / 2.0;

                // --- Sektor-Pfad ---
                var path = MakeSectorPath(startAngle, endAngle, InnerR, OuterR);
                path.Fill            = new SolidColorBrush(Color.FromArgb(40, 212, 168, 50));
                path.Stroke          = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                path.StrokeThickness = 1.5;
                WheelCanvas.Children.Add(path);
                _sectorPaths.Add(path);

                // --- 4 Buchstaben-Dots (A/B/X/Y) ---
                var btns = new Border[4];
                for (int b = 0; b < 4; b++)
                {
                    // Die 4 Dots leicht versetzt um den Sektor-Mittelpunkt
                    double dotAngleDeg = midAngle + (b - 1.5) * (30.0 / 3.0);
                    double dotRadius   = (InnerR + OuterR) / 2.0;
                    double dotRad      = dotAngleDeg * Math.PI / 180.0;
                    double dotX = CenterX + Math.Cos(dotRad) * dotRadius - 14;
                    double dotY = CenterY + Math.Sin(dotRad) * dotRadius - 14;

                    var dot = new Border
                    {
                        Width           = 28, Height = 28,
                        CornerRadius    = new CornerRadius(14),
                        Background      = new SolidColorBrush(Color.FromArgb(80, BtnColors[b].R, BtnColors[b].G, BtnColors[b].B)),
                        BorderBrush     = new SolidColorBrush(BtnColors[b]),
                        BorderThickness = new Thickness(1.5),
                        Child = new TextBlock
                        {
                            Text                = Sectors[s, b],
                            Foreground          = new SolidColorBrush(BtnColors[b]),
                            FontSize            = 11,
                            FontWeight          = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment   = VerticalAlignment.Center
                        }
                    };
                    Canvas.SetLeft(dot, dotX);
                    Canvas.SetTop(dot, dotY);
                    WheelCanvas.Children.Add(dot);
                    btns[b] = dot;
                }
                _sectorBtns.Add(btns);
            }
        }

        private static Path MakeSectorPath(double startDeg, double endDeg, double inner, double outer)
        {
            double s1 = startDeg * Math.PI / 180.0, s2 = endDeg * Math.PI / 180.0;
            var p1o = new Point(CenterX + Math.Cos(s1) * outer, CenterY + Math.Sin(s1) * outer);
            var p2o = new Point(CenterX + Math.Cos(s2) * outer, CenterY + Math.Sin(s2) * outer);
            var p1i = new Point(CenterX + Math.Cos(s2) * inner, CenterY + Math.Sin(s2) * inner);
            var p2i = new Point(CenterX + Math.Cos(s1) * inner, CenterY + Math.Sin(s1) * inner);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = p1o, IsClosed = true };
            fig.Segments.Add(new ArcSegment(p2o, new Size(outer, outer), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(p1i, true));
            fig.Segments.Add(new ArcSegment(p2i, new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true));
            geo.Figures.Add(fig);
            return new Path { Data = geo };
        }

        // -------------------------------------------------------
        // Input — called by HybridEngine every tick
        // -------------------------------------------------------
        public bool UpdateInput(
            float lx, float ly,
            bool a, bool b, bool x, bool y,
            bool l3, bool r3, bool start, bool bBtn)
        {
            // Sektor bestimmen
            float mag = MathF.Sqrt(lx * lx + ly * ly);
            if (mag > 0.30f)
            {
                double angle = (Math.Atan2(ly, lx) * 180.0 / Math.PI + 360.0 + 90.0) % 360.0;
                _activeSector = (int)(angle / 45.0) % 8;
            }
            else
            {
                _activeSector = -1;
            }

            HighlightSector(_activeSector);

            // Type character on fresh key press (rising edge)
            if (_activeSector >= 0)
            {
                if (a && !_prevA) TypeChar(Sectors[_activeSector, 0]);
                if (b && !_prevB) TypeChar(Sectors[_activeSector, 1]);
                if (x && !_prevX) TypeChar(Sectors[_activeSector, 2]);
                if (y && !_prevY) TypeChar(Sectors[_activeSector, 3]);
            }

            if (l3 && !_prevL3 && _currentText.Length > 0)
                SetText(_currentText[..^1]);              // Backspace

            if (r3 && !_prevR3)
                TypeChar(" ");                             // Leerzeichen

            _prevA = a; _prevB = b; _prevX = x; _prevY = y;
            _prevL3 = l3; _prevR3 = r3;

            // Start → submit and close
            if (start && !_prevStart)
            {
                _prevStart = start;
                _ = InputSimulator.SendChatString(_currentText);
                Dispatcher.Invoke(() => { try { Close(); } catch { } });
                return true; // signals window closed
            }
            _prevStart = start;

            // B without sector → cancel
            if (bBtn && !_prevB && _activeSector < 0)
            {
                Dispatcher.Invoke(() => { try { Close(); } catch { } });
                return true;
            }

            return false;
        }

        private void TypeChar(string c)
        {
            SetText(_currentText + c);
        }

        private void SetText(string t)
        {
            _currentText = t;
            Dispatcher.Invoke(() =>
            {
                CurrentTextBlock.Text = t.Length > 16 ? "…" + t[^16..] : t;
            });
        }

        private void HighlightSector(int active)
        {
            Dispatcher.Invoke(() =>
            {
                for (int s = 0; s < 8; s++)
                {
                    bool on = s == active;
                    _sectorPaths[s].Fill = new SolidColorBrush(
                        on ? Color.FromArgb(130, 212, 168, 50)
                           : Color.FromArgb(40,  212, 168, 50));
                    _sectorPaths[s].Stroke = new SolidColorBrush(
                        on ? Color.FromRgb(212, 168, 50)
                           : Color.FromRgb(33, 38, 45));

                    for (int b = 0; b < 4; b++)
                    {
                        var dot = _sectorBtns[s][b];
                        ((TextBlock)dot.Child).Foreground = on
                            ? new SolidColorBrush(BtnColors[b])
                            : new SolidColorBrush(Color.FromArgb(120, BtnColors[b].R, BtnColors[b].G, BtnColors[b].B));
                        dot.BorderBrush = new SolidColorBrush(
                            on ? BtnColors[b]
                               : Color.FromArgb(80, BtnColors[b].R, BtnColors[b].G, BtnColors[b].B));
                    }
                }
            });
        }
    }
}
