using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RagnaController.Core
{
    /// <summary>
    /// Records and plays back macro sequences (button presses + delays).
    /// User can record a combo like: A → wait 200ms → X → Y, then bind it to a button.
    /// </summary>
    public class MacroRecorder
    {
        // ── State ─────────────────────────────────────────────────────────────────
        public bool IsRecording  { get; private set; }
        public bool IsPlaying    { get; private set; }
        public int  RecordedSteps => _recordingBuffer.Count;

        // Current recording
        private readonly List<MacroStep> _recordingBuffer = new();
        private readonly Stopwatch _recordTimer = new();

        // Playback state
        private readonly List<MacroStep> _playbackMacro = new();
        private int _playbackIndex = 0;
        private int _playbackTimer = 0;

        // Events
        public event Action? RecordingStarted;
        public event Action? RecordingStopped;
        public event Action<MacroStep>? StepRecorded;
        public event Action? PlaybackCompleted;

        // ── Recording ─────────────────────────────────────────────────────────────

        public void StartRecording()
        {
            if (IsRecording) return;

            _recordingBuffer.Clear();
            _recordTimer.Restart();
            IsRecording = true;

            RecordingStarted?.Invoke();
        }

        public Macro StopRecording(string name = "Untitled Macro")
        {
            if (!IsRecording) return new Macro { Name = name };

            _recordTimer.Stop();
            IsRecording = false;

            // Build macro from buffer
            var macro = new Macro
            {
                Name = name,
                Steps = new List<MacroStep>(_recordingBuffer),
                TotalDurationMs = _recordingBuffer.Sum(s => s.DelayMs)
            };

            RecordingStopped?.Invoke();
            return macro;
        }

        public void RecordKeyPress(VirtualKey key)
        {
            if (!IsRecording) return;

            int elapsed = (int)_recordTimer.ElapsedMilliseconds;
            _recordTimer.Restart();

            var step = new MacroStep
            {
                Type = MacroStepType.KeyPress,
                Key = key,
                DelayMs = Math.Max(elapsed, 50) // Minimum 50ms between steps
            };

            _recordingBuffer.Add(step);
            StepRecorded?.Invoke(step);
        }

        public void RecordClick(ActionType clickType)
        {
            if (!IsRecording) return;

            int elapsed = (int)_recordTimer.ElapsedMilliseconds;
            _recordTimer.Restart();

            var step = new MacroStep
            {
                Type = clickType == ActionType.LeftClick ? MacroStepType.LeftClick : MacroStepType.RightClick,
                DelayMs = Math.Max(elapsed, 50)
            };

            _recordingBuffer.Add(step);
            StepRecorded?.Invoke(step);
        }

        public void CancelRecording()
        {
            IsRecording = false;
            _recordingBuffer.Clear();
            _recordTimer.Stop();
        }

        // ── Playback ──────────────────────────────────────────────────────────────

        public void PlayMacro(Macro macro)
        {
            if (IsPlaying) return;
            if (macro.Steps.Count == 0) return;

            _playbackMacro.Clear();
            _playbackMacro.AddRange(macro.Steps);
            _playbackIndex = 0;
            _playbackTimer = 0;
            IsPlaying = true;
        }

        public void StopPlayback()
        {
            IsPlaying = false;
            _playbackMacro.Clear();
            _playbackIndex = 0;
        }

        /// <summary>
        /// Call every engine tick (16ms) when playback is active.
        /// </summary>
        public void UpdatePlayback(int tickMs)
        {
            if (!IsPlaying) return;
            if (_playbackIndex >= _playbackMacro.Count)
            {
                // Playback complete
                IsPlaying = false;
                _playbackMacro.Clear();
                PlaybackCompleted?.Invoke();
                return;
            }

            var currentStep = _playbackMacro[_playbackIndex];
            _playbackTimer += tickMs;

            if (_playbackTimer >= currentStep.DelayMs)
            {
                ExecuteStep(currentStep);
                _playbackTimer = 0;
                _playbackIndex++;
            }
        }

        private void ExecuteStep(MacroStep step)
        {
            switch (step.Type)
            {
                case MacroStepType.KeyPress:
                    if (step.Key != VirtualKey.None)
                        InputSimulator.TapKey(step.Key);
                    break;

                case MacroStepType.LeftClick:
                    InputSimulator.LeftClick();
                    break;

                case MacroStepType.RightClick:
                    InputSimulator.RightClick();
                    break;

                case MacroStepType.Delay:
                    // Just wait (delay is handled by timer)
                    break;
            }
        }

        // ── Macro Editing ─────────────────────────────────────────────────────────

        public static Macro OptimizeMacro(Macro original)
        {
            // Remove steps with very short delays, merge consecutive delays
            var optimized = new List<MacroStep>();
            
            foreach (var step in original.Steps)
            {
                if (step.DelayMs < 30)
                    continue; // Skip very short delays (likely noise)
                
                optimized.Add(step);
            }

            return new Macro
            {
                Name = original.Name,
                Steps = optimized,
                TotalDurationMs = optimized.Sum(s => s.DelayMs)
            };
        }

        public static Macro SpeedUpMacro(Macro original, float multiplier)
        {
            // Speed up all delays by multiplier (e.g. 2.0 = twice as fast)
            var sped = original.Steps.Select(s => new MacroStep
            {
                Type = s.Type,
                Key = s.Key,
                DelayMs = Math.Max(30, (int)(s.DelayMs / multiplier))
            }).ToList();

            return new Macro
            {
                Name = original.Name + " (Fast)",
                Steps = sped,
                TotalDurationMs = sped.Sum(s => s.DelayMs)
            };
        }

        public static Macro SlowDownMacro(Macro original, float multiplier)
        {
            // Slow down all delays by multiplier (e.g. 2.0 = twice as slow)
            var slowed = original.Steps.Select(s => new MacroStep
            {
                Type = s.Type,
                Key = s.Key,
                DelayMs = (int)(s.DelayMs * multiplier)
            }).ToList();

            return new Macro
            {
                Name = original.Name + " (Slow)",
                Steps = slowed,
                TotalDurationMs = slowed.Sum(s => s.DelayMs)
            };
        }
    }

    // ── Data Models ───────────────────────────────────────────────────────────────

    public class Macro
    {
        public string Name { get; set; } = "Untitled";
        public List<MacroStep> Steps { get; set; } = new();
        public int TotalDurationMs { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class MacroStep
    {
        public MacroStepType Type { get; set; }
        public VirtualKey Key { get; set; } = VirtualKey.None;
        public int DelayMs { get; set; } // Wait before this step

        public override string ToString() => Type switch
        {
            MacroStepType.KeyPress => $"Press {Key} ({DelayMs}ms)",
            MacroStepType.LeftClick => $"Left Click ({DelayMs}ms)",
            MacroStepType.RightClick => $"Right Click ({DelayMs}ms)",
            MacroStepType.Delay => $"Wait {DelayMs}ms",
            _ => "Unknown"
        };
    }

    public enum MacroStepType
    {
        KeyPress,
        LeftClick,
        RightClick,
        Delay
    }
}
