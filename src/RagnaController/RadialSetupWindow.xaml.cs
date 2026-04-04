using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RagnaController.Core;
using RagnaController.Profiles;

namespace RagnaController
{
    public class RoEmote
    {
        public string Command       { get; set; } = "";
        public string Name          { get; set; } = "";
        public string ImagePath     { get; set; } = "";
        public string FallbackEmoji { get; set; } = "❓";
        public string WikiUrl       { get; set; } = "";   // iROWiki GIF-URL

        public bool       HasImage        => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);
        public Visibility ImageVisibility => HasImage ? Visibility.Visible   : Visibility.Collapsed;
        public Visibility EmojiVisibility => HasImage ? Visibility.Collapsed : Visibility.Visible;
    }

    public partial class RadialSetupWindow : Window
    {
        private readonly List<RadialItem> _items;
        private readonly List<TextBox>    _nameBoxes = new();
        private readonly List<TextBox>    _cmdBoxes  = new();
        private readonly List<ComboBox>   _keyCombos = new();
        private readonly string[]         _selectedImages = new string[8]; // merkt Bildpfad pro Slot
        private int           _currentRowEditing = -1;
        private List<RoEmote> _roEmotes          = new();

        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
        }) { Timeout = TimeSpan.FromSeconds(15) };

        static RadialSetupWindow()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("RagnaController/1.2.0");
        }

        private static readonly List<VirtualKey> AllKeys =
            Enum.GetValues(typeof(VirtualKey)).Cast<VirtualKey>()
                .Where(k => k != VirtualKey.None).OrderBy(k => k.ToString()).ToList();

        private static readonly string[] Directions =
            { "↑ UP", "↗ UP-R", "→ RIGHT", "↘ DN-R", "↓ DOWN", "↙ DN-L", "← LEFT", "↖ UP-L" };

        // Full emote list with iROWiki GIF URLs (source: irowiki.org/cl/images/)
        private static readonly (string cmd, string name, string emoji, string wikiGif)[] EmoteTable =
        {
            ("!",     "SURPRISE",   "😱", "https://irowiki.org/cl/images/7/70/Exc.gif"),
            ("?",     "QUESTION",   "🤔", "https://irowiki.org/cl/images/8/82/Que.gif"),
            ("lv",    "LOVE",       "❤️", "https://irowiki.org/cl/images/b/b6/Lov.gif"),
            ("lv2",   "BIG LOVE",   "💕", "https://irowiki.org/cl/images/c/ce/Lov2.gif"),
            ("swt",   "SWEAT",      "💦", "https://irowiki.org/cl/images/1/17/Swt.gif"),
            ("swt2",  "SWEAT2",     "😰", "https://irowiki.org/cl/images/9/98/Swt2.gif"),
            ("ic",    "IDEA",       "💡", "https://irowiki.org/cl/images/b/be/Lit.gif"),
            ("an",    "ANGER",      "😤", "https://irowiki.org/cl/images/d/df/Ang.gif"),
            ("ag",    "FRUSTRATE",  "🌩️", "https://irowiki.org/cl/images/8/8b/Agh.gif"),
            ("$",     "MONEY",      "💰", "https://irowiki.org/cl/images/6/66/Money.gif"),
            ("...",   "SPEECHLESS", "💬", "https://irowiki.org/cl/images/0/05/....gif"),
            ("ho",    "WHISTLE",    "🎵", "https://irowiki.org/cl/images/8/86/Hoe.gif"),
            ("thx",   "THANKS",     "👍", "https://irowiki.org/cl/images/e/e9/Thx.gif"),
            ("wah",   "PANIC",      "😨", "https://irowiki.org/cl/images/5/5c/Wah.gif"),
            ("sry",   "SORRY",      "🙏", "https://irowiki.org/cl/images/0/0c/Sry.gif"),
            ("heh",   "HAHA",       "😄", "https://irowiki.org/cl/images/5/57/Heh.gif"),
            ("hmm",   "THINKING",   "🤔", "https://irowiki.org/cl/images/4/47/Hmm.gif"),
            ("no1",   "THUMBS UP",  "👌", "https://irowiki.org/cl/images/1/16/No1.gif"),
            ("ok",    "OKAY",       "✅", "https://irowiki.org/cl/images/b/ba/Ok.gif"),
            ("omg",   "SHOCK",      "😱", "https://irowiki.org/cl/images/8/80/Omg.gif"),
            ("oh",    "OH!",        "⭕", "https://irowiki.org/cl/images/c/c7/Ohh.gif"),
            ("x",     "NO",         "❌", "https://irowiki.org/cl/images/8/86/Ecks.gif"),
            ("hlp",   "HELP",       "❓", "https://irowiki.org/cl/images/2/21/Hlp.gif"),
            ("go",    "GO!",        "🏃", "https://irowiki.org/cl/images/b/bb/Goo.gif"),
            ("sob",   "CRY",        "😢", "https://irowiki.org/cl/images/1/1d/Sob.gif"),
            ("gg",    "GG",         "😈", "https://irowiki.org/cl/images/e/e4/Ggg.gif"),
            ("kis",   "KISS",       "💋", "https://irowiki.org/cl/images/6/68/Kis.gif"),
            ("kis2",  "KISS2",      "💋", "https://irowiki.org/cl/images/5/51/Kis2.gif"),
            ("pif",   "PIF",        "😏", "https://irowiki.org/cl/images/e/e0/Pif.gif"),
            ("??",    "SHAKING",    "😕", "https://irowiki.org/cl/images/f/f8/Ono.gif"),
            ("bzz",   "ANGRY",      "😡", "https://irowiki.org/cl/images/0/00/Bzz.gif"),
            ("rice",  "DROOL",      "🤤", "https://irowiki.org/cl/images/5/5e/Rice.gif"),
            ("awsm",  "AWESOME",    "😍", "https://irowiki.org/cl/images/2/26/Awsm.gif"),
            ("meh",   "MEH",        "😛", "https://irowiki.org/cl/images/b/bb/Meh.gif"),
            ("shy",   "SHY",        "😊", "https://irowiki.org/cl/images/9/97/Shy.gif"),
            ("pat",   "PAT",        "🤝", "https://irowiki.org/cl/images/e/e3/Pat.gif"),
            ("mp",    "LOW SP",     "🔵", "https://irowiki.org/cl/images/1/1c/Mep.gif"),
            ("slur",  "LUSTFUL",    "😋", "https://irowiki.org/cl/images/4/4c/Slur.gif"),
            ("com",   "COME HERE",  "☝️", "https://irowiki.org/cl/images/e/e6/Come.gif"),
            ("yawn",  "YAWN",       "🥱", "https://irowiki.org/cl/images/8/83/Yawn.gif"),
            ("grat",  "CONGRATS",   "🎉", "https://irowiki.org/cl/images/f/fa/Grat.gif"),
            ("hp",    "LOW HP",     "❤️", "https://irowiki.org/cl/images/e/eb/Hep.gif"),
            ("fsh",   "SHINY EYES", "✨", "https://irowiki.org/cl/images/e/e2/Fsh.gif"),
            ("spin",  "DIZZY",      "😵", "https://irowiki.org/cl/images/7/71/Spin.gif"),
            ("sigh",  "SIGH",       "😮‍💨", "https://irowiki.org/cl/images/7/7a/Sigh.gif"),
            ("dum",   "DUMB",       "😐", "https://irowiki.org/cl/images/c/ce/Dum.gif"),
            ("crwd",  "CLAMOR",     "📢", "https://irowiki.org/cl/images/3/3b/Crwd.gif"),
            ("desp",  "OTL",        "🙇", "https://irowiki.org/cl/images/7/71/Desp.gif"),
            ("dice",  "DICE",       "🎲", "https://irowiki.org/cl/images/3/3e/Dice.gif"),
            ("e20",   "POINT UP",   "☝️", "https://irowiki.org/cl/images/7/7f/E20.gif"),
            ("hum",   "ANNOYED",    "😒", "https://irowiki.org/cl/images/7/75/Hum.gif"),
            ("abs",   "SOUL OUT",   "👻", "https://irowiki.org/cl/images/2/21/Abs.gif"),
            ("oops",  "OOPS",       "😳", "https://irowiki.org/cl/images/f/fe/Oops.gif"),
            ("spit",  "SPIT",       "🤢", "https://irowiki.org/cl/images/7/78/Spit.gif"),
            ("ene",   "BLESSED",    "😇", "https://irowiki.org/cl/images/2/20/Ene.gif"),
            ("panic", "PANIC",      "😱", "https://irowiki.org/cl/images/8/8b/Panic.gif"),
            ("whisp", "WHISPER",    "🤫", "https://irowiki.org/cl/images/c/cd/Whisp.gif"),
            ("bawi",  "ROCK",       "✊", "https://irowiki.org/cl/images/d/d1/Emote_bawi.png"),
            ("bo",    "PAPER",      "🖐️", "https://irowiki.org/cl/images/6/69/Emote_bo.png"),
            ("gawi",  "SCISSORS",   "✌️", "https://irowiki.org/cl/images/a/a3/Emote_gawi.png"),
        };

        public RadialSetupWindow(List<RadialItem> currentItems)
        {
            InitializeComponent();
            _items = currentItems;
            // Pre-load existing image paths from saved items
            for (int i = 0; i < 8 && i < currentItems.Count; i++)
                _selectedImages[i] = currentItems[i].ImagePath ?? "";
            LoadEmoteDatabase();
            EmoteList.ItemsSource = _roEmotes;
            BuildUI();
        }

        // ─── Emote-Datenbank ────────────────────────────────────────────────

        private void LoadEmoteDatabase()
        {
            string baseDir     = AppDomain.CurrentDomain.BaseDirectory;
            string emoteFolder = Path.Combine(baseDir, "Assets", "Emotes");
            Directory.CreateDirectory(emoteFolder);

            _roEmotes = new List<RoEmote>();

            foreach (var (cmd, name, emoji, wikiGif) in EmoteTable)
            {
                // Search for local file (by command name or emotion_XX index)
                string? found = FindEmoteLocal(emoteFolder, cmd, wikiGif);
                _roEmotes.Add(new RoEmote
                {
                    Command       = $"/{cmd}",
                    Name          = name,
                    ImagePath     = found ?? "",
                    FallbackEmoji = emoji,
                    WikiUrl       = wikiGif
                });
            }

            UpdateEmoteStatus();
        }

        private static string? FindEmoteLocal(string folder, string cmd, string wikiUrl)
        {
            // 1) cmd-Name: lv.gif / lv.png / lv.bmp
            string safeName = MakeSafeFilename(cmd);
            foreach (string ext in new[] { ".gif", ".png", ".bmp" })
            {
                string p = Path.Combine(folder, safeName + ext);
                if (File.Exists(p)) return p;
            }
            // 2) Wiki-Dateiname aus URL ableiten (z.B. Lov.gif)
            if (!string.IsNullOrEmpty(wikiUrl))
            {
                string wikiFile = Path.GetFileName(wikiUrl);
                string p = Path.Combine(folder, wikiFile);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string MakeSafeFilename(string cmd)
            => cmd.Replace("/", "").Replace("$", "dollar").Replace(".", "dot").Replace("?", "q");

        private void UpdateEmoteStatus()
        {
            if (EmoteStatusText == null) return;
            int loaded = _roEmotes.Count(e => e.HasImage);
            EmoteStatusText.Text = loaded > 0
                ? $"✓ {loaded} / {_roEmotes.Count} images loaded"
                : "Emoji fallback active  –  ⬇ Load from iROWiki  or  📁 Scan RO client";
        }

        // ─── iROWiki Download ────────────────────────────────────────────────

        private async void BtnDownloadWiki_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;
            EmoteStatusText.Text = "⏳ Lade von iROWiki...";

            string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Emotes");
            Directory.CreateDirectory(dest);

            int ok = 0, fail = 0;
            foreach (var emote in _roEmotes)
            {
                if (emote.HasImage) { ok++; continue; }
                if (string.IsNullOrEmpty(emote.WikiUrl)) continue;

                string wikiFile = Path.GetFileName(emote.WikiUrl);
                string target   = Path.Combine(dest, wikiFile);
                try
                {
                    byte[] data = await _http.GetByteArrayAsync(emote.WikiUrl);
                    await File.WriteAllBytesAsync(target, data);
                    // Also save under command name so FindEmoteLocal can locate it
                    string cmdTarget = Path.Combine(dest, MakeSafeFilename(
                        emote.Command.TrimStart('/')) + Path.GetExtension(wikiFile));
                    if (!File.Exists(cmdTarget)) File.Copy(target, cmdTarget, overwrite: false);
                    ok++;
                }
                catch { fail++; }

                EmoteStatusText.Text = $"⏳ {ok}/{_roEmotes.Count}...";
                await Task.Delay(80); // Brief delay — polite to the server
            }

            LoadEmoteDatabase();
            EmoteList.ItemsSource = null;
            EmoteList.ItemsSource = _roEmotes;

            if (sender is Button b) b.IsEnabled = true;
            EmoteStatusText.Text = fail == 0
                ? $"✓ {ok} images loaded"
                : $"✓ {ok} loaded, {fail} failed";
        }

        // ─── RO-Client Scan ─────────────────────────────────────────────────

        private void BtnScanRoPath_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description            = "Select RO client emotion folder (e.g. data/texture/emotion)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton    = false
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string src  = dlg.SelectedPath;
            string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Emotes");
            Directory.CreateDirectory(dest);

            int copied = 0;
            var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".bmp", ".png", ".gif" };

            foreach (string file in Directory.GetFiles(src))
            {
                if (!validExts.Contains(Path.GetExtension(file))) continue;
                string target = Path.Combine(dest, Path.GetFileName(file));
                try { File.Copy(file, target, overwrite: true); copied++; } catch { }
            }

            MessageBox.Show(this,
                $"{copied} Dateien nach Assets/Emotes/ kopiert.",
                "Scan abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadEmoteDatabase();
            EmoteList.ItemsSource = null;
            EmoteList.ItemsSource = _roEmotes;
        }

        // ─── UI Aufbau ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            ItemsStack.Children.Clear();
            _nameBoxes.Clear();
            _cmdBoxes.Clear();
            _keyCombos.Clear();

            for (int i = 0; i < 8; i++)
            {
                var item = i < _items.Count
                    ? _items[i]
                    : new RadialItem { Name = "", Command = "", IsEmote = true };

                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                var numPanel = new StackPanel
                    { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
                numPanel.Children.Add(new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(229, 184, 66)),
                    FontSize = 14, FontWeight = FontWeights.Black
                });
                numPanel.Children.Add(new TextBlock
                {
                    Text = Directions[i],
                    Foreground = new SolidColorBrush(Color.FromRgb(125, 139, 158)),
                    FontSize = 9, FontWeight = FontWeights.Bold
                });
                Grid.SetColumn(numPanel, 0);
                row.Children.Add(numPanel);

                var nameBox = new TextBox
                {
                    Text = item.Name, Height = 36, Margin = new Thickness(4, 0, 4, 0),
                    Background = new SolidColorBrush(Color.FromRgb(18, 22, 32)),
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 244, 248)),
                    BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 0, 10, 0), FontWeight = FontWeights.SemiBold
                };
                var bName = WrapBorder(nameBox);
                Grid.SetColumn(bName, 1);
                row.Children.Add(bName);
                _nameBoxes.Add(nameBox);

                var cmdBox = new TextBox
                {
                    Text = item.Command, Height = 36, Margin = new Thickness(4, 0, 4, 0),
                    Background = new SolidColorBrush(Color.FromRgb(18, 22, 32)),
                    Foreground = new SolidColorBrush(Color.FromRgb(229, 184, 66)),
                    BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 0, 10, 0),
                    FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.Bold
                };
                var bCmd = WrapBorder(cmdBox);
                Grid.SetColumn(bCmd, 2);
                row.Children.Add(bCmd);
                _cmdBoxes.Add(cmdBox);

                var keyCombo = new ComboBox
                {
                    Height = 36, Margin = new Thickness(4, 0, 4, 0),
                    Style = (Style)Application.Current.Resources["DarkComboBox"]
                };
                keyCombo.Items.Add(new ComboBoxItem { Content = "Chat (Emote)", Tag = VirtualKey.None });
                foreach (var k in AllKeys)
                    keyCombo.Items.Add(new ComboBoxItem { Content = k.ToString(), Tag = k });
                var existingKey = keyCombo.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(ci => (VirtualKey)ci.Tag == item.Key);
                keyCombo.SelectedItem = existingKey ?? keyCombo.Items[0];
                Grid.SetColumn(keyCombo, 3);
                row.Children.Add(keyCombo);
                _keyCombos.Add(keyCombo);

                var btnGallery = new Button
                {
                    Content = "🖼 Gallery", Height = 36, Margin = new Thickness(4, 0, 0, 0),
                    Style = (Style)Application.Current.Resources["ConsoleGhostBtn"], Tag = i
                };
                btnGallery.Click += BtnOpenGallery_Click;
                Grid.SetColumn(btnGallery, 4);
                row.Children.Add(btnGallery);

                ItemsStack.Children.Add(row);
            }
        }

        private static Border WrapBorder(UIElement child) => new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(18, 22, 32)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(42, 50, 69)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(6),
            Child           = child
        };

        // ─── Gallery Events ─────────────────────────────────────────────────

        private void BtnOpenGallery_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int rowIndex)
            {
                _currentRowEditing = rowIndex;
                EmoteGalleryOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseGallery_Click(object sender, RoutedEventArgs e)
            => EmoteGalleryOverlay.Visibility = Visibility.Collapsed;

        private void BtnSelectEmote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoEmote emote && _currentRowEditing >= 0)
            {
                _nameBoxes[_currentRowEditing].Text = emote.Name;
                _cmdBoxes[_currentRowEditing].Text  = emote.Command;
                _keyCombos[_currentRowEditing].SelectedIndex = 0;
                _selectedImages[_currentRowEditing] = emote.ImagePath; // Store image path for this slot
                EmoteGalleryOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Save / Close ────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            for (int i = 0; i < 8; i++)
            {
                if (string.IsNullOrWhiteSpace(_nameBoxes[i].Text)) continue;
                var selectedKey = _keyCombos[i].SelectedItem is ComboBoxItem ci
                    ? (VirtualKey)ci.Tag : VirtualKey.None;
                bool isEmote = (selectedKey == VirtualKey.None);
                var newItem = new RadialItem
                {
                    Name      = _nameBoxes[i].Text.ToUpper(),
                    IsEmote   = isEmote,
                    Key       = selectedKey,
                    ImagePath = _selectedImages[i] ?? ""  // Bild mitsspeichern
                };
                if (isEmote)
                {
                    string cmd = _cmdBoxes[i].Text.Trim();
                    if (!cmd.StartsWith("/") && !string.IsNullOrEmpty(cmd)) cmd = "/" + cmd;
                    newItem.Command = cmd;
                }
                _items.Add(newItem);
            }
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
