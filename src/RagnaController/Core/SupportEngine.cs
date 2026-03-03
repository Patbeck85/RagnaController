using System;
using RagnaController.Core;

namespace RagnaController.Core
{
    /// <summary>
    /// Support Combat Engine — Party healing, buffs, rezz, and target cycling.
    /// Fully cleaned version without unused variables.
    /// </summary>
    public class SupportEngine
    {
        // ── Config ────────────────────────────────────────────────────────────────
        public bool  SupportEnabled           { get; set; } = false;
        public int   HealKeyVK                { get; set; } = 0x5A;  // Z
        public int   HealIntervalMs           { get; set; } = 80;
        public int   SelfHealKeyVK            { get; set; } = 0x5A;  // Z
        public int   RezzKeyVK                { get; set; } = 0x58;  // X
        public int   SanctuaryKeyVK           { get; set; } = 0x43;  // C
        public float GroundAimSensitivity     { get; set; } = 18f;
        public float GroundAimDeadzone        { get; set; } = 0.15f;
        public float TargetAimSensitivity     { get; set; } = 22f;
        public int   TabCycleMs               { get; set; } = 100;
        public bool  AutoCycleEnabled         { get; set; } = false;
        public int   AutoCycleIntervalMs      { get; set; } = 3000;

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

        public event Action<SupportPhase>? PhaseChanged;

        // ── Internal ──────────────────────────────────────────────────────────────
        private int   _healCooldown       = 0;
        private int   _tabCooldown        = 0;
        private int   _autoCycleTimer     = 0;
        private float _aimX               = 0f;
        private float _aimY               = 0f;
        private bool  _prevR3             = false;
        private bool  _prevL3             = false;

        // REMOVED: _hasTarget was unused (would cause warning CS0414)

        public void ToggleSupportMode()
        {
            IsActive = !IsActive;
            SetPhase(IsActive ? SupportPhase.TargetingParty : SupportPhase.Idle);
            if (!IsActive) { HealCount = 0; RezzCount = 0; }
        }

        public void ResetCounters()
        {
            HealCount = 0;
            RezzCount = 0;
        }

        public void Update(float rightX, float rightY, bool r3, bool l3, 
                           bool r2Held, bool rbPressed, int tickMs)
        {
            if (!IsActive) return;

            Mode = r2Held ? SupportMode.GroundTarget : SupportMode.Targeting;

            float mag = MathF.Sqrt(rightX * rightX + rightY * rightY);
            float deadzone = Mode == SupportMode.GroundTarget ? GroundAimDeadzone : 0.15f;
            float sens     = Mode == SupportMode.GroundTarget ? GroundAimSensitivity : TargetAimSensitivity;

            if (mag > deadzone)
            {
                float norm = (mag - deadzone) / (1f - deadzone);
                _aimX = rightX / mag * norm;
                _aimY = rightY / mag * norm;
            }
            else { _aimX = 0f; _aimY = 0f; }

            if (_aimX != 0 || _aimY != 0)
            {
                int mx = (int)(_aimX * sens);
                int my = (int)(-_aimY * sens);
                if (mx != 0 || my != 0)
                    InputSimulator.MoveMouseRelative(mx, my);
            }

            _healCooldown     = Math.Max(0, _healCooldown - tickMs);
            _tabCooldown      = Math.Max(0, _tabCooldown - tickMs);
            _autoCycleTimer  += tickMs;

            if (l3 && !_prevL3) SelfHeal();
            _prevL3 = l3;

            if (r3 && !_prevR3)
            {
                if (Mode == SupportMode.GroundTarget) PlaceSanctuary();
                else SnapTargetAndHeal();
            }
            _prevR3 = r3;

            if (rbPressed && _tabCooldown <= 0) TabCycleParty();

            if (AutoCycleEnabled && _autoCycleTimer >= AutoCycleIntervalMs)
            {
                _autoCycleTimer = 0;
                TabCycleParty();
                SetPhase(SupportPhase.AutoCycling);
            }

            switch (Phase)
            {
                case SupportPhase.Healing: UpdateHealing(tickMs); break;
                // Transient phases: visible for 1 tick (~8ms), then return to Targeting
                case SupportPhase.SelfHealing:
                case SupportPhase.Rezzing:
                case SupportPhase.PlacingSanctuary:
                    SetPhase(SupportPhase.TargetingParty);
                    break;
                // AutoCycling: stays visible for TabCycleMs (100ms) so UI shows "AUTO-CYCLE"
                case SupportPhase.AutoCycling:
                    if (_tabCooldown <= 0)
                        SetPhase(SupportPhase.TargetingParty);
                    break;
            }
        }

        private void SnapTargetAndHeal()
        {
            InputSimulator.RightClick();
            InputSimulator.TapKey((VirtualKey)HealKeyVK);
            HealCount++;
            SetPhase(SupportPhase.Healing);
        }

        private void SelfHeal()
        {
            InputSimulator.TapKey((VirtualKey)SelfHealKeyVK);
            HealCount++;
            SetPhase(SupportPhase.SelfHealing);
        }

        private void UpdateHealing(int tickMs)
        {
            if (_healCooldown <= 0) SetPhase(SupportPhase.TargetingParty);
        }

        private void TabCycleParty()
        {
            InputSimulator.TapKey(VirtualKey.Tab);
            _tabCooldown = TabCycleMs;
        }

        private void PlaceSanctuary()
        {
            InputSimulator.LeftClick();
            InputSimulator.TapKey((VirtualKey)SanctuaryKeyVK);
            SetPhase(SupportPhase.PlacingSanctuary);
        }

        public void CastRezz()
        {
            InputSimulator.TapKey((VirtualKey)RezzKeyVK);
            RezzCount++;
            SetPhase(SupportPhase.Rezzing);
        }

        private void SetPhase(SupportPhase phase)
        {
            Phase = phase;
            if (phase == SupportPhase.Healing) _healCooldown = 500;
            PhaseChanged?.Invoke(phase);
        }
    }

    public enum SupportMode { Targeting, GroundTarget }
    public enum SupportPhase { Idle, TargetingParty, Healing, SelfHealing, Rezzing, PlacingSanctuary, AutoCycling }
}