using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Melee Auto-Target System — no memory access, pure input simulation.
    ///
    /// How it works in Ragnarok Online:
    ///   1. TAB          = cycles to next nearest monster (RO built-in)
    ///   2. Right-Click  = lock target / attack-move to clicked monster
    ///   3. Left-Click   = select monster under cursor
    ///   4. Z key (default) = basic attack / use skill on current target
    ///
    /// Strategy:
    ///   - RIGHT STICK held     → directional aim: move cursor in stick direction
    ///   - R3 (right stick btn) → snap-cycle Tab to next target in aim direction
    ///   - AUTO-ATTACK toggle   → when target locked, auto-repeat attack key
    ///   - COMBAT MODE ON       → auto-retarget when target is lost (Tab-cycle)
    /// </summary>
    public class AutoTargetEngine
    {
        // ── Config (set from profile) ─────────────────────────────────────────────
        public bool  AutoAttackEnabled   { get; set; } = true;
        public bool  AutoRetargetEnabled { get; set; } = true;
        public int   AttackKey_VK        { get; set; } = (int)VirtualKey.Z;
        public int   TabCycleMs          { get; set; } = 80;   // How long between Tab presses during seek
        public int   AttackIntervalMs    { get; set; } = 60;   // How fast auto-attack fires
        public float AimSensitivity      { get; set; } = 22f;  // Cursor speed when aiming
        public float AimDeadzone         { get; set; } = 0.20f;

        // ── State ─────────────────────────────────────────────────────────────────
        public CombatState State           { get; private set; } = CombatState.Idle;
        public bool         IsTargetLocked { get; private set; } = false;
        public string       StateLabel     => State switch
        {
            CombatState.Seeking   => "SEEKING TARGET",
            CombatState.Engaged   => "TARGET LOCKED",
            CombatState.Attacking => "AUTO-ATTACKING",
            _                     => "IDLE"
        };

        // Events for UI
        public event Action<CombatState>? StateChanged;

        // ── Timers ────────────────────────────────────────────────────────────────
        private int _tabCooldownMs      = 0;
        private int _attackCooldownMs   = 0;
        private int _retargetCooldownMs = 0;
        private int _engagedMs          = 0;   // How long we've been engaged
        private int _seekCycles         = 0;   // How many Tab presses during current seek

        // ── Aim state ────────────────────────────────────────────────────────────
        private float _aimX;
        private float _aimY;
        private float _aimAccumX;
        private float _aimAccumY;

        // ── Public control ────────────────────────────────────────────────────────

        /// <summary>Toggle combat mode on/off (mapped to a button).</summary>
        public void ToggleCombatMode()
        {
            if (State == CombatState.Idle)
                EnterSeek();
            else
                EnterIdle();
        }

        /// <summary>Manually request immediate retarget (Tab cycle).</summary>
        public void ManualRetarget()
        {
            if (State == CombatState.Idle) return;
            _tabCooldownMs = 0;
            DoTabCycle();
        }

        /// <summary>Signal that a target was manually selected (right-click lock).</summary>
        public void OnTargetLocked()
        {
            IsTargetLocked = true;
            _seekCycles    = 0;
            SetState(CombatState.Engaged);
        }

        /// <summary>Signal that target was lost (e.g. monster died, moved out of range).</summary>
        public void OnTargetLost()
        {
            IsTargetLocked = false;
            if (AutoRetargetEnabled && State != CombatState.Idle)
            {
                _retargetCooldownMs = 300; // Short pause then re-seek
                SetState(CombatState.Seeking);
            }
        }

        // ── Main Update (call every tick, tickMs = 16) ────────────────────────────

        /// <summary>
        /// Call every engine tick.
        /// rightX/Y = right analog stick (-1..+1), pressed = R3 button,
        /// rightShoulderHeld = RB/R1 held for aim-lock mode.
        /// </summary>
        public void Update(float rightX, float rightY, bool r3Pressed, bool r3WasPressed,
                           bool rightShoulderHeld, int tickMs)
        {
            TickCooldowns(tickMs);

            // ── RIGHT STICK = directional aim ─────────────────────────────────────
            float aimMag = MathF.Sqrt(rightX * rightX + rightY * rightY);
            if (aimMag > AimDeadzone)
            {
                _aimX = rightX / aimMag * ((aimMag - AimDeadzone) / (1f - AimDeadzone));
                _aimY = rightY / aimMag * ((aimMag - AimDeadzone) / (1f - AimDeadzone));

                _aimAccumX += _aimX * AimSensitivity;
                _aimAccumY += -_aimY * AimSensitivity;

                int mx = (int)_aimAccumX;
                int my = (int)_aimAccumY;
                _aimAccumX -= mx;
                _aimAccumY -= my;

                if (mx != 0 || my != 0)
                    InputSimulator.MoveMouseRelative(mx, my);
            }
            else
            {
                _aimX = _aimY = 0f;
            }

            // ── R3 press = snap-target monster under cursor ───────────────────────
            if (r3Pressed && !r3WasPressed)
                SnapTargetUnderCursor();

            // ── RB held + right stick = aim-then-select (directional snap) ────────
            if (rightShoulderHeld && aimMag > AimDeadzone)
                AimDirectionalSelect(tickMs);

            // ── State machine ─────────────────────────────────────────────────────
            switch (State)
            {
                case CombatState.Seeking:
                    UpdateSeeking(tickMs);
                    break;

                case CombatState.Engaged:
                    UpdateEngaged(tickMs);
                    break;

                case CombatState.Attacking:
                    UpdateAttacking(tickMs);
                    break;
            }
        }

        // ── State machine ─────────────────────────────────────────────────────────

        private void UpdateSeeking(int tickMs)
        {
            // Auto-retarget: press Tab until we lock something
            if (_tabCooldownMs <= 0 && AutoRetargetEnabled)
            {
                DoTabCycle();
                _seekCycles++;

                // After 1 Tab press → assume we've targeted something, engage
                // (RO selects the nearest monster with Tab)
                if (_seekCycles >= 1)
                {
                    OnTargetLocked();
                }
            }
        }

        private void UpdateEngaged(int tickMs)
        {
            _engagedMs += tickMs;

            // After a brief moment engaged → start auto-attacking
            if (AutoAttackEnabled && _engagedMs >= 150)
                SetState(CombatState.Attacking);
        }

        private void UpdateAttacking(int tickMs)
        {
            // Fire attack key on interval
            if (_attackCooldownMs <= 0)
            {
                InputSimulator.TapKey((VirtualKey)AttackKey_VK);
                _attackCooldownMs = AttackIntervalMs;
            }

            // Periodically re-confirm target with right-click (keeps lock alive in RO)
            if (_retargetCooldownMs <= 0)
            {
                InputSimulator.RightClick();
                _retargetCooldownMs = 3000; // Every 3 seconds
            }
        }

        // ── Input helpers ─────────────────────────────────────────────────────────

        /// <summary>Press Tab to cycle to next nearest monster.</summary>
        private void DoTabCycle()
        {
            InputSimulator.TapKey(VirtualKey.Tab);
            _tabCooldownMs = TabCycleMs;
        }

        /// <summary>Right-click monster under cursor to lock + attack.</summary>
        private void SnapTargetUnderCursor()
        {
            InputSimulator.RightClick();
            OnTargetLocked();
        }

        /// <summary>
        /// While RB is held and right stick is pushed: move cursor in stick direction,
        /// then left-click to select monster. Creates a "directional snap" feel.
        /// </summary>
        private void AimDirectionalSelect(int tickMs)
        {
            // Click after the cursor has been aimed (small delay for feel)
            if (_tabCooldownMs <= 0)
            {
                InputSimulator.LeftClick();
                _tabCooldownMs = 200; // Prevent rapid-fire clicks
            }
        }

        // ── Transitions ───────────────────────────────────────────────────────────

        private void EnterSeek()
        {
            _seekCycles    = 0;
            _tabCooldownMs = 0;
            _engagedMs     = 0;
            IsTargetLocked = false;
            SetState(CombatState.Seeking);
        }

        private void EnterIdle()
        {
            IsTargetLocked = false;
            _seekCycles    = 0;
            _engagedMs     = 0;
            SetState(CombatState.Idle);
        }

        private void SetState(CombatState newState)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState);
        }

        private void TickCooldowns(int ms)
        {
            _tabCooldownMs      = Math.Max(0, _tabCooldownMs      - ms);
            _attackCooldownMs   = Math.Max(0, _attackCooldownMs   - ms);
            _retargetCooldownMs = Math.Max(0, _retargetCooldownMs - ms);
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────────

    public enum CombatState
    {
        Idle,       // Engine off / not in combat
        Seeking,    // Pressing Tab to find nearest monster
        Engaged,    // Target selected, preparing to attack
        Attacking   // Firing attack key on interval
    }
}
