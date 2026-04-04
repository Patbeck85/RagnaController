using System;
using System.Collections.Generic;
using RagnaController.Profiles;

namespace RagnaController.Core
{
    
    public enum ActionType { Key, LeftClick, RightClick, Scroll, Combo, SwitchWindow }
    public enum TurboMode  { Standard, Burst, Rhythmic, Adaptive }

    public class ButtonAction
    {
        public ActionType Type           { get; set; }
        public VirtualKey Key            { get; set; }
        public string     Label          { get; set; } = "";
        public bool       TurboEnabled   { get; set; }
        public int        TurboIntervalMs { get; set; } = 100;
        public TurboMode  Mode           { get; set; } = TurboMode.Standard;
        public string?    MacroFilePath  { get; set; }
        public bool       IsMacro        => !string.IsNullOrEmpty(MacroFilePath);
        public bool       IsGroundSpell  { get; set; }
        // Target process name for ActionType.SwitchWindow (partial match, e.g. "ragexe")
        public string     WindowTarget   { get; set; } = "ragexe";
    }

    public class CombatEngine
    {
        private readonly Dictionary<string, TurboState> _turboStates  = new();
        private readonly Dictionary<string, MacroRecorder> _macroPlayers = new();
        // Fix: Macro-Cache — kein Disk-Read bei jedem Tastendruck
        private readonly Dictionary<string, Macro?> _macroCache = new();
        private Profile? _profile;
        private string   _prefix = "";
        private static readonly string[] _prefixes = { "", "L1+", "R1+", "L2+", "R2+" };

        public event Action<ButtonAction>? ActionFired;
        public event Action?               GroundSpellFired;

        public void LoadProfile(Profile p)
        {
            _profile = p;
            _turboStates.Clear();
            _macroCache.Clear();
        }

        public void UpdateLayers(bool l1, bool r1, bool l2, bool r2)
            => _prefix = l1 ? _prefixes[1] : r1 ? _prefixes[2] : l2 ? _prefixes[3] : r2 ? _prefixes[4] : _prefixes[0];

        /// <summary>
        /// Returns true if any currently pressed button is mapped to ActionType.Combo.
        /// Wird von HybridEngine genutzt um die ComboEngine zu aktivieren.
        /// </summary>
        public bool IsComboActionHeld(SharpDX.XInput.GamepadButtonFlags buttons)
        {
            if (_profile == null) return false;
            foreach (SharpDX.XInput.GamepadButtonFlags flag in
                     (SharpDX.XInput.GamepadButtonFlags[])Enum.GetValues(typeof(SharpDX.XInput.GamepadButtonFlags)))
            {
                if (flag == 0 || !buttons.HasFlag(flag)) continue;
                string name   = flag.ToString();
                string mapped = _prefix + name;
                if (_profile.ButtonMappings.TryGetValue(mapped, out var a) && a.Type == ActionType.Combo) return true;
                if (_prefix != "" && _profile.ButtonMappings.TryGetValue(name, out var b) && b.Type == ActionType.Combo) return true;
            }
            return false;
        }

        public void ProcessButton(string btn, bool pr, int ms)
        {
            if (_profile == null) return;
            string key = _prefix + btn;

            if (pr)
            {
                foreach (var p in _prefixes)
                    if (p != _prefix && _turboStates.TryGetValue(p + btn, out var s)) s.WasPressed = false;
            }

            if (!_profile.ButtonMappings.TryGetValue(key, out var a))
            {
                if (_prefix == "" || !_profile.ButtonMappings.TryGetValue(btn, out a)) return;
            }

            // Combo actions are handled by ComboEngine — skip here
            if (a.Type == ActionType.Combo) return;

            // SwitchWindow fires once on initial press — no turbo, no macro
            if (a.Type == ActionType.SwitchWindow)
            {
                if (!_turboStates.TryGetValue(key, out var sw)) _turboStates[key] = sw = new TurboState();
                if (pr && !sw.WasPressed) Fire(a);
                sw.WasPressed = pr;
                return;
            }

            if (!_turboStates.TryGetValue(key, out var st)) _turboStates[key] = st = new TurboState();

            if (a.IsMacro && pr && !st.WasPressed)
            {
                if (!_macroPlayers.TryGetValue(key, out var pl)) _macroPlayers[key] = pl = new MacroRecorder();
                // Fix: Cache — kein Disk-Read bei jedem Tastendruck
                if (!_macroCache.TryGetValue(a.MacroFilePath!, out var m))
                {
                    m = MacroRecorder.LoadMacro(a.MacroFilePath!);
                    _macroCache[a.MacroFilePath!] = m;
                }
                if (m != null) pl.Play(m, m.LoopCount);
            }
            else if (a.TurboEnabled)
            {
                if (pr)
                {
                    st.HoldMs += ms;
                    if (st.HoldMs >= a.TurboIntervalMs || !st.WasPressed) { Fire(a); st.HoldMs = 0; }
                }
                else st.HoldMs = 0;
            }
            else if (pr && !st.WasPressed) Fire(a);

            st.WasPressed = pr;
        }

        private void Fire(ButtonAction a)
        {
            // Only raise event — HybridEngine decides between normal and SmartSkill path
            ActionFired?.Invoke(a);
            if (a.IsGroundSpell) GroundSpellFired?.Invoke();
            // Direct InputSimulator calls removed: HybridEngine.ActionFired handler is now responsible
        }

        public void UpdateMacroPlayback(int ms)
        {
            foreach (var pl in _macroPlayers.Values)
                if (pl.IsPlaying) pl.UpdatePlayback(ms);
        }

        class TurboState { public bool WasPressed; public int HoldMs; }
    }
}
