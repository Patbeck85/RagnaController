using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public partial class ComboEditorWindow : Window
    {
        private readonly Profile _profile;
        private bool _showRenewal = true;

        // --- Klassen-Templates ---
        private record ComboTemplate(
            string ClassName,
            string Icon,
            string Hint,
            string[] SkillNames,
            VirtualKey[] Keys,
            int[] PreDelays,
            int[] RenDelays);

        private static readonly ComboTemplate[] Templates = {
            new("Monk", "🥋",
                "Triple Attack triggers automatically on auto-attack. Then HOLD → Chain Combo → Combo Finish → Asura Strike.",
                new[]{ "Chain Combo", "Combo Finish", "Asura Strike" },
                new[]{ VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 },
                new[]{ 350, 400, 1000 }, new[]{ 250, 300, 800 }),

            new("Champion", "👊",
                "Same chain as Monk with shorter delays due to higher base stats.",
                new[]{ "Chain Combo", "Combo Finish", "Asura Strike" },
                new[]{ VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 },
                new[]{ 300, 350, 900 }, new[]{ 220, 280, 750 }),

            new("Taekwon", "🦵",
                "Activate Kick Stance, then automatically fire Combo Kick.",
                new[]{ "Kick Stance", "Combo Kick" },
                new[]{ VirtualKey.F1, VirtualKey.F2 },
                new[]{ 300, 400 }, new[]{ 200, 300 }),

            new("Star Gladiator", "⭐",
                "Solar/Lunar Stance → Mild Wind → Stellar Kick. Ideal for fast burst combos.",
                new[]{ "Solar Stance", "Mild Wind", "Stellar Kick" },
                new[]{ VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 },
                new[]{ 280, 350, 450 }, new[]{ 200, 270, 350 }),

            new("Soul Linker", "🔮",
                "Prepare two Ka spells, then Esma to deal maximum damage automatically.",
                new[]{ "Ka Spell 1", "Ka Spell 2", "Esma" },
                new[]{ VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 },
                new[]{ 400, 400, 600 }, new[]{ 300, 300, 500 }),

            new("Ninja", "🗡",
                "Throw Shuriken, then Shadow Leap for positioning, Final Strike to finish.",
                new[]{ "Throw Shuriken", "Shadow Leap", "Final Strike" },
                new[]{ VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 },
                new[]{ 200, 300, 400 }, new[]{ 150, 220, 320 }),

            new("Gunslinger", "🔫",
                "Desperado sustained fire followed by Chain Action as a follow-up.",
                new[]{ "Desperado", "Chain Action" },
                new[]{ VirtualKey.F1, VirtualKey.F2 },
                new[]{ 200, 300 }, new[]{ 150, 220 }),

            new("Sniper", "🏹",
                "Arrow Shower for AoE, immediately followed by Focused Arrow Strike.",
                new[]{ "Arrow Shower", "Focused Arrow Strike" },
                new[]{ VirtualKey.F1, VirtualKey.F2 },
                new[]{ 220, 320 }, new[]{ 160, 240 }),
        };

        // Interne Step-Liste
        private readonly List<ComboStep> _steps = new();

        private class ComboStep
        {
            public string    SkillName { get; set; } = "Skill";
            public VirtualKey Key      { get; set; } = VirtualKey.F1;
            public int       PreDelay  { get; set; } = 300;
            public int       RenDelay  { get; set; } = 200;
        }

        // F1-F12 and common keys for quick selection
        private static readonly (string Label, VirtualKey Key)[] QuickKeys =
        {
            ("F1",  VirtualKey.F1),  ("F2",  VirtualKey.F2),  ("F3",  VirtualKey.F3),
            ("F4",  VirtualKey.F4),  ("F5",  VirtualKey.F5),  ("F6",  VirtualKey.F6),
            ("F7",  VirtualKey.F7),  ("F8",  VirtualKey.F8),  ("F9",  VirtualKey.F9),
            ("F10", VirtualKey.F10), ("F11", VirtualKey.F11), ("F12", VirtualKey.F12),
            ("Z",   VirtualKey.Z),   ("X",   VirtualKey.X),   ("C",   VirtualKey.C),
            ("V",   VirtualKey.V),
        };

        public ComboEditorWindow(Profile profile)
        {
            InitializeComponent();
            _profile = profile;
            BuildClassCards();
            LoadFromProfile();
            RefreshChain();
        }

        // -------------------------------------------------------
        //  Klassen-Karten
        // -------------------------------------------------------
        private void BuildClassCards()
        {
            ClassCards.Children.Clear();
            foreach (var t in Templates)
            {
                var card = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock { Text = t.Icon, FontSize = 14, Margin = new Thickness(0,0,5,0), VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock { Text = t.ClassName, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }
                        }
                    },
                    Height = 34,
                    Padding = new Thickness(10, 0, 10, 0),
                    Margin = new Thickness(0, 0, 6, 6),
                    Style = (Style)Application.Current.Resources["ConsoleGhostBtn"],
                    Tag = t
                };
                card.Click += (s, e) =>
                {
                    if (card.Tag is ComboTemplate tmpl) LoadTemplate(tmpl);
                };
                ClassCards.Children.Add(card);
            }
        }

        private void LoadTemplate(ComboTemplate t)
        {
            _steps.Clear();
            for (int i = 0; i < t.SkillNames.Length; i++)
                _steps.Add(new ComboStep
                {
                    SkillName = t.SkillNames[i],
                    Key       = i < t.Keys.Length      ? t.Keys[i]      : VirtualKey.F1,
                    PreDelay  = i < t.PreDelays.Length  ? t.PreDelays[i] : 300,
                    RenDelay  = i < t.RenDelays.Length  ? t.RenDelays[i] : 200,
                });
            HintText.Text = t.Hint;
            RefreshChain();
        }

        private void LoadFromProfile()
        {
            ChkEnabled.IsChecked = _profile.ComboEnabled;
            _steps.Clear();
            int count = Math.Max(_profile.ComboSequenceVK.Count,
                        Math.Max(_profile.PreRenewalComboDelays.Count,
                                 _profile.RenewalComboDelays.Count));
            for (int i = 0; i < count; i++)
                _steps.Add(new ComboStep
                {
                    SkillName = i < _profile.ComboSkillNames.Count ? _profile.ComboSkillNames[i] : $"Skill {i + 1}",
                    Key       = i < _profile.ComboSequenceVK.Count  ? _profile.ComboSequenceVK[i]      : VirtualKey.F1,
                    PreDelay  = i < _profile.PreRenewalComboDelays.Count ? _profile.PreRenewalComboDelays[i] : 300,
                    RenDelay  = i < _profile.RenewalComboDelays.Count    ? _profile.RenewalComboDelays[i]   : 200,
                });

            // Set class hint text if a matching template is found
            var tmpl = Templates.FirstOrDefault(t =>
                t.ClassName.Equals(_profile.Name, StringComparison.OrdinalIgnoreCase) ||
                _profile.Name.Contains(t.ClassName, StringComparison.OrdinalIgnoreCase));
            HintText.Text = tmpl?.Hint ?? "Configure your combo chain. Select a class above for a pre-built template.";
        }

        // -------------------------------------------------------
        // Render visual chain
        // -------------------------------------------------------
        private void RefreshChain()
        {
            ChainPanel.Children.Clear();

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                int idx  = i; // capture

                // Step card
                var card = new Border
                {
                    Background      = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(12),
                    Margin          = new Thickness(0, 0, 0, 4),
                };

                var cardGrid = new Grid();
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });      // Badge
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });     // Key-Picker
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });     // Delay

                // Step-Badge
                var badge = new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(Color.FromRgb(212, 168, 50)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = (i + 1).ToString(), Foreground = new SolidColorBrush(Color.FromRgb(13, 16, 23)),
                        FontWeight = FontWeights.Bold, FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(badge, 0); cardGrid.Children.Add(badge);

                // Skill-Name (editierbar)
                var nameBox = new TextBox
                {
                    Text = step.SkillName,
                    Background = new SolidColorBrush(Color.FromRgb(13, 16, 23)),
                    Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                    FontSize = 13, FontWeight = FontWeights.Bold,
                    Padding = new Thickness(6, 4, 6, 4),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 12, 0)
                };
                nameBox.TextChanged += (s, e) => step.SkillName = nameBox.Text;
                Grid.SetColumn(nameBox, 1); cardGrid.Children.Add(nameBox);

                // Key-Picker (F1-F12 + Z/X/C/V als Toggle-Buttons)
                var keyPanel = new WrapPanel { Margin = new Thickness(0, 0, 12, 0) };
                foreach (var (label, key) in QuickKeys)
                {
                    var kBtn = new ToggleButton
                    {
                        Content    = label,
                        IsChecked  = step.Key == key,
                        Width = 32, Height = 22,
                        Margin = new Thickness(2, 1, 0, 1),
                        FontSize = 9,
                        Background = step.Key == key
                            ? new SolidColorBrush(Color.FromRgb(0, 80, 80))
                            : new SolidColorBrush(Color.FromRgb(26, 32, 48)),
                        Foreground = step.Key == key ? Brushes.Cyan : new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                        BorderThickness = new Thickness(1),
                        Tag = key
                    };
                    var capturedKey = key;
                    kBtn.Checked += (s, e) =>
                    {
                        step.Key = capturedKey;
                        // Deselect all other keys
                        foreach (ToggleButton tb in keyPanel.Children.OfType<ToggleButton>())
                        {
                            bool isThis = (VirtualKey)tb.Tag == capturedKey;
                            tb.IsChecked  = isThis;
                            tb.Background = isThis
                                ? new SolidColorBrush(Color.FromRgb(0, 80, 80))
                                : new SolidColorBrush(Color.FromRgb(26, 32, 48));
                            tb.Foreground = isThis ? Brushes.Cyan : new SolidColorBrush(Color.FromRgb(139, 148, 158));
                        }
                    };
                    keyPanel.Children.Add(kBtn);
                }
                Grid.SetColumn(keyPanel, 2); cardGrid.Children.Add(keyPanel);

                // Delay-Anzeige + Slider
                var delay = _showRenewal ? step.RenDelay : step.PreDelay;
                var delayPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var delayLabel = new TextBlock
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(212, 168, 50)),
                    FontSize = 10, FontFamily = new FontFamily("Consolas"),
                    Text = $"⏱ {delay} ms"
                };
                var delaySlider = new Slider
                {
                    Minimum = 50, Maximum = 2000, Value = delay,
                    Width = 140, TickFrequency = 50, IsSnapToTickEnabled = true,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                delaySlider.ValueChanged += (s, e) =>
                {
                    int v = (int)delaySlider.Value;
                    if (_showRenewal) step.RenDelay = v; else step.PreDelay = v;
                    delayLabel.Text = $"⏱ {v} ms";
                };
                delayPanel.Children.Add(delayLabel);
                delayPanel.Children.Add(delaySlider);
                Grid.SetColumn(delayPanel, 3); cardGrid.Children.Add(delayPanel);

                card.Child = cardGrid;
                ChainPanel.Children.Add(card);

                // --- Pfeil zwischen Schritten ---
                if (i < _steps.Count - 1)
                {
                    ChainPanel.Children.Add(new TextBlock
                    {
                        Text = "  ↓",
                        Foreground = new SolidColorBrush(Color.FromRgb(85, 94, 106)),
                        FontSize = 16, Margin = new Thickness(6, 0, 0, 4)
                    });
                }
            }

            if (_steps.Count == 0)
                ChainPanel.Children.Add(new TextBlock
                {
                    Text = "No steps defined. Select a class above or add steps manually.",
                    Foreground = new SolidColorBrush(Color.FromRgb(85, 94, 106)),
                    FontSize = 11, Margin = new Thickness(4, 10, 0, 0), TextWrapping = TextWrapping.Wrap
                });
        }

        // -------------------------------------------------------
        //  Delay-Tab Umschalten
        // -------------------------------------------------------
        private void BtnTabPre_Click(object s, RoutedEventArgs e)
        {
            _showRenewal = false;
            BtnTabPre.Style = (Style)Application.Current.Resources["ConsolePrimaryBtn"];
            BtnTabRen.Style = (Style)Application.Current.Resources["ConsoleGhostBtn"];
            RefreshChain();
        }

        private void BtnTabRen_Click(object s, RoutedEventArgs e)
        {
            _showRenewal = true;
            BtnTabRen.Style = (Style)Application.Current.Resources["ConsolePrimaryBtn"];
            BtnTabPre.Style = (Style)Application.Current.Resources["ConsoleGhostBtn"];
            RefreshChain();
        }

        // -------------------------------------------------------
        // Step add / remove
        // -------------------------------------------------------
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _steps.Add(new ComboStep { SkillName = $"Skill {_steps.Count + 1}" });
            RefreshChain();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_steps.Count > 0) { _steps.RemoveAt(_steps.Count - 1); RefreshChain(); }
        }

        // -------------------------------------------------------
        //  Speichern
        // -------------------------------------------------------
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _profile.ComboEnabled            = ChkEnabled.IsChecked == true;
            _profile.ComboSkillNames         = _steps.Select(s => s.SkillName).ToList();
            _profile.ComboSequenceVK         = _steps.Select(s => s.Key).ToList();
            _profile.PreRenewalComboDelays   = _steps.Select(s => Math.Max(50, s.PreDelay)).ToList();
            _profile.RenewalComboDelays      = _steps.Select(s => Math.Max(50, s.RenDelay)).ToList();
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
