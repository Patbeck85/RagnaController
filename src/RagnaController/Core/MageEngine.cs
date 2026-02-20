using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Mage Combat Engine — Ground-target spells and Bolt spam for Wizard/Sage in RO.
    ///
    /// ── Two modes ────────────────────────────────────────────────────────────────
    ///
    /// 1. GROUND-TARGET MODE (Storm Gust, Meteor Storm, Lord of Vermillion, etc.)
    ///    - Right stick → move cursor to target location on ground
    ///    - R3 (RS click) → Left-Click ground + instantly fire spell key
    ///    - Result: AoE spell cast at cursor position
    ///
    /// 2. BOLT-SPAM MODE (Fire Bolt, Cold Bolt, Lightning Bolt, etc.)
    ///    - Right stick → aim at monster (move cursor)
    ///    - R3 → Right-click lock + auto-spam bolt key with cast delay
    ///    - Respects cast time (e.g. Fire Bolt Lv10 = ~1.2s cast)
    ///
    /// ── Controller mapping ──────────────────────────────────────────────────────
    ///
    ///   L3 (LS click)       = Toggle Mage mode ON/OFF
    ///   Right Stick         = Cursor positioning (aim / ground target)
    ///   R3 (RS click)       = GROUND: place spell  |  BOLT: lock + spam
    ///   R2 (held)           = Switch to BOLT mode (default is GROUND mode)
    ///   L2 (held)           = Quick-cast defensive (Safety Wall / Ice Wall)
    ///   D-Pad               = Manual skills (utility)
    ///
    /// ── Cast delay handling ─────────────────────────────────────────────────────
    ///
    /// RO has cast times for all bolt spells. We simulate this by spacing key presses:
    ///   - Fire Bolt Lv10:     1200ms cast + 50ms for next
    ///   - Cold Bolt Lv10:     1000ms
    ///   - Lightning Bolt Lv10: 800ms
    ///   - Default:            1000ms
    ///
    /// After casting, we wait CastDelayMs before firing next bolt.
    ///
    /// ── SP Management hint ──────────────────────────────────────────────────────
    ///
    /// No memory read, but we COUNT casts. After X casts → UI hint "Low SP?"
    /// User can manually pot or retreat.
    ///
    /// </summary>
    public class MageEngine
    {
        // ── Config ────────────────────────────────────────────────────────────────
        public bool  MageEnabled           { get; set; } = false;

        // Ground-target mode
        public int   GroundSpellKeyVK      { get; set; } = 0x5A;  // Z = Storm Gust default
        public float GroundAimSensitivity  { get; set; } = 18f;
        public float GroundAimDeadzone     { get; set; } = 0.15f;

        // Bolt-spam mode
        public int   BoltKeyVK             { get; set; } = 0x56;  // V = Fire Bolt default
        public int   BoltCastDelayMs       { get; set; } = 1200;  // Fire Bolt Lv10 cast time
        public float BoltAimSensitivity    { get; set; } = 20f;

        // Defensive quick-cast
        public int   DefensiveKeyVK        { get; set; } = 0x43;  // C = Safety Wall / Ice Wall
        public int   DefensiveCooldownMs   { get; set; } = 800;

        // SP hint
        public int   CastsBeforeSPWarning  { get; set; } = 15;

        // ── State ─────────────────────────────────────────────────────────────────
        public bool       IsActive         { get; private set; } = false;
        public MageMode   Mode             { get; private set; } = MageMode.Ground;
        public MagePhase  Phase            { get; private set; } = MagePhase.Idle;

        public string PhaseLabel => Phase switch
        {
            MagePhase.Aiming        => Mode == MageMode.Ground ? "AIMING GROUND" : "AIMING BOLT",
            MagePhase.Casting       => "CASTING",
            MagePhase.BoltLocked    => "BOLT LOCKED",
            MagePhase.BoltSpamming  => "BOLT SPAMMING",
            _                       => "IDLE"
        };

        public int CastCount { get; private set; } = 0;
        public bool SPWarning => CastCount >= CastsBeforeSPWarning;

        // Events
        public event Action<MagePhase>? PhaseChanged;

        // ── Internal ──────────────────────────────────────────────────────────────
        private int   _castCooldown        = 0;
        private int   _defensiveCooldown   = 0;
        private float _aimX, _aimY;
        private bool  _prevR3              = false;
        private bool  _prevL2              = false;

        // ── Public control ────────────────────────────────────────────────────────

        public void ToggleMageMode()
        {
            IsActive = !IsActive;
            if (IsActive)
                SetPhase(MagePhase.Aiming);
            else
            {
                SetPhase(MagePhase.Idle);
                CastCount = 0;
            }
        }

        public void ResetCastCount() => CastCount = 0;

        // ── Main Update ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call every engine tick (16ms).
        /// rightX/Y  = right stick
        /// r3        = right stick button (place spell / lock target)
        /// r2Held    = R2 trigger (switch to bolt mode)
        /// l2Held    = L2 trigger (quick defensive cast)
        /// tickMs    = tick interval (16)
        /// </summary>
        public void Update(float rightX, float rightY, bool r3, bool r2Held, bool l2Held, int tickMs)
        {
            if (!IsActive) return;

            // Mode switching: R2 held = Bolt mode, released = Ground mode
            Mode = r2Held ? MageMode.Bolt : MageMode.Ground;

            // Process aim input
            float mag = MathF.Sqrt(rightX * rightX + rightY * rightY);
            float deadzone = Mode == MageMode.Ground ? GroundAimDeadzone : 0.15f;
            float sens     = Mode == MageMode.Ground ? GroundAimSensitivity : BoltAimSensitivity;

            if (mag > deadzone)
            {
                float norm = (mag - deadzone) / (1f - deadzone);
                _aimX = rightX / mag * norm;
                _aimY = rightY / mag * norm;
            }
            else
            {
                _aimX = 0f;
                _aimY = 0f;
            }

            // Move cursor based on aim
            if (_aimX != 0 || _aimY != 0)
            {
                int mx = (int)(_aimX * sens);
                int my = (int)(-_aimY * sens); // Y inverted
                if (mx != 0 || my != 0)
                    InputSimulator.MoveMouseRelative(mx, my);
            }

            // Tick cooldowns
            _castCooldown      = Math.Max(0, _castCooldown - tickMs);
            _defensiveCooldown = Math.Max(0, _defensiveCooldown - tickMs);

            // ── L2: Quick defensive cast ─────────────────────────────────────────
            if (l2Held && !_prevL2 && _defensiveCooldown <= 0)
            {
                QuickDefensiveCast();
            }
            _prevL2 = l2Held;

            // ── R3: Spell placement / bolt lock ──────────────────────────────────
            if (r3 && !_prevR3)
            {
                if (Mode == MageMode.Ground)
                    PlaceGroundSpell();
                else
                    LockAndBolt();
            }
            _prevR3 = r3;

            // ── State machine ────────────────────────────────────────────────────
            switch (Phase)
            {
                case MagePhase.Aiming:
                    // Just aiming, waiting for R3
                    break;

                case MagePhase.Casting:
                    // Single ground cast animation wait
                    if (_castCooldown <= 0)
                        SetPhase(MagePhase.Aiming);
                    break;

                case MagePhase.BoltLocked:
                    // Locked on target, entering spam phase
                    SetPhase(MagePhase.BoltSpamming);
                    break;

                case MagePhase.BoltSpamming:
                    UpdateBoltSpam(tickMs);
                    break;
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────────

        private void PlaceGroundSpell()
        {
            // Left-click ground at cursor, then immediately fire spell key
            InputSimulator.LeftClick();
            System.Threading.Thread.Sleep(20); // Tiny delay for RO to register click
            InputSimulator.TapKey((VirtualKey)GroundSpellKeyVK);

            _castCooldown = 600; // Brief animation lock
            CastCount++;
            SetPhase(MagePhase.Casting);
        }

        private void LockAndBolt()
        {
            // Right-click to lock monster, then enter bolt spam
            InputSimulator.RightClick();
            SetPhase(MagePhase.BoltLocked);
        }

        private void UpdateBoltSpam(int tickMs)
        {
            // Auto-fire bolt key with cast delay
            if (_castCooldown <= 0)
            {
                InputSimulator.TapKey((VirtualKey)BoltKeyVK);
                _castCooldown = BoltCastDelayMs;
                CastCount++;
            }
        }

        private void QuickDefensiveCast()
        {
            // Fire defensive skill (Safety Wall on self, Ice Wall behind, etc.)
            // User configures which key this is
            InputSimulator.TapKey((VirtualKey)DefensiveKeyVK);
            _defensiveCooldown = DefensiveCooldownMs;
            CastCount++;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetPhase(MagePhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────────

    public enum MageMode
    {
        Ground,  // Ground-target AoE placement
        Bolt     // Bolt spam on locked target
    }

    public enum MagePhase
    {
        Idle,
        Aiming,        // Moving cursor to target
        Casting,       // Cast animation for ground spell
        BoltLocked,    // Just locked target for bolt
        BoltSpamming   // Auto-firing bolts
    }
}
