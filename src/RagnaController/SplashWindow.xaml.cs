using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.IO;

namespace RagnaController
{
    public partial class SplashWindow : Window
    {
        private static readonly string[] StatusMessages = { "Initializing…", "Loading profiles…", "Starting engine…", "Connecting controller…", "Configuring macros…", "Almost ready…", "Ready." };
        private readonly DispatcherTimer _statusTimer = new();
        private int _statusPhase = 0;

        public SplashWindow()
        {
            InitializeComponent();
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            SplashVersionLabel.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.2.0";
            PrepareVoice();
            ContentRendered += (s, e) => StartAnimations();
        }

        private void PrepareVoice()
        {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string mp3Path = Path.Combine(baseDir, "startup_voice.mp3");
                string wavPath = Path.Combine(baseDir, "startup_voice.wav");
                
                if (File.Exists(mp3Path)) VoicePlayer.Source = new Uri(mp3Path, UriKind.Absolute);
                else if (File.Exists(wavPath)) VoicePlayer.Source = new Uri(wavPath, UriKind.Absolute);
            } catch { }
        }

        public void PlayVoice()
        {
            try {
                if (VoicePlayer.Source != null) VoicePlayer.Play();
                else {
                    System.Threading.Tasks.Task.Run(() => {
                        try {
                            using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
                            var voice = synth.GetInstalledVoices().FirstOrDefault(v => v.VoiceInfo.Gender == System.Speech.Synthesis.VoiceGender.Female);
                            if (voice != null) synth.SelectVoice(voice.VoiceInfo.Name);
                            synth.Speak("Ragna Controller");
                        } catch { }
                    });
                }
            } catch { }
        }

        private async void StartAnimations()
        {
            Play("FadeIn");
            await System.Threading.Tasks.Task.Delay(700);
            Play("GoldPulse");
            Play("LogoIn");
            Play("ProgressAnim");
            
            DoubleAnimation fadeInBottom = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800));
            BottomRow.BeginAnimation(OpacityProperty, fadeInBottom);

            _statusTimer.Interval = TimeSpan.FromMilliseconds(700);
            _statusTimer.Tick += (s, e) => {
                _statusPhase++;
                if (_statusPhase < StatusMessages.Length) StatusText.Text = StatusMessages[_statusPhase];
                else _statusTimer.Stop();
            };
            _statusTimer.Start();
        }

        public void FadeAndClose(int durationMs = 600)
        {
            _statusTimer.Stop();
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(durationMs))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (s, e) => Close();
            Root.BeginAnimation(OpacityProperty, fade);
        }

        private void Play(string key) { if (Resources.Contains(key) && Resources[key] is Storyboard sb) sb.Begin(this); }
    }
}