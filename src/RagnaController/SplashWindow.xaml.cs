using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
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
            PrepareVoice(); // preload — App.xaml.cs ruft PlayVoice() nach garantiertem Render
            ContentRendered += (_, _) => OnLoaded(this, new System.Windows.RoutedEventArgs()); // fires after first frame is painted on screen
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ── Startup voice ─────────────────────────────────────────────────
            // ── Animations starten ───────────────────────────────────────────
            Play("FadeIn");
            await Delay(700); // FadeIn dauert 700ms — Splash jetzt voll sichtbar
            // Voice wird von App.xaml.cs getriggert — nach garantiertem Render

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
        /// <summary>
        /// Plays startup_voice.mp3 / .wav next to the .exe (or in app folder).
        /// Generate the file once via ElevenLabs / Voicemaker / Azure TTS:
        ///   Text : "RagnaController"
        ///   Voice: anime female Japanese (e.g. ElevenLabs "Yuna", Voicemaker "Ai-Keiko")
        ///   Format: MP3 or WAV, save as "startup_voice.mp3" next to RagnaController.exe
        ///
        /// Falls back to Windows TTS if no file found.
        /// </summary>
        public void PrepareVoice()
        {
            // Set the source on the XAML MediaElement — WPF handles buffering automatically.
            // LoadedBehavior=Manual means it won't play until we call Play().
            try
            {
                string exeDir  = AppDomain.CurrentDomain.BaseDirectory;
                string mp3Path = System.IO.Path.Combine(exeDir, "startup_voice.mp3");
                string wavPath = System.IO.Path.Combine(exeDir, "startup_voice.wav");
                string? audioPath = System.IO.File.Exists(mp3Path) ? mp3Path
                                  : System.IO.File.Exists(wavPath) ? wavPath
                                  : null;
                if (audioPath == null) return;

                VoicePlayer.Source = new Uri(audioPath, UriKind.Absolute);
                // WPF MediaElement with LoadedBehavior=Manual buffers automatically.
                // Call PlayVoice() when you want playback to start.
            }
            catch { /* never crash splash for audio */ }
        }

        public void PlayVoice()
        {
            // MediaElement.Play() — WPF native, no threading issues, no GC problems.
            try
            {
                VoicePlayer.Play();
                return;
            }
            catch { }

            // Fallback TTS if no file was prepared
            try
            {

                // Fallback: Windows TTS (sounds robotic but always works)
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        using var synth = new System.Speech.Synthesis.SpeechSynthesizer();

                        // Try to find a female voice — prefer Japanese/Asian if available
                        var voices = synth.GetInstalledVoices();
                        var femaleJp = voices.FirstOrDefault(v =>
                            v.VoiceInfo.Gender == System.Speech.Synthesis.VoiceGender.Female &&
                            (v.VoiceInfo.Culture.Name.StartsWith("ja") ||
                             v.VoiceInfo.Culture.Name.StartsWith("zh")));

                        var femaleAny = voices.FirstOrDefault(v =>
                            v.VoiceInfo.Gender == System.Speech.Synthesis.VoiceGender.Female);

                        if (femaleJp != null)       synth.SelectVoice(femaleJp.VoiceInfo.Name);
                        else if (femaleAny != null) synth.SelectVoice(femaleAny.VoiceInfo.Name);

                        synth.Rate   = -1;  // slightly slower = more dramatic
                        synth.Volume = 85;
                        synth.Speak("Ragna Controller");
                    }
                    catch { /* TTS not available — silent start */ }
                });
            }
            catch { /* Never crash the splash for audio */ }
        }


    }
}
