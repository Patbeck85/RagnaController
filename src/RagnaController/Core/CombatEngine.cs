using System;
using System.Collections.Generic;
using RagnaController.Profiles;

namespace RagnaController.Core
{
    public class CombatEngine
    {
        private readonly Dictionary<string, TurboState> _turboStates = new();
        private readonly Dictionary<string, MacroRecorder> _macroPlayers = new();
        private readonly Dictionary<string, Macro?> _loadedMacros = new();

        private Profile? _profile;
        private bool _layerL1Active;

        /// <summary>Fired when an action is triggered (for vibration feedback).</summary>
        public event Action<ButtonAction>? ActionFired;

        /// <summary>
        /// Fired when a ground-spell action is triggered (IsGroundSpell=true).
        /// HybridEngine listens and tells MageEngine to enter ground-aim mode.
        /// </summary>
        public event Action? GroundSpellFired;
        private bool _layerR1Active;
        private bool _layerL2Active;
        private bool _layerR2Active;

        public void LoadProfile(Profile profile)
        {
            _profile = profile;
            _turboStates.Clear();
            _loadedMacros.Clear();
            _macroFileTimes.Clear();

            // Stop all running macro playback so an old macro can't bleed into the new profile
            foreach (var player in _macroPlayers.Values)
                player.StopPlayback();
            _macroPlayers.Clear();
        }

        public void UpdateLayers(bool l1Held, bool r1Held, bool l2Held, bool r2Held)
        {
            _layerL1Active = l1Held;
            _layerR1Active = r1Held;
            _layerL2Active = l2Held;
            _layerR2Active = r2Held;
        }

        public void ProcessButton(string button, bool pressed, int tickMs)
        {
            if (_profile == null) return;

            string layer = _layerL1Active ? "L1+" : _layerR1Active ? "R1+" : _layerL2Active ? "L2+" : _layerR2Active ? "R2+" : "";
            string layeredKey = layer + button;

            // When the active layer changes while a button is still held, the old layered-key
            // (e.g. "L1+A") never receives pressed=false, leaving WasPressed stuck at true.
            // Reset any stale TurboState for the same base button under a different layer prefix.
            foreach (var suffix in new[] { "", "L1+", "R1+", "L2+", "R2+" })
            {
                string staleKey = suffix + button;
                if (staleKey != layeredKey && _turboStates.TryGetValue(staleKey, out var stale))
                    stale.WasPressed = false;
            }

            if (!_profile.ButtonMappings.TryGetValue(layeredKey, out var action))
            {
                if (!(_layerL1Active || _layerR1Active || _layerL2Active || _layerR2Active) ||
                    !_profile.ButtonMappings.TryGetValue(button, out action))
                    return;
            }

            if (!_turboStates.ContainsKey(layeredKey)) _turboStates[layeredKey] = new TurboState();
            var state = _turboStates[layeredKey];

            if (action.IsMacro && !string.IsNullOrEmpty(action.MacroFilePath))
            {
                ProcessMacroButton(layeredKey, action, pressed);
                return;
            }

            if (action.TurboEnabled)
            {
                ProcessTurboMode(action, state, pressed, tickMs);
            }
            else
            {
                if (pressed && !state.WasPressed) FireAction(action);
                state.WasPressed = pressed;
            }
        }

        private void FireAction(ButtonAction action)
        {
            ActionFired?.Invoke(action);

            // Notify MageEngine if this is a ground-target spell
            if (action.IsGroundSpell)
                GroundSpellFired?.Invoke();

            switch (action.Type)
            {
                case ActionType.Key:
                    if (action.ModifierKey != VirtualKey.None)
                        InputSimulator.TapKeyWithModifier(action.ModifierKey, action.Key);
                    else
                        InputSimulator.TapKey(action.Key);
                    break;
                case ActionType.LeftClick: InputSimulator.LeftClick(); break;
                case ActionType.RightClick: InputSimulator.RightClick(); break;
                case ActionType.Scroll: InputSimulator.ScrollWheel(action.ScrollDelta); break;
            }
        }

        private void ProcessTurboMode(ButtonAction action, TurboState state, bool pressed, int tickMs)
        {
            switch (action.Mode)
            {
                case TurboMode.Standard:
                    // Fires at fixed interval while held
                    if (pressed)
                    {
                        state.HoldMs += tickMs;
                        if (state.HoldMs >= action.TurboIntervalMs || !state.WasPressed)
                        { FireAction(action); state.HoldMs -= action.TurboIntervalMs; } // subtract to avoid drift
                    }
                    else state.HoldMs = 0;
                    break;

                case TurboMode.Burst:
                    // Fires 3 times quickly on initial press, then waits for release
                    if (pressed && !state.WasPressed)
                    {
                        for (int i = 0; i < 3; i++) FireAction(action);
                        state.HoldMs = 0;
                    }
                    else if (pressed)
                    {
                        state.HoldMs += tickMs;
                        if (state.HoldMs >= action.TurboIntervalMs * 3)
                        { FireAction(action); state.HoldMs = 0; }
                    }
                    else state.HoldMs = 0;
                    break;

                case TurboMode.Rhythmic:
                    // Accelerates then decelerates (sine-wave interval)
                    if (pressed)
                    {
                        state.HoldMs += tickMs;
                        // Guard: TurboIntervalMs=0 würde Modulo/Division by Zero verursachen
                        int safeTurboMs = Math.Max(1, action.TurboIntervalMs);
                        // Phase cycles 0→PI, interval goes from full→half→full
                        double phase = (state.HoldMs % (safeTurboMs * 4)) / (double)(safeTurboMs * 4) * Math.PI;
                        int dynamicInterval = (int)(safeTurboMs * (0.5 + 0.5 * Math.Sin(phase)));
                        dynamicInterval = Math.Max(30, dynamicInterval);
                        if (state.BurstTimer <= 0 || !state.WasPressed)
                        { FireAction(action); state.BurstTimer = dynamicInterval; }
                        state.BurstTimer -= tickMs;
                    }
                    else { state.HoldMs = 0; state.BurstTimer = 0; }
                    break;

                case TurboMode.Adaptive:
                    // First press instant, then slower repeat to avoid skill animation lock
                    if (pressed && !state.WasPressed)
                    { FireAction(action); state.BurstTimer = action.TurboIntervalMs * 2; }
                    else if (pressed)
                    {
                        state.BurstTimer -= tickMs;
                        if (state.BurstTimer <= 0)
                        { FireAction(action); state.BurstTimer = action.TurboIntervalMs; }
                    }
                    else state.BurstTimer = 0;
                    break;
            }
            state.WasPressed = pressed;
        }

        private void ProcessMacroButton(string key, ButtonAction action, bool pressed)
        {
            if (pressed && !_turboStates[key].WasPressed)
            {
                if (!_macroPlayers.ContainsKey(key)) _macroPlayers[key] = new MacroRecorder();
                var m = LoadMacro(action.MacroFilePath!);
                if (m != null) _macroPlayers[key].PlayMacro(m, m.LoopCount > 0 ? m.LoopCount : 1);
            }
            _turboStates[key].WasPressed = pressed;
        }

        // Track file-modification times so the cache is invalidated when the file changes on disk
        private readonly Dictionary<string, DateTime> _macroFileTimes = new();

        private Macro? LoadMacro(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;

                var lastWrite = System.IO.File.GetLastWriteTime(path);

                // Cache hit – only return cached copy if the file hasn't changed since we read it
                if (_loadedMacros.TryGetValue(path, out var cached) &&
                    _macroFileTimes.TryGetValue(path, out var cachedTime) &&
                    lastWrite == cachedTime)
                    return cached;

                // Cache miss or file changed – reload from disk
                var macro = System.Text.Json.JsonSerializer.Deserialize<Macro>(
                    System.IO.File.ReadAllText(path));
                _loadedMacros[path]   = macro;
                _macroFileTimes[path] = lastWrite;
                return macro;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CombatEngine] Macro load error: {ex.Message}");
                return null;
            }
        }

        public void UpdateMacroPlayback(int tickMs)
        {
            foreach (var p in _macroPlayers.Values) if (p.IsPlaying) p.UpdatePlayback(tickMs);
        }

        private class TurboState { public bool WasPressed; public int HoldMs; public int BurstTimer; }
    }

    // ── THESE PARTS WERE ADDED BACK (resolves CS0246) ──────────────────────────
    public enum ActionType { Key, LeftClick, RightClick, Scroll }

    public enum TurboMode { Standard, Burst, Rhythmic, Adaptive }

    public class ButtonAction
    {
        public ActionType Type { get; set; } = ActionType.Key;
        public VirtualKey Key { get; set; } = VirtualKey.F1;
        public bool TurboEnabled { get; set; } = false;
        public int TurboIntervalMs { get; set; } = 100;
        public int ScrollDelta { get; set; } = 120;
        public string Label { get; set; } = string.Empty;
        public TurboMode Mode { get; set; } = TurboMode.Standard;
        public VirtualKey ModifierKey { get; set; } = VirtualKey.None;
        public string? MacroFilePath { get; set; }
        public bool IsMacro => !string.IsNullOrEmpty(MacroFilePath);

        /// <summary>
        /// Mark this action as a Ground-Target spell (Storm Gust, Meteor Storm, etc.).
        /// When fired in Mage mode, the engine automatically enters ground-aim:
        /// movement stops, right stick controls cursor, release trigger = place spell.
        /// </summary>
        public bool IsGroundSpell { get; set; } = false;
    }
}