using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RagnaController.Core
{
    public class AutoTargetEngine
    {
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

        public bool AutoAttackEnabled   { get; set; } = true;
        public bool AutoRetargetEnabled { get; set; } = true;
        public bool SmartSkillEnabled   { get; set; } = true;  // Cursor-Juggling opt-in
        public int  AttackKey_VK        { get; set; } = 90;
        public int  TabCycleMs          { get; set; } = 80;
        public int  AttackIntervalMs    { get; set; } = 60;
        public float AimSensitivity     { get; set; } = 22f;
        public float AimDeadzone        { get; set; } = 0.20f;
        public int  SkillInterruptMs    { get; set; } = 750;

        public CombatState State            { get; private set; } = CombatState.Idle;
        public bool IsTargetLocked          { get; private set; }
        public bool SuppressMovementClicks  { get; private set; }
        public string StateLabel => State switch
        {
            CombatState.Seeking   => "SEEKING",
            CombatState.Engaged   => "LOCKED",
            CombatState.Attacking => _skillPause > 0 ? "SKILL PAUSE" : "ATTACKING",
            _                     => "IDLE"
        };

        public event Action<CombatState>? StateChanged;

        // volatile: _skillPause and _ac are written from both Task.Run (SmartSkill) and the tick thread
        private volatile int _tc, _ac, _rc, _ems, _sc, _asc, _wac;
        private volatile int _skillPause;
        private float _aax, _aay;

        // Last known target position — updated on every lock and RS movement
        private POINT _lockPos;
        private bool  _lockPosValid = false;

        // Semaphore verhindert dass mehrere SmartSkill-Tasks gleichzeitig laufen
        private readonly SemaphoreSlim _skillSem = new(1, 1);

        public void ToggleCombatMode() { if (State == CombatState.Idle) EnterSeek(); else EnterIdle(); }

        public void NotifySkillFired()
        {
            _skillPause = SkillInterruptMs;
            _ac         = SkillInterruptMs;
            _wac        = SkillInterruptMs;
            // RightClick only makes sense in Attacking state — in Seeking it would interrupt Tab cycling
            if (State == CombatState.Attacking) InputSimulator.RightClick();
        }

        public void OnTargetLocked()
        {
            IsTargetLocked = true;
            _sc = 0;
            // Save cursor position when target is locked
            if (GetCursorPos(out _lockPos)) _lockPosValid = true;
            SetState(CombatState.Engaged);
        }

        public void OnTargetLost()
        {
            IsTargetLocked  = false;
            _lockPosValid   = false;
            if (AutoRetargetEnabled && State != CombatState.Idle) { _rc = 250; SetState(CombatState.Seeking); }
        }

        /// <summary>
        /// Smart Skill: snaps cursor to locked target, fires skill, restores cursor position.
        /// Completes in ~15ms — invisible to the player.
        /// Only called when SmartSkillEnabled && State != Idle && IsTargetLocked.
        /// </summary>
        public void FireSmartSkill(VirtualKey skillKey)
        {
            if (!SmartSkillEnabled || !IsTargetLocked || !_lockPosValid || State == CombatState.Idle)
            {
                // Fallback: no lock — TapKey + LeftClick so melee skills register
                InputSimulator.TapKey(skillKey);
                InputSimulator.LeftClick();
                NotifySkillFired();
                return;
            }

            // Fire-and-forget mit Semaphore — kein async void race condition
            Task.Run(async () =>
            {
                if (!await _skillSem.WaitAsync(50)) return; // wait up to 50ms; drop if engine is still busy
                try
                {
                    // 1. Save current cursor position
                    GetCursorPos(out POINT saved);

                    // 2. Snap cursor to locked target
                    InputSimulator.MoveMouseAbsolute(_lockPos.X, _lockPos.Y);

                    // 3. Fire skill key and click
                    InputSimulator.TapKey(skillKey);
                    await Task.Delay(12);
                    InputSimulator.LeftClick();

                    // 4. Restore cursor
                    InputSimulator.MoveMouseAbsolute(saved.X, saved.Y);

                    // 5. Pause auto-attacks during skill animation
                    NotifySkillFired();
                }
                finally { _skillSem.Release(); }
            });
        }

        public void Update(float rx, float ry, bool r3, bool r3w, bool rb, int ms, float lx = 0, float ly = 0)
        {
            _tc = Math.Max(0, _tc - ms); _ac = Math.Max(0, _ac - ms); _rc = Math.Max(0, _rc - ms);
            _asc = Math.Max(0, _asc - ms); _wac = Math.Max(0, _wac - ms); _skillPause = Math.Max(0, _skillPause - ms);

            float wsq = lx * lx + ly * ly;
            SuppressMovementClicks = IsTargetLocked && wsq > 0.04f
                && (State == CombatState.Attacking || _skillPause > 0);

            if (SuppressMovementClicks && _wac <= 0)
            {
                float wm = MathF.Sqrt(wsq);
                int wx = (int)(lx / wm * AimSensitivity * 0.7f), wy = (int)(-ly / wm * AimSensitivity * 0.7f);
                if (wx != 0 || wy != 0) InputSimulator.MoveMouseRelative(wx, wy);
                InputSimulator.RightClick(); _wac = 300;
                // Lock-Position bei Bewegung aktualisieren
                if (GetCursorPos(out _lockPos)) _lockPosValid = true;
            }

            float rsq = rx * rx + ry * ry;
            if (rsq > AimDeadzone * AimDeadzone)
            {
                float rm = MathF.Sqrt(rsq), rn = (rm - AimDeadzone) / (1f - AimDeadzone);
                _aax += (rx / rm * rn) * AimSensitivity; _aay += (-ry / rm * rn) * AimSensitivity;
                int mx = (int)_aax, my = (int)_aay; _aax -= mx; _aay -= my;
                if (mx != 0 || my != 0) InputSimulator.MoveMouseRelative(mx, my);
                if (rb && _asc <= 0) { InputSimulator.LeftClick(); _asc = 150; }
                // Manuelles Zielen aktualisiert Lock-Position
                if (GetCursorPos(out _lockPos)) _lockPosValid = true;
            }

            if (r3 && !r3w && State != CombatState.Idle) { InputSimulator.RightClick(); OnTargetLocked(); }

            switch (State)
            {
                case CombatState.Seeking:
                    if (_rc <= 0 && _tc <= 0 && AutoRetargetEnabled)
                    {
                        InputSimulator.TapKey(VirtualKey.Tab);
                        _tc = TabCycleMs;
                        _sc++;
                        if (_sc >= 3) OnTargetLocked();
                    }
                    break;
                case CombatState.Engaged:
                    _ems += ms;
                    if (AutoAttackEnabled && _ems >= 100) SetState(CombatState.Attacking);
                    break;
                case CombatState.Attacking:
                    if (_skillPause <= 0 && _ac <= 0)
                    {
                        InputSimulator.TapKey((VirtualKey)AttackKey_VK);
                        _ac = AttackIntervalMs;
                    }
                    if (_rc <= 0) { InputSimulator.RightClick(); _rc = 2000; }
                    break;
            }
        }

        private void EnterSeek() { _sc = 0; _tc = 0; _ems = 0; IsTargetLocked = false; _lockPosValid = false; SetState(CombatState.Seeking); }
        private void EnterIdle() { IsTargetLocked = false; _lockPosValid = false; SetState(CombatState.Idle); }
        private void SetState(CombatState s) { if (State == s) return; State = s; StateChanged?.Invoke(s); }
    }

    public enum CombatState { Idle, Seeking, Engaged, Attacking }
}
