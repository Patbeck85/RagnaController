using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RagnaController
{
    public partial class SplashWindow : Window
    {
        private static readonly string[] StatusMessages =
        {
            "Initializing…",
            "Loading profiles…",
            "Starting engine…",
            "Connecting controller…",
            "Configuring macros…",
            "Almost ready…",
            "Ready.",
        };

        private readonly DispatcherTimer _statusTimer = new();
        private int _statusPhase = 0;

        public SplashWindow()
        {
            InitializeComponent();
            var _splashVer = Assembly.GetExecutingAssembly().GetName().Version;
            SplashVersionLabel.Text = _splashVer != null ? $"v{_splashVer.Major}.{_splashVer.Minor}.{_splashVer.Build}" : "v?";
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ── Animations starten ───────────────────────────────────────────
            Play("FadeIn");
            await Delay(300);

            Play("BeamIn");
            await Delay(200);

            Play("GoldPulse");
            Play("CoreGlowPulse");
            await Delay(200);

            Play("WingsIn");
            await Delay(150);

            Play("LogoIn");
            await Delay(250);

            Play("BlueFlash");
            await Delay(300);

            Play("LogoFloat");
            Play("BeamPulse");
            Play("BluePulse");
            Play("LogoGlowPulse");
            Play("WingsPulse");
            await Delay(200);

            Play("ParticlesIn");
            await Delay(200);

            // Progress läuft jetzt über 4.2s (etwas länger als vorher, passend zu 5s)
            Play("ProgressAnim");
            Play("BottomIn");

            // Status-Texte alle 700ms wechseln (7 Texte × 700ms ≈ 4.9s)
            _statusTimer.Interval = TimeSpan.FromMilliseconds(700);
            _statusTimer.Tick += (_, _) =>
            {
                _statusPhase++;
                if (_statusPhase < StatusMessages.Length)
                    StatusText.Text = StatusMessages[_statusPhase];
                else
                    _statusTimer.Stop();
            };
            _statusTimer.Start();
        }

        /// <summary>
        /// Blendet den Splash aus und schließt ihn.
        /// durationMs: Dauer der Ausblend-Animation in ms (Standard 600).
        /// </summary>
        public void FadeAndClose(int durationMs = 600)
        {
            _statusTimer.Stop();
            StatusText.Text = "Ready.";

            var fade = new DoubleAnimation(1, 0,
                new Duration(TimeSpan.FromMilliseconds(durationMs)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => Close();
            Root.BeginAnimation(OpacityProperty, fade);
        }

        private void Play(string key)
            => ((Storyboard)Resources[key]).Begin(this);

        private static System.Threading.Tasks.Task Delay(int ms)
            => System.Threading.Tasks.Task.Delay(ms);
    }
}
