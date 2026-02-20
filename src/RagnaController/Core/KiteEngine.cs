using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Kite Engine — Phase-based automated kiting for Archer / Hunter in Ragnarok Online.
    ///
    /// ── How it works (no memory, pure input) ────────────────────────────────────
    ///
    /// RO kiting requires a precise rhythm:
    ///   1. LOCK       → Right-click monster to lock and start attacking
    ///   2. ATTACK     → Fire skill (Double Strafe / Arrow Shower) via turbo
    ///   3. RETREAT    → Click-move in opposite direction of monster (escape cursor)
    ///   4. PIVOT      → Stop retreat, turn cursor back toward monster
    ///   5. RELOCK     → Right-click to re-lock, resume attacking
    ///   (loop)
    ///
    /// ── Controller mapping ──────────────────────────────────────────────────────
    ///
    ///   Right Stick         = Aim direction (point at monster)
    ///   R3 (RS click)       = Snap-lock monster under cursor
    ///   L2 + any movement   = MANUAL RETREAT BOOST (overrides auto-retreat)
    ///   R2 (held)           = Hold ground — pause retreat, keep firing
    ///   L3 (LS click)       = Toggle Kite Mode ON/OFF
    ///   LS                  = Character movement (normal)
    ///   A button            = Manual attack fire (also works during any phase)
    ///
    /// ── Retreat logic ───────────────────────────────────────────────────────────
    ///
    /// When monster is aimed at with right stick: direction vector = (aimX, aimY)
    /// Retreat direction = OPPOSITE: (-aimX, -aimY)
    /// The engine moves the cursor in that direction and left-clicks to run away.
    /// After retreat duration → pivot cursor back → right-click to relock.
    ///
    /// </summary>
    public class KiteEngine
    {
        // ── Config ────────────────────────────────────────────────────────────────
        public bool  KiteEnabled          { get; set; } = false;
        public int   AttackKeyVK          { get; set; } = 0x5A;  // Z = Double Strafe default
        public int   AttackIntervalMs     { get; set; } = 55;    // Fast enough for Double Strafe
        public int   AttacksBeforeRetreat { get; set; } = 3;     // Attacks before kiting back
        public int   RetreatDurationMs    { get; set; } = 600;   // How long to run backwards
        public int   PivotDurationMs      { get; set; } = 180;   // Cursor turn-back time
        public int   RelockDelayMs        { get; set; } = 120;   // Pause before re-locking
        public float RetreatCursorDist    { get; set; } = 90f;   // Pixels to move cursor back
        public float AimSensitivity       { get; set; } = 20f;
        public float AimDeadzone          { get; set; } = 0.18f;

        // ── State ─────────────────────────────────────────────────────────────────
        public KitePhase  Phase           { get; private set; } = KitePhase.Idle;
        public bool       IsActive        { get; private set; } = false;
        public string     PhaseLabel      => Phase switch
        {
            KitePhase.Locking    => "LOCKING TARGET",
            KitePhase.Attacking  => "ATTACKING",
            KitePhase.Retreating => "RETREATING",
            KitePhase.Pivoting   => "PIVOTING",
            KitePhase.Relocking  => "RELOCKING",
            _                    => "IDLE"
        };
        public int        AttacksFired    { get; private set; } = 0;

        // Events
        public event Action<KitePhase>? PhaseChanged;

        // ── Internal timers & counters ────────────────────────────────────────────
        private int   _phaseTimer        = 0;
        private int   _attackCooldown    = 0;
        private int   _attacksThisCycle  = 0;

        // Aim vector (set from right stick every tick)
        private float _aimX              = 0f;
        private float _aimY              = 0f;
        private float _lastAimX          = 1f; // fallback: aim right
        private float _lastAimY          = 0f;

        // Cursor accumulator for smooth retreat movement
        private float _retreatAccumX     = 0f;
        private float _retreatAccumY     = 0f;
        private float _retreatDirX       = 0f;
        private float _retreatDirY       = 0f;

        // Flags from last frame
        private bool  _prevR3            = false;
        private bool  _holdGround        = false; // R2 = hold ground

        // ── Public control ────────────────────────────────────────────────────────

        public void ToggleKiteMode()
        {
            IsActive = !IsActive;
            if (IsActive)
                SetPhase(KitePhase.Locking);
            else
                SetPhase(KitePhase.Idle);
        }

        public void ForceRetreat()
        {
            if (!IsActive) return;
            BeginRetreat();
        }

        // ── Main Update ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call every engine tick (16 ms).
        /// rightX/Y = right stick (-1..+1)
        /// r3        = right stick button (snap-lock)
        /// holdGround = R2 held (stop retreat, keep firing)
        /// manualRetreat = L2 held (force retreat now)
        /// tickMs    = tick interval (usually 16)
        /// </summary>
        public void Update(float rightX, float rightY,
                           bool r3, bool holdGround, bool manualRetreat,
                           int tickMs)
        {
            if (!IsActive) return;

            _holdGround = holdGround;

            // ── Process aim input ─────────────────────────────────────────────────
            float mag = MathF.Sqrt(rightX * rightX + rightY * rightY);
            if (mag > AimDeadzone)
            {
                float norm = (mag - AimDeadzone) / (1f - AimDeadzone);
                _aimX = rightX / mag * norm;
                _aimY = rightY / mag * norm;
                // Remember last valid aim for retreat direction
                _lastAimX = _aimX;
                _lastAimY = _aimY;
            }
            else
            {
                _aimX = 0f;
                _aimY = 0f;
            }

            // ── R3: snap-lock monster under cursor ────────────────────────────────
            if (r3 && !_prevR3)
            {
                InputSimulator.RightClick();
                SetPhase(KitePhase.Attacking);
            }
            _prevR3 = r3;

            // ── Tick cooldowns ────────────────────────────────────────────────────
            _phaseTimer     = Math.Max(0, _phaseTimer     - tickMs);
            _attackCooldown = Math.Max(0, _attackCooldown - tickMs);

            // ── Manual retreat override ───────────────────────────────────────────
            if (manualRetreat && Phase != KitePhase.Retreating && Phase != KitePhase.Idle)
            {
                BeginRetreat();
                return;
            }

            // ── State machine ─────────────────────────────────────────────────────
            switch (Phase)
            {
                case KitePhase.Locking:
                    UpdateLocking(tickMs);
                    break;
                case KitePhase.Attacking:
                    UpdateAttacking(tickMs);
                    break;
                case KitePhase.Retreating:
                    UpdateRetreating(tickMs);
                    break;
                case KitePhase.Pivoting:
                    UpdatePivoting(tickMs);
                    break;
                case KitePhase.Relocking:
                    UpdateRelocking(tickMs);
                    break;
            }
        }

        // ── Phase handlers ────────────────────────────────────────────────────────

        private void UpdateLocking(int tickMs)
        {
            // Move cursor toward aim direction then right-click to lock
            MoveCursorTowardAim(tickMs, AimSensitivity * 1.5f);

            if (_phaseTimer <= 0)
            {
                // Try to lock: right-click
                InputSimulator.RightClick();
                _attacksThisCycle = 0;
                SetPhase(KitePhase.Attacking);
            }
        }

        private void UpdateAttacking(int tickMs)
        {
            // Keep cursor aimed at monster
            MoveCursorTowardAim(tickMs, AimSensitivity * 0.6f);

            // Fire attack key
            if (_attackCooldown <= 0 && !_holdGround == false || _attackCooldown <= 0)
            {
                InputSimulator.TapKey((VirtualKey)AttackKeyVK);
                _attackCooldown = AttackIntervalMs;
                _attacksThisCycle++;
                AttacksFired++;
            }

            // After N attacks → initiate kite retreat
            if (_attacksThisCycle >= AttacksBeforeRetreat && !_holdGround)
                BeginRetreat();
        }

        private void UpdateRetreating(int tickMs)
        {
            if (_holdGround)
            {
                // R2 held: cancel retreat, go back to attacking
                SetPhase(KitePhase.Pivoting);
                return;
            }

            // Move cursor in retreat direction (opposite of aim)
            float speed = RetreatCursorDist / (RetreatDurationMs / (float)tickMs);
            _retreatAccumX += _retreatDirX * speed;
            _retreatAccumY += _retreatDirY * speed;

            int mx = (int)_retreatAccumX;
            int my = (int)_retreatAccumY;
            _retreatAccumX -= mx;
            _retreatAccumY -= my;

            // Click-to-move: left-click in retreat direction every ~180ms
            if (_phaseTimer % 180 < tickMs)
                InputSimulator.LeftClick();

            if (_phaseTimer <= 0)
                SetPhase(KitePhase.Pivoting);
        }

        private void UpdatePivoting(int tickMs)
        {
            // Swing cursor back toward monster (use aim or last known aim)
            MoveCursorTowardAim(tickMs, AimSensitivity * 2.5f);

            if (_phaseTimer <= 0)
                SetPhase(KitePhase.Relocking);
        }

        private void UpdateRelocking(int tickMs)
        {
            if (_phaseTimer <= 0)
            {
                // Re-lock + immediately fire first shot
                InputSimulator.RightClick();
                _attacksThisCycle = 0;
                _attackCooldown   = 0;
                SetPhase(KitePhase.Attacking);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void BeginRetreat()
        {
            // Freeze retreat direction as OPPOSITE of current aim
            float len = MathF.Sqrt(_lastAimX * _lastAimX + _lastAimY * _lastAimY);
            _retreatDirX = len > 0.01f ? -_lastAimX / len : 0f;
            _retreatDirY = len > 0.01f ?  _lastAimY / len : 0f; // Y inverted (screen coords)

            _retreatAccumX = 0f;
            _retreatAccumY = 0f;

            SetPhase(KitePhase.Retreating, RetreatDurationMs);
        }

        private void MoveCursorTowardAim(int tickMs, float speed)
        {
            float ax = _aimX != 0 || _aimY != 0 ? _aimX : _lastAimX;
            float ay = _aimX != 0 || _aimY != 0 ? _aimY : _lastAimY;

            int mx = (int)(ax * speed);
            int my = (int)(-ay * speed); // Screen Y inverted

            if (mx != 0 || my != 0)
                InputSimulator.MoveMouseRelative(mx, my);
        }

        private void SetPhase(KitePhase phase, int durationMs = -1)
        {
            Phase       = phase;
            _phaseTimer = durationMs >= 0 ? durationMs : GetDefaultDuration(phase);
            PhaseChanged?.Invoke(phase);
        }

        private int GetDefaultDuration(KitePhase phase) => phase switch
        {
            KitePhase.Locking   => 200,
            KitePhase.Attacking => int.MaxValue, // Exits on attack count
            KitePhase.Retreating => RetreatDurationMs,
            KitePhase.Pivoting  => PivotDurationMs,
            KitePhase.Relocking => RelockDelayMs,
            _                   => 0
        };
    }

    // ── Enum ──────────────────────────────────────────────────────────────────────
    public enum KitePhase
    {
        Idle,
        Locking,    // Initial lock on target
        Attacking,  // Firing skills
        Retreating, // Click-moving backwards
        Pivoting,   // Turning cursor back to monster
        Relocking   // Re-locking before next attack cycle
    }
}
