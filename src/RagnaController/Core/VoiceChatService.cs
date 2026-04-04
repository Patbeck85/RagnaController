using System;
using System.Speech.Recognition;
using System.Threading.Tasks;

namespace RagnaController.Core
{
    /// <summary>
    /// Local speech recognition via Windows Speech API (no cloud, no API key).
    /// System.Speech is already referenced in the project.
    /// Workflow: StartListening() → user speaks → text is automatically
    /// typed into the RO chat via Enter → Text → Enter.
    /// </summary>
    public class VoiceChatService : IDisposable
    {
        private SpeechRecognitionEngine? _engine;
        private bool _isListening;
        private bool _disposed;

        public bool IsListening  => _isListening;
        public bool IsAvailable  => _engine != null;
        public float MinConfidence { get; set; } = 0.55f;

        public event Action<string>? TextRecognized;
        public event Action<string>? StatusChanged;

        public VoiceChatService()
        {
            try
            {
                _engine = new SpeechRecognitionEngine();
                _engine.SetInputToDefaultAudioDevice();
                // Diktat-Grammatik: erkennt freie Texteingabe (kein festes Vokabular)
                _engine.LoadGrammar(new DictationGrammar());
                _engine.SpeechRecognized        += OnRecognized;
                _engine.SpeechRecognitionRejected += (s, e) =>
                    StatusChanged?.Invoke("Nicht verstanden — bitte nochmals sprechen.");
                StatusChanged?.Invoke("Mikrofon bereit.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Speech recognition unavailable: {ex.Message}");
            }
        }

        public void StartListening()
        {
            if (_engine == null || _isListening) return;
            try
            {
                _isListening = true;
                _engine.RecognizeAsync(RecognizeMode.Single); // nur eine Aussage
                StatusChanged?.Invoke("🎤 Listening…");
            }
            catch { _isListening = false; }
        }

        public void StopListening()
        {
            if (_engine == null || !_isListening) return;
            try { _engine.RecognizeAsyncCancel(); } catch { }
            _isListening = false;
            StatusChanged?.Invoke("Mikrofon aus.");
        }

        private async void OnRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            _isListening = false;
            if (e.Result.Confidence < MinConfidence)
            {
                StatusChanged?.Invoke($"Zu unsicher ({e.Result.Confidence:P0}) – ignoriert.");
                return;
            }
            string text = e.Result.Text;
            StatusChanged?.Invoke($"Erkannt: \"{text}\"");
            TextRecognized?.Invoke(text);
            await InputSimulator.SendChatString(text);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopListening();
            _engine?.Dispose();
            _engine = null;
        }
    }
}
