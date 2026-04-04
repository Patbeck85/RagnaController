using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    public class MageEngine
    {
        [DllImport("user32.dll")] private static extern IntPtr GetCursor();
        private int _cc, _dc, _cw;
        private bool _pr3, _pl1, _al1, _ar1, _al2, _ar2, _cal;
        private IntPtr _nc = IntPtr.Zero;

        public bool MageEnabled { get; set; }
        public int MageBoltKeyVK { get; set; } = 86;
        public int MageBoltCastDelayMs { get; set; } = 1200;
        public float BoltAimSensitivity { get; set; } = 20f;
        public int DefensiveKeyVK { get; set; } = 67;
        public int DefensiveCooldownMs { get; set; } = 800;
        public bool IsActive { get; private set; }
        public MageMode Mode { get; private set; } = MageMode.Idle;
        public MagePhase Phase { get; private set; } = MagePhase.Idle;
        public bool GroundAimHeld { get; private set; }
        public bool GroundSpellPending { get; private set; }
        public int CastCount { get; private set; }

        public event Action<MagePhase>? PhaseChanged;

        public void ToggleMageMode() { IsActive = !IsActive; if (!IsActive) { SetPhase(MagePhase.Idle); Mode = MageMode.Idle; GroundAimHeld = false; CastCount = 0; } }
        public void RecalibrateCursor() { _cal = false; _nc = IntPtr.Zero; }

        public void EnterGroundAim(bool l2, bool r2, bool l1, bool r1) { if (!IsActive) return; GroundAimHeld = true; _al2 = l2; _ar2 = r2; _al1 = l1; _ar1 = r1; Mode = MageMode.Ground; _cw = 120; SetPhase(MagePhase.GroundAiming); }

        public bool Update(float rx, float ry, bool r3, bool r2, bool l2, bool l1, bool r1, int ms)
        {
            if (!IsActive) return false;
            IntPtr c = GetCursor();
            if (!_cal) { _nc = c; _cal = true; } else if (_cw > 0) _cw -= ms; else GroundSpellPending = (c != _nc && c != IntPtr.Zero);
            _cc = Math.Max(0, _cc - ms); _dc = Math.Max(0, _dc - ms);

            if (l1 && !_pl1 && _dc <= 0) { InputSimulator.TapKey((VirtualKey)DefensiveKeyVK); _dc = DefensiveCooldownMs; CastCount++; }
            _pl1 = l1;

            if (GroundAimHeld)
            {
                if ((_al2 && !l2) || (_ar2 && !r2) || (_al1 && !l1) || (_ar1 && !r1)) { InputSimulator.LeftClick(); _cc = 800; GroundAimHeld = false; CastCount++; SetPhase(MagePhase.Casting); Mode = MageMode.Idle; return false; }
                if (r3 && !_pr3) { InputSimulator.TapKey(VirtualKey.Escape); GroundAimHeld = false; SetPhase(MagePhase.Idle); Mode = MageMode.Idle; return false; }
                Move(rx, ry, 18f * (r1 ? 0.5f : 1.0f), 0.15f); _pr3 = r3; return true;
            }

            if (Phase == MagePhase.Casting) { if (_cc <= 0) SetPhase(MagePhase.Idle); return false; }
            if (r2 && !_ar2)
            {
                Mode = MageMode.Bolt; Move(rx, ry, BoltAimSensitivity, 0.15f);
                if (r3 && !_pr3) SetPhase(MagePhase.BoltLocked);
                if (Phase == MagePhase.BoltLocked) SetPhase(MagePhase.BoltSpamming);
                if (Phase == MagePhase.BoltSpamming && _cc <= 0) { InputSimulator.TapKey((VirtualKey)MageBoltKeyVK); _cc = MageBoltCastDelayMs; CastCount++; }
            }
            else if (!r2 && Mode == MageMode.Bolt) { SetPhase(MagePhase.Idle); Mode = MageMode.Idle; }
            _pr3 = r3; return false;
        }

        private void Move(float rx, float ry, float s, float dz) { float sq = rx * rx + ry * ry; if (sq <= dz * dz) return; float m = MathF.Sqrt(sq); int mx = (int)(rx / m * ((m - dz) / (1f - dz)) * s), my = (int)(-ry / m * ((m - dz) / (1f - dz)) * s); if (mx != 0 || my != 0) InputSimulator.MoveMouseRelative(mx, my); }
        private void SetPhase(MagePhase p) { Phase = p; PhaseChanged?.Invoke(p); }
        public string PhaseLabel => Phase.ToString().ToUpper();
    }

    public enum MageMode { Idle, Ground, Bolt }
    public enum MagePhase { Idle, GroundAiming, Casting, BoltLocked, BoltSpamming }
}