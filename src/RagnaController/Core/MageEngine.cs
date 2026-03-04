using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Mage Engine — Ground-target spells and Bolt spam.
    ///
    /// ── GROUND SPELL FLOW ────────────────────────────────────────────────────────
    ///
    ///   1. Player holds L2 or R2
    ///   2. Presses A / B / X / Y  →  CombatEngine sends spell key to RO
    ///                                 CombatEngine fires GroundSpellFired event
    ///                                 HybridEngine calls MageEngine.EnterGroundAim()
    ///   3. Movement stops immediately
    ///      Right stick = cursor control for targeting
    ///      GetCursor() watches for RO's ground-target crosshair
    ///   4. Player releases L2 / R2  →  LeftClick places the spell
    ///      Left stick resumes walking immediately
    ///
    /// ── BOLT MODE ────────────────────────────────────────────────────────────────
    ///
    ///   R2 held (without combo press) → right stick aims, R3 locks + auto-bolts
    ///
    /// ── LAYOUT ──────────────────────────────────────────────────────────────────
    ///
    ///   L3             = Toggle Mage mode ON/OFF
    ///   Left Stick     = Walk (always — even in Mage mode)
    ///   Right Stick    = Cursor (only while ground-aiming or bolt mode)
    ///   L1 + A/B/X/Y  = Ground spell → aim → release L1 to cast
    ///   L2 + A/B/X/Y  = Ground spell → aim → release L2 to cast
    ///   R1 + A/B/X/Y  = Ground spell → aim → release R1 to cast
    ///   R2 + A/B/X/Y  = Ground spell → aim → release R2 to cast
    ///   R2 alone       = Bolt mode
    ///   R3             = Ground-aim: CANCEL (sends ESC to RO) | Bolt: lock target + spam
    ///   R1 (held)      = Fine-Aim: half cursor speed for precision targeting
    ///   L1             = Quick defensive cast (Safety Wall / Ice Wall)
    ///
    /// </summary>
    public class MageEngine
    {
        // ── Win32 ─────────────────────────────────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetCursor();

        // ── Config ────────────────────────────────────────────────────────────────
        public bool  MageEnabled          { get; set; } = false;

        /// <summary>
        /// VK of the ground spell key — kept for profile compatibility.
        /// CombatEngine sends the key; this is used as fallback if needed.
        /// </summary>
        public int   GroundSpellKeyVK     { get; set; } = 0x5A;  // Z default
        public float GroundAimSensitivity { get; set; } = 18f;
        public float GroundAimDeadzone    { get; set; } = 0.15f;
        public int   BoltKeyVK            { get; set; } = 0x56;   // V default
        public int   BoltCastDelayMs      { get; set; } = 1200;
        public float BoltAimSensitivity   { get; set; } = 20f;
        public int   DefensiveKeyVK       { get; set; } = 0x43;   // C default
        public int   DefensiveCooldownMs  { get; set; } = 800;
        public int   CastsBeforeSPWarning { get; set; } = 15;

        // ── State ─────────────────────────────────────────────────────────────────
        public bool      IsActive        { get; private set; } = false;
        public MageMode  Mode            { get; private set; } = MageMode.Idle;
        public MagePhase Phase           { get; private set; } = MagePhase.Idle;

        /// <summary>True while holding L2/R2 after a ground-spell combo. Suppresses movement.</summary>
        public bool GroundAimHeld        { get; private set; } = false;

        /// <summary>True when RO cursor changed to ground-target crosshair.</summary>
        public bool GroundSpellPending   { get; private set; } = false;

        public string PhaseLabel => Phase switch
        {
            MagePhase.GroundAiming  => "GROUND AIM ▶ Release to cast",
            MagePhase.Casting       => "CASTING...",
            MagePhase.BoltLocked    => "BOLT LOCKED",
            MagePhase.BoltSpamming  => "BOLT SPAM",
            _                       => IsActive ? "MAGE READY" : "IDLE"
        };

        public int  CastCount  { get; private set; } = 0;
        public bool SPWarning  => CastCount >= CastsBeforeSPWarning;

        // Events
        public event Action<MagePhase>? PhaseChanged;

        // ── Internal ──────────────────────────────────────────────────────────────
        private int   _castCooldown       = 0;
        private int   _defensiveCooldown  = 0;
        private bool  _prevR3             = false;
        private bool  _prevL1             = false;
        private bool  _prevL2             = false;
        private bool  _prevR2             = false;

        // Which trigger started ground-aim (so we detect the right release)
        private bool  _groundAimFromL2    = false;
        private bool  _groundAimFromR2    = false;
        private bool  _groundAimFromL1    = false;
        private bool  _groundAimFromR1    = false;

        // Cursor detection
        private IntPtr _normalCursor      = IntPtr.Zero;
        private bool   _cursorCalibrated  = false;
        private int    _cursorWaitMs      = 0;

        // ── Public control ────────────────────────────────────────────────────────

        public void ToggleMageMode()
        {
            IsActive = !IsActive;
            if (!IsActive)
            {
                SetPhase(MagePhase.Idle);
                Mode          = MageMode.Idle;
                GroundAimHeld = false;
                CastCount     = 0;
            }
        }

        public void ResetCastCount() => CastCount = 0;

        /// <summary>
        /// Called by HybridEngine when a face button (A/B/X/Y) is pressed in Mage mode.
        /// If ground-aim was triggered via CombatEngine's GroundSpellFired event, this is a no-op.
        /// Otherwise provides a hook for future extensions (SP tracking, feedback etc.)
        /// </summary>
        public void NotifySkillFired()
        {
            // CastCount is already incremented in CastGroundSpell / bolt spam.
            // This is intentionally lightweight — the real work is done via EnterGroundAim.
        }

        /// <summary>Re-learn the normal cursor handle (call after Alt+Tab).</summary>
        public void RecalibrateCursor()
        {
            _cursorCalibrated = false;
            _normalCursor     = IntPtr.Zero;
        }

        /// <summary>
        /// Called by HybridEngine when CombatEngine fires a ground-spell combo.
        /// The spell key was already sent — now we enter targeting mode.
        /// </summary>
        public void EnterGroundAim(bool fromL2, bool fromR2, bool fromL1 = false, bool fromR1 = false)
        {
            if (!IsActive) return;
            GroundAimHeld      = true;
            _groundAimFromL2   = fromL2;
            _groundAimFromR2   = fromR2;
            _groundAimFromL1   = fromL1;
            _groundAimFromR1   = fromR1;
            Mode               = MageMode.Ground;
            _cursorWaitMs      = 120;
            SetPhase(MagePhase.GroundAiming);
        }

        // ── Main Update ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call every tick (16ms).
        /// Returns true if left-stick movement should be suppressed.
        /// </summary>
        public bool Update(float rightX, float rightY,
                           bool r3, bool r2Held, bool l2Held, bool l1Held, bool r1Held, int tickMs)
        {
            if (!IsActive) return false;

            UpdateCursorDetection(tickMs);

            _castCooldown      = Math.Max(0, _castCooldown - tickMs);
            _defensiveCooldown = Math.Max(0, _defensiveCooldown - tickMs);

            // ── L1: Quick defensive cast ──────────────────────────────────────────
            if (l1Held && !_prevL1 && _defensiveCooldown <= 0)
            {
                InputSimulator.TapKey((VirtualKey)DefensiveKeyVK);
                _defensiveCooldown = DefensiveCooldownMs;
                CastCount++;
            }
            _prevL1 = l1Held;

            // ── Ground-aim active: check for trigger release → cast ───────────────
            if (GroundAimHeld)
            {
                bool triggerReleased = (_groundAimFromL2 && !l2Held)
                                    || (_groundAimFromR2 && !r2Held)
                                    || (_groundAimFromL1 && !l1Held)
                                    || (_groundAimFromR1 && !r1Held);

                if (triggerReleased)
                {
                    // Trigger released → place the spell
                    CastGroundSpell();
                    _prevR3 = r3;
                    _prevL2 = l2Held;
                    _prevR2 = r2Held;
                    return false; // movement resumes
                }

                    // R3 (RS click) while aiming = Cancel without casting
                if (r3 && !_prevR3)
                {
                    CancelGroundAim();
                    _prevR3 = r3;
                    _prevL2 = l2Held;
                    _prevR2 = r2Held;
                    return false;
                }

                // Fine-Aim: R1 held = half cursor speed for precision targeting
                float aimMult = r1Held ? 0.5f : 1.0f;
                MoveAimCursor(rightX, rightY, GroundAimSensitivity * aimMult, GroundAimDeadzone);
                _prevR3 = r3;
                _prevL2 = l2Held;
                _prevR2 = r2Held;
                return true; // suppress left stick
            }

            // ── Casting cooldown ──────────────────────────────────────────────────
            if (Phase == MagePhase.Casting)
            {
                if (_castCooldown <= 0) SetPhase(MagePhase.Idle);
                _prevR3 = r3; _prevL2 = l2Held; _prevR2 = r2Held;
                return false;
            }

            // ── Bolt mode: R2 held without ground-aim ─────────────────────────────
            if (r2Held && !_groundAimFromR2)
            {
                Mode = MageMode.Bolt;

                MoveAimCursor(rightX, rightY, BoltAimSensitivity, 0.15f);

                if (r3 && !_prevR3)
                {
                    InputSimulator.RightClick();
                    SetPhase(MagePhase.BoltLocked);
                }

                if (Phase == MagePhase.BoltLocked)
                    SetPhase(MagePhase.BoltSpamming);

                if (Phase == MagePhase.BoltSpamming && _castCooldown <= 0)
                {
                    InputSimulator.TapKey((VirtualKey)BoltKeyVK);
                    _castCooldown = BoltCastDelayMs;
                    CastCount++;
                }

                _prevR3 = r3; _prevL2 = l2Held; _prevR2 = r2Held;
                return false; // can still walk in bolt mode
            }
            else if (!r2Held && (Phase == MagePhase.BoltSpamming || Phase == MagePhase.BoltLocked))
            {
                // R2 released outside ground-aim → stop bolt
                SetPhase(MagePhase.Idle);
                Mode = MageMode.Idle;
            }

            _prevR3 = r3; _prevL2 = l2Held; _prevR2 = r2Held;
            return false;
        }

        // ── Actions ───────────────────────────────────────────────────────────────

        private void CastGroundSpell()
        {
            // Spell key was already sent by CombatEngine on button press.
            // GetCursor() confirms if RO shows the crosshair:
            //   GroundSpellPending = true  → crosshair visible, LeftClick places it
            //   GroundSpellPending = false → RO may still be reacting; click anyway
            InputSimulator.LeftClick();

            _castCooldown      = 800;
            _groundAimFromL2   = false;
            _groundAimFromR2   = false;
            _groundAimFromL1   = false;
            _groundAimFromR1   = false;
            GroundAimHeld      = false;
            CastCount++;
            SetPhase(MagePhase.Casting);
            Mode = MageMode.Idle;
        }

        private void CancelGroundAim()
        {
            // ESC tells RO to cancel the pending ground placement
            InputSimulator.TapKey(VirtualKey.Escape);
            _groundAimFromL2   = false;
            _groundAimFromR2   = false;
            _groundAimFromL1   = false;
            _groundAimFromR1   = false;
            GroundAimHeld      = false;
            GroundSpellPending = false;
            SetPhase(MagePhase.Idle);
            Mode = MageMode.Idle;
        }

        private void MoveAimCursor(float rx, float ry,
                                   float sensitivity = -1f, float deadzone = -1f)
        {
            if (sensitivity < 0) sensitivity = GroundAimSensitivity;
            if (deadzone    < 0) deadzone    = GroundAimDeadzone;

            float mag = MathF.Sqrt(rx * rx + ry * ry);
            if (mag <= deadzone) return;

            float norm = (mag - deadzone) / (1f - deadzone);
            int mx = (int)(rx / mag * norm * sensitivity);
            int my = (int)(-ry / mag * norm * sensitivity);
            if (mx != 0 || my != 0)
                InputSimulator.MoveMouseRelative(mx, my);
        }

        // ── Cursor Detection ──────────────────────────────────────────────────────

        private void UpdateCursorDetection(int tickMs)
        {
            IntPtr cur = GetCursor();

            if (!_cursorCalibrated)
            {
                _normalCursor     = cur;
                _cursorCalibrated = true;
                GroundSpellPending = false;
                return;
            }

            if (_cursorWaitMs > 0)
            {
                _cursorWaitMs = Math.Max(0, _cursorWaitMs - tickMs);
                return;
            }

            GroundSpellPending = (cur != _normalCursor && cur != IntPtr.Zero);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetPhase(MagePhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }

    public enum MageMode  { Idle, Ground, Bolt }
    public enum MagePhase { Idle, GroundAiming, Casting, BoltLocked, BoltSpamming }
}
