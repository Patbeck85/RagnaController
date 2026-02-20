using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Support Combat Engine — Party healing, buffs, rezz, and target cycling for Priest/Monk.
    ///
    /// ── Core Gameplay Loop ───────────────────────────────────────────────────────
    ///
    /// 1. TARGET CYCLING
    ///    - Tab = cycle to next party member (RO built-in)
    ///    - Right stick = move cursor to party member on screen
    ///    - R3 = snap-target party member under cursor + instant heal
    ///
    /// 2. HEALING
    ///    - A button (turbo) = Heal spam on current target
    ///    - L3 = SELF-HEAL (emergency, instant)
    ///    - R3 = Target + instant heal combo
    ///
    /// 3. GROUND-TARGET (Sanctuary)
    ///    - R2 held = Ground-target mode
    ///    - Right stick = move cursor to ground
    ///    - R3 = Place Sanctuary at cursor
    ///
    /// 4. BUFFS & UTILITY
    ///    - L2 held = Buff mode (D-Pad = buffs on current target)
    ///    - B button = Resurrection
    ///    - X/Y = Utility (Cure, Dispell, etc.)
    ///
    /// 5. AUTO-FEATURES (optional)
    ///    - Auto-cycle through party every N seconds (heal check sweep)
    ///    - Emergency self-heal when no target (failsafe)
    ///
    /// ── Controller Layout ────────────────────────────────────────────────────────
    ///
    ///   Left Stick          = Movement (normal)
    ///   Right Stick         = Cursor aim (party target / ground)
    ///   L3 (LS-Click)       = SELF-HEAL (emergency)
    ///   R3 (RS-Click)       = Snap-target + instant heal  OR  place Sanctuary
    ///   R2 (held)           = Ground-target mode (Sanctuary)
    ///   L2 (held)           = Buff mode (D-Pad becomes buffs)
    ///   A (turbo)           = Heal spam
    ///   B                   = Resurrection
    ///   X                   = Cure
    ///   Y                   = Dispell / Utility
    ///   D-Pad (normal)      = Items / utility
    ///   D-Pad (L2 held)     = Buffs (Blessing, Agi, Assumptio, etc.)
    ///   RB                  = Tab (cycle next party member)
    ///
    /// </summary>
    public class SupportEngine
    {
        // ── Config ────────────────────────────────────────────────────────────────
        public bool  SupportEnabled           { get; set; } = false;

        // Healing
        public int   HealKeyVK                { get; set; } = 0x5A;  // Z = Heal
        public int   HealIntervalMs           { get; set; } = 80;    // Heal spam interval
        public int   SelfHealKeyVK            { get; set; } = 0x5A;  // Z = Self-Heal (same key, different target logic)
        
        // Resurrection
        public int   RezzKeyVK                { get; set; } = 0x58;  // X = Resurrection
        
        // Sanctuary (ground-target)
        public int   SanctuaryKeyVK           { get; set; } = 0x43;  // C = Sanctuary
        public float GroundAimSensitivity     { get; set; } = 18f;
        public float GroundAimDeadzone        { get; set; } = 0.15f;

        // Target cycling
        public float TargetAimSensitivity     { get; set; } = 22f;
        public int   TabCycleMs               { get; set; } = 100;   // Tab press interval
        public bool  AutoCycleEnabled         { get; set; } = false; // Auto-sweep party
        public int   AutoCycleIntervalMs      { get; set; } = 3000;  // Every 3s

        // ── State ─────────────────────────────────────────────────────────────────
        public bool          IsActive         { get; private set; } = false;
        public SupportMode   Mode             { get; private set; } = SupportMode.Targeting;
        public SupportPhase  Phase            { get; private set; } = SupportPhase.Idle;

        public string PhaseLabel => Phase switch
        {
            SupportPhase.TargetingParty  => "TARGETING PARTY",
            SupportPhase.Healing         => "HEALING",
            SupportPhase.SelfHealing     => "SELF-HEAL",
            SupportPhase.Rezzing         => "RESURRECTION",
            SupportPhase.PlacingSanctuary => "SANCTUARY",
            SupportPhase.AutoCycling     => "AUTO-CYCLE",
            _                            => "IDLE"
        };

        public int HealCount { get; private set; } = 0;
        public int RezzCount { get; private set; } = 0;

        // Events
        public event Action<SupportPhase>? PhaseChanged;

        // ── Internal ──────────────────────────────────────────────────────────────
        private int   _healCooldown       = 0;
        private int   _tabCooldown        = 0;
        private int   _autoCycleTimer     = 0;
        private float _aimX, _aimY;
        private bool  _prevR3             = false;
        private bool  _prevL3             = false;
        private bool  _hasTarget          = false;

        // ── Public control ────────────────────────────────────────────────────────

        public void ToggleSupportMode()
        {
            IsActive = !IsActive;
            if (IsActive)
                SetPhase(SupportPhase.TargetingParty);
            else
            {
                SetPhase(SupportPhase.Idle);
                HealCount = 0;
                RezzCount = 0;
            }
        }

        public void ResetCounters()
        {
            HealCount = 0;
            RezzCount = 0;
        }

        // ── Main Update ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call every engine tick (16ms).
        /// rightX/Y  = right stick
        /// r3        = right stick button (snap-target + heal / place sanctuary)
        /// l3        = left stick button (self-heal emergency)
        /// r2Held    = R2 trigger (ground-target mode for Sanctuary)
        /// l2Held    = L2 trigger (buff mode, handled in combat mappings)
        /// rbPressed = RB shoulder (Tab = cycle next party member)
        /// tickMs    = tick interval (16)
        /// </summary>
        public void Update(float rightX, float rightY, bool r3, bool l3, 
                           bool r2Held, bool rbPressed, int tickMs)
        {
            if (!IsActive) return;

            // Mode switching: R2 held = Ground-target (Sanctuary), else = Party targeting
            Mode = r2Held ? SupportMode.GroundTarget : SupportMode.Targeting;

            // Process aim input
            float mag = MathF.Sqrt(rightX * rightX + rightY * rightY);
            float deadzone = Mode == SupportMode.GroundTarget ? GroundAimDeadzone : 0.15f;
            float sens     = Mode == SupportMode.GroundTarget ? GroundAimSensitivity : TargetAimSensitivity;

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
                int my = (int)(-_aimY * sens);
                if (mx != 0 || my != 0)
                    InputSimulator.MoveMouseRelative(mx, my);
            }

            // Tick cooldowns
            _healCooldown     = Math.Max(0, _healCooldown - tickMs);
            _tabCooldown      = Math.Max(0, _tabCooldown - tickMs);
            _autoCycleTimer  += tickMs;

            // ── L3: Self-Heal (emergency) ────────────────────────────────────────
            if (l3 && !_prevL3)
            {
                SelfHeal();
            }
            _prevL3 = l3;

            // ── R3: Snap-target + heal OR place Sanctuary ────────────────────────
            if (r3 && !_prevR3)
            {
                if (Mode == SupportMode.GroundTarget)
                    PlaceSanctuary();
                else
                    SnapTargetAndHeal();
            }
            _prevR3 = r3;

            // ── RB: Tab cycle next party member ──────────────────────────────────
            if (rbPressed && _tabCooldown <= 0)
            {
                TabCycleParty();
            }

            // ── Auto-cycle sweep ─────────────────────────────────────────────────
            if (AutoCycleEnabled && _autoCycleTimer >= AutoCycleIntervalMs)
            {
                _autoCycleTimer = 0;
                TabCycleParty();
                SetPhase(SupportPhase.AutoCycling);
            }

            // ── State machine ────────────────────────────────────────────────────
            switch (Phase)
            {
                case SupportPhase.TargetingParty:
                    // Just aiming, waiting for action
                    break;

                case SupportPhase.Healing:
                    UpdateHealing(tickMs);
                    break;

                case SupportPhase.SelfHealing:
                    // Single-shot self-heal, return to targeting
                    SetPhase(SupportPhase.TargetingParty);
                    break;

                case SupportPhase.Rezzing:
                    // Single resurrection cast, return to targeting
                    SetPhase(SupportPhase.TargetingParty);
                    break;

                case SupportPhase.PlacingSanctuary:
                    // Single placement, return to targeting
                    SetPhase(SupportPhase.TargetingParty);
                    break;

                case SupportPhase.AutoCycling:
                    // Brief indicator, then back to targeting
                    SetPhase(SupportPhase.TargetingParty);
                    break;
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────────

        private void SnapTargetAndHeal()
        {
            // Right-click to target party member under cursor
            InputSimulator.RightClick();
            _hasTarget = true;

            // Instant heal on target
            System.Threading.Thread.Sleep(30); // Brief delay for RO to register target
            InputSimulator.TapKey((VirtualKey)HealKeyVK);

            HealCount++;
            SetPhase(SupportPhase.Healing);
        }

        private void SelfHeal()
        {
            // Target self (Shift+Left-Click in RO, or just press Heal with no target)
            // Assuming no-target = self-heal, or use /hi macro
            InputSimulator.TapKey((VirtualKey)SelfHealKeyVK);

            HealCount++;
            SetPhase(SupportPhase.SelfHealing);
        }

        private void UpdateHealing(int tickMs)
        {
            // Continuous heal spam on current target (if A button held via turbo)
            // This is handled by CombatEngine turbo, but we track state
            // Return to targeting after brief period
            if (_healCooldown <= 0)
            {
                SetPhase(SupportPhase.TargetingParty);
            }
        }

        private void TabCycleParty()
        {
            // Press Tab to cycle to next party member (RO built-in)
            InputSimulator.TapKey(VirtualKey.Tab);
            _tabCooldown = TabCycleMs;
            _hasTarget   = true;
        }

        private void PlaceSanctuary()
        {
            // Left-click ground at cursor, then fire Sanctuary key
            InputSimulator.LeftClick();
            System.Threading.Thread.Sleep(20);
            InputSimulator.TapKey((VirtualKey)SanctuaryKeyVK);

            SetPhase(SupportPhase.PlacingSanctuary);
        }

        public void CastRezz()
        {
            // Called from button mapping (B button)
            InputSimulator.TapKey((VirtualKey)RezzKeyVK);
            RezzCount++;
            SetPhase(SupportPhase.Rezzing);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetPhase(SupportPhase phase)
        {
            Phase = phase;

            // Set cooldowns based on phase
            if (phase == SupportPhase.Healing)
                _healCooldown = 500; // Brief heal animation lock

            PhaseChanged?.Invoke(phase);
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────────

    public enum SupportMode
    {
        Targeting,     // Aiming at party members
        GroundTarget   // Placing Sanctuary on ground
    }

    public enum SupportPhase
    {
        Idle,
        TargetingParty,   // Moving cursor to party member
        Healing,          // Casting heal on target
        SelfHealing,      // Emergency self-heal
        Rezzing,          // Resurrection cast
        PlacingSanctuary, // Sanctuary ground placement
        AutoCycling       // Auto-sweep through party
    }
}
