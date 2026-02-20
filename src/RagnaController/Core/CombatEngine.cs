using System;
using System.Collections.Generic;
using RagnaController.Profiles;

namespace RagnaController.Core
{
    /// <summary>
    /// Maps gamepad button presses to Ragnarok Online keyboard/mouse actions.
    /// Supports: skill layers (normal / L2-held / R2-held), advanced turbo modes, macro playback.
    /// </summary>
    public class CombatEngine
    {
        // Turbo state per action
        private readonly Dictionary<string, TurboState> _turboStates = new();

        // Macro instances
        private readonly Dictionary<string, MacroRecorder> _macroPlayers = new();
        private readonly Dictionary<string, Macro?> _loadedMacros = new();

        // Currently loaded mapping
        private Profile? _profile;

        // Layer tracking
        private bool _layerL2Active;
        private bool _layerR2Active;

        public void LoadProfile(Profile profile)
        {
            _profile = profile;
            _turboStates.Clear();
            foreach (var kvp in profile.ButtonMappings)
                _turboStates[kvp.Key] = new TurboState();
        }

        // ── Called every tick ────────────────────────────────────────────────────

        public void UpdateLayers(bool l2Held, bool r2Held)
        {
            _layerL2Active = l2Held;
            _layerR2Active = r2Held;
        }

        /// <summary>
        /// Process a button event. Call with the current button name (e.g. "A", "B", "X", "Y",
        /// "LB", "RB", "DPadUp", etc.) and whether it is currently pressed.
        /// </summary>
        public void ProcessButton(string button, bool pressed, int tickMs)
        {
            if (_profile == null) return;

            string layeredButton = GetLayeredButton(button);
            if (!_profile.ButtonMappings.TryGetValue(layeredButton, out var action)) return;

            if (!_turboStates.ContainsKey(layeredButton))
                _turboStates[layeredButton] = new TurboState();

            var state = _turboStates[layeredButton];

            // Check if this is a macro action
            if (action.IsMacro && !string.IsNullOrEmpty(action.MacroFilePath))
            {
                ProcessMacroButton(layeredButton, action, pressed);
                return;
            }

            // Regular action processing
            if (action.TurboEnabled)
            {
                ProcessTurboMode(action, state, pressed, tickMs);
            }
            else
            {
                // Non-turbo: fire on press, not on release
                if (pressed && !state.WasPressed)
                    FireAction(action);
                state.WasPressed = pressed;
            }
        }

        // ── Macro Playback ────────────────────────────────────────────────────────

        private void ProcessMacroButton(string buttonKey, ButtonAction action, bool pressed)
        {
            if (pressed && !_turboStates[buttonKey].WasPressed)
            {
                // Button pressed - start macro
                if (!_macroPlayers.ContainsKey(buttonKey))
                    _macroPlayers[buttonKey] = new MacroRecorder();

                var macro = LoadMacro(action.MacroFilePath!);
                if (macro != null)
                {
                    _macroPlayers[buttonKey].PlayMacro(macro);
                }
            }

            _turboStates[buttonKey].WasPressed = pressed;
        }

        private Macro? LoadMacro(string filePath)
        {
            if (_loadedMacros.ContainsKey(filePath))
                return _loadedMacros[filePath];

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    string json = System.IO.File.ReadAllText(filePath);
                    var macro = System.Text.Json.JsonSerializer.Deserialize<Macro>(json);
                    _loadedMacros[filePath] = macro;
                    return macro;
                }
            }
            catch { }

            _loadedMacros[filePath] = null;
            return null;
        }

        public void UpdateMacroPlayback(int tickMs)
        {
            foreach (var player in _macroPlayers.Values)
            {
                if (player.IsPlaying)
                    player.UpdatePlayback(tickMs);
            }
        }

        // ── Turbo Mode Processing ─────────────────────────────────────────────────

        private void ProcessTurboMode(ButtonAction action, TurboState state, bool pressed, int tickMs)
        {
            if (pressed)
            {
                state.HoldMs += tickMs;

                switch (action.Mode)
                {
                    case TurboMode.Standard:
                        ProcessStandardTurbo(action, state);
                        break;

                    case TurboMode.Burst:
                        ProcessBurstTurbo(action, state, tickMs);
                        break;

                    case TurboMode.Rhythmic:
                        ProcessRhythmicTurbo(action, state);
                        break;

                    case TurboMode.Adaptive:
                        ProcessAdaptiveTurbo(action, state);
                        break;
                }
            }
            else
            {
                // Button released: reset state
                state.HoldMs = 0;
                state.BurstCounter = 0;
                state.InBurstPause = false;
                state.RhythmIndex = 0;
                state.AdaptiveStep = 0;
            }

            state.WasPressed = pressed;
        }

        // ── Standard: Constant interval ───────────────────────────────────────────
        private void ProcessStandardTurbo(ButtonAction action, TurboState state)
        {
            if (state.HoldMs >= action.TurboIntervalMs)
            {
                FireAction(action);
                state.HoldMs = 0;
            }
        }

        // ── Burst: N rapid presses, then pause ────────────────────────────────────
        private void ProcessBurstTurbo(ButtonAction action, TurboState state, int tickMs)
        {
            if (state.InBurstPause)
            {
                // In pause phase
                if (state.HoldMs >= action.BurstPauseMs)
                {
                    state.InBurstPause = false;
                    state.BurstCounter = 0;
                    state.HoldMs = 0;
                }
            }
            else
            {
                // In burst phase
                if (state.HoldMs >= action.TurboIntervalMs)
                {
                    FireAction(action);
                    state.HoldMs = 0;
                    state.BurstCounter++;

                    if (state.BurstCounter >= action.BurstCount)
                    {
                        // Enter pause
                        state.InBurstPause = true;
                        state.BurstCounter = 0;
                        state.HoldMs = 0;
                    }
                }
            }
        }

        // ── Rhythmic: Custom pattern (tap-tap-pause-tap) ──────────────────────────
        private void ProcessRhythmicTurbo(ButtonAction action, TurboState state)
        {
            if (action.RhythmPattern == null || action.RhythmPattern.Length == 0)
            {
                // Fallback to standard if no pattern defined
                ProcessStandardTurbo(action, state);
                return;
            }

            int currentInterval = action.RhythmPattern[state.RhythmIndex];

            if (state.HoldMs >= currentInterval)
            {
                FireAction(action);
                state.HoldMs = 0;
                state.RhythmIndex = (state.RhythmIndex + 1) % action.RhythmPattern.Length;
            }
        }

        // ── Adaptive: Accelerates the longer button is held ───────────────────────
        private void ProcessAdaptiveTurbo(ButtonAction action, TurboState state)
        {
            // Calculate current interval based on how long button has been held
            int totalHoldMs = state.HoldMs + (state.AdaptiveStep * action.AdaptiveMin);
            
            // Interpolate between AdaptiveMin (slow) and AdaptiveMax (fast)
            float progress = Math.Min(1.0f, state.AdaptiveStep / (float)action.AdaptiveSteps);
            int currentInterval = (int)(action.AdaptiveMin + progress * (action.AdaptiveMax - action.AdaptiveMin));

            if (state.HoldMs >= currentInterval)
            {
                FireAction(action);
                state.HoldMs = 0;
                
                if (state.AdaptiveStep < action.AdaptiveSteps)
                    state.AdaptiveStep++;
            }
        }

        // ── Fire Action ───────────────────────────────────────────────────────────

        private void FireAction(ButtonAction action)
        {
            switch (action.Type)
            {
                case ActionType.Key:
                    InputSimulator.TapKey(action.Key);
                    break;

                case ActionType.LeftClick:
                    InputSimulator.LeftClick();
                    break;

                case ActionType.RightClick:
                    InputSimulator.RightClick();
                    break;

                case ActionType.Scroll:
                    InputSimulator.ScrollWheel(action.ScrollDelta);
                    break;
            }
        }

        private string GetLayeredButton(string button)
        {
            if (_layerL2Active) return "L2+" + button;
            if (_layerR2Active) return "R2+" + button;
            return button;
        }

        // ── Inner types ──────────────────────────────────────────────────────────

        private class TurboState
        {
            public bool WasPressed;
            public int  HoldMs;           // Current turbo cooldown accumulator
            
            // Burst mode
            public int  BurstCounter;     // Number of bursts fired in current cycle
            public bool InBurstPause;     // Currently in pause phase
            
            // Rhythmic mode
            public int  RhythmIndex;      // Current position in rhythm pattern
            
            // Adaptive mode
            public int  AdaptiveStep;     // Current speed step (0 = slowest)
        }
    }

    // ── Data models for mappings ─────────────────────────────────────────────────

    public enum ActionType { Key, LeftClick, RightClick, Scroll }

    public class ButtonAction
    {
        public ActionType Type          { get; set; } = ActionType.Key;
        public VirtualKey Key           { get; set; } = VirtualKey.F1;
        public bool       TurboEnabled  { get; set; } = false;
        public int        TurboIntervalMs { get; set; } = 100;
        public int        ScrollDelta   { get; set; } = 120;
        public string     Label         { get; set; } = string.Empty;
        
        // Advanced turbo modes
        public TurboMode  Mode          { get; set; } = TurboMode.Standard;
        public int        BurstCount    { get; set; } = 5;      // Burst: N rapid presses
        public int        BurstPauseMs  { get; set; } = 500;    // Burst: Pause duration
        public int[]?     RhythmPattern { get; set; }           // Rhythmic: [100,100,200,100] etc
        public int        AdaptiveMin   { get; set; } = 200;    // Adaptive: Start interval
        public int        AdaptiveMax   { get; set; } = 50;     // Adaptive: End interval
        public int        AdaptiveSteps { get; set; } = 10;     // Adaptive: Steps to reach max speed
        
        // Macro playback (v1.4)
        public string?    MacroFilePath { get; set; }           // Path to macro JSON file
        public bool       IsMacro => !string.IsNullOrEmpty(MacroFilePath);
    }

    public enum TurboMode
    {
        Standard,   // Constant interval
        Burst,      // N rapid presses, then pause
        Rhythmic,   // Custom pattern: tap-tap-pause-tap
        Adaptive    // Accelerates the longer button is held
    }
}
