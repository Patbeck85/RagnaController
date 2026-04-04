using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace RagnaController.Core
{
    public enum MacroStepType { KeyPress, LeftClick, RightClick, Delay }

    public class MacroStep 
    { 
        public int Index { get; set; } 
        public MacroStepType Type { get; set; } 
        public VirtualKey Key { get; set; } = VirtualKey.None; 
        public int DelayMs { get; set; } 

        public override string ToString() => Type switch {
            MacroStepType.KeyPress => $"Key: {Key} ({DelayMs}ms)",
            MacroStepType.LeftClick => $"L-Click ({DelayMs}ms)",
            MacroStepType.RightClick => $"R-Click ({DelayMs}ms)",
            _ => $"Wait ({DelayMs}ms)"
        };
    }

    public class Macro 
    { 
        public string Name { get; set; } = "Untitled"; 
        public List<MacroStep> Steps { get; set; } = new(); 
        public int TotalDurationMs { get; set; } 
        public int LoopCount { get; set; } = 1; 
    }

    public class MacroRecorder
    {
        public bool IsRecording { get; private set; }
        public bool IsPlaying { get; private set; }
        private readonly List<MacroStep> _buf = new();
        private readonly Stopwatch _sw = new();
        private MacroStep[]? _steps;
        private int _idx, _timer, _loop;

        public event Action? RecordingStarted, RecordingStopped, PlaybackCompleted;
        public event Action<MacroStep>? StepRecorded;

        public void Start() { IsRecording = true; _buf.Clear(); _sw.Restart(); RecordingStarted?.Invoke(); }
        public void Cancel() { IsRecording = false; _sw.Stop(); _buf.Clear(); }
        public Macro Stop(string n) { IsRecording = false; _sw.Stop(); var m = new Macro { Name = n, Steps = new List<MacroStep>(_buf), TotalDurationMs = _buf.Sum(s => s.DelayMs) }; RecordingStopped?.Invoke(); return m; }
        public void RecordKey(VirtualKey k) { if (!IsRecording) return; int ms = (int)_sw.ElapsedMilliseconds; _sw.Restart(); var s = new MacroStep { Index = _buf.Count + 1, Type = MacroStepType.KeyPress, Key = k, DelayMs = Math.Max(ms, 50) }; _buf.Add(s); StepRecorded?.Invoke(s); }
        public void RecordClick(ActionType type) { if (!IsRecording) return; int ms = (int)_sw.ElapsedMilliseconds; _sw.Restart(); var s = new MacroStep { Index = _buf.Count + 1, Type = (type == ActionType.LeftClick ? MacroStepType.LeftClick : MacroStepType.RightClick), DelayMs = Math.Max(ms, 50) }; _buf.Add(s); StepRecorded?.Invoke(s); }
        public void Play(Macro m, int l = 1) { if (IsPlaying || m == null || m.Steps.Count == 0) return; _steps = m.Steps.ToArray(); _idx = 0; _timer = 0; _loop = l; IsPlaying = true; }
        public void StopPlayback() { IsPlaying = false; _steps = null; }
        public void UpdatePlayback(int ms) {
            if (!IsPlaying || _steps == null) return;
            _timer += ms;
            if (_idx >= _steps.Length) { if (_loop == 0 || --_loop > 0) { _idx = 0; _timer = 0; } else { IsPlaying = false; _steps = null; PlaybackCompleted?.Invoke(); } return; }
            if (_timer >= _steps[_idx].DelayMs) { var s = _steps[_idx]; if (s.Type == MacroStepType.KeyPress) InputSimulator.TapKey(s.Key); else if (s.Type == MacroStepType.LeftClick) InputSimulator.LeftClick(); else if (s.Type == MacroStepType.RightClick) InputSimulator.RightClick(); _timer -= s.DelayMs; _idx++; }
        }
        public static string SaveMacro(Macro m) { string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RagnaController", "Macros"); Directory.CreateDirectory(dir); string p = Path.Combine(dir, m.Name + ".json"); File.WriteAllText(p, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true })); return p; }
        public static Macro? LoadMacro(string p) { try { return File.Exists(p) ? JsonSerializer.Deserialize<Macro>(File.ReadAllText(p)) : null; } catch { return null; } }
    }
}