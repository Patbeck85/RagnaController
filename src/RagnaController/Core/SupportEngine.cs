using System;

namespace RagnaController.Core
{
    public class SupportEngine
    {
        public bool SupportEnabled { get; set; } = false;
        public int HealKeyVK { get; set; } = 0x5A;
        public int HealIntervalMs { get; set; } = 80;
        public int SelfHealKeyVK { get; set; } = 0x5A;
        public int RezzKeyVK { get; set; } = 0x58;
        public int SanctuaryKeyVK { get; set; } = 0x43;
        public float GroundAimSensitivity { get; set; } = 18f;
        public float GroundAimDeadzone { get; set; } = 0.15f;
        public float TargetAimSensitivity { get; set; } = 22f;
        public int TabCycleMs { get; set; } = 100;
        public bool AutoCycleEnabled { get; set; } = false;
        public int AutoCycleIntervalMs { get; set; } = 3000;

        public bool IsActive { get; private set; } = false;
        public SupportMode Mode { get; private set; } = SupportMode.Targeting;
        public SupportPhase Phase { get; private set; } = SupportPhase.Idle;
        public int HealCount { get; private set; } = 0;
        public int RezzCount { get; private set; } = 0;

        public string PhaseLabel => Phase switch
        {
            SupportPhase.TargetingParty => "TARGETING PARTY",
            SupportPhase.Healing => "HEALING",
            SupportPhase.SelfHealing => "SELF-HEAL",
            SupportPhase.Rezzing => "RESURRECTION",
            SupportPhase.PlacingSanctuary => "SANCTUARY",
            SupportPhase.AutoCycling => "AUTO-CYCLE",
            _ => "IDLE"
        };

        public event Action<SupportPhase>? PhaseChanged;

        private int _healCooldown = 0;
        private int _tabCooldown = 0;
        private int _autoCycleTimer = 0;
        private bool _prevR3, _prevL3;

        public void ToggleSupportMode()
        {
            IsActive = !IsActive;
            SetPhase(IsActive ? SupportPhase.TargetingParty : SupportPhase.Idle);
            if (!IsActive) { HealCount = 0; RezzCount = 0; }
        }

        public void ResetCounters() { HealCount = 0; RezzCount = 0; }

        public void Update(float rx, float ry, bool r3, bool l3, bool r2, bool rb, int ms)
        {
            if (!IsActive) return;

            Mode = r2 ? SupportMode.GroundTarget : SupportMode.Targeting;
            float sqMag = rx * rx + ry * ry;
            float dz = r2 ? GroundAimDeadzone : 0.15f;
            float sens = r2 ? GroundAimSensitivity : TargetAimSensitivity;

            if (sqMag > dz * dz)
            {
                float mag = MathF.Sqrt(sqMag);
                float norm = (mag - dz) / (1f - dz);
                int mx = (int)(rx / mag * norm * sens);
                int my = (int)(-ry / mag * norm * sens);
                if (mx != 0 || my != 0) InputSimulator.MoveMouseRelative(mx, my);
            }

            _healCooldown = Math.Max(0, _healCooldown - ms);
            _tabCooldown = Math.Max(0, _tabCooldown - ms);
            _autoCycleTimer += ms;

            if (l3 && !_prevL3) { InputSimulator.TapKey((VirtualKey)SelfHealKeyVK); HealCount++; SetPhase(SupportPhase.SelfHealing); }
            _prevL3 = l3;

            if (r3 && !_prevR3)
            {
                if (r2) { InputSimulator.LeftClick(); InputSimulator.TapKey((VirtualKey)SanctuaryKeyVK); SetPhase(SupportPhase.PlacingSanctuary); }
                else { InputSimulator.RightClick(); InputSimulator.TapKey((VirtualKey)HealKeyVK); HealCount++; SetPhase(SupportPhase.Healing); }
            }
            _prevR3 = r3;

            if (rb && _tabCooldown <= 0) { InputSimulator.TapKey(VirtualKey.Tab); _tabCooldown = TabCycleMs; }

            if (AutoCycleEnabled && _autoCycleTimer >= AutoCycleIntervalMs)
            {
                _autoCycleTimer = 0;
                InputSimulator.TapKey(VirtualKey.Tab);
                _tabCooldown = TabCycleMs;
                SetPhase(SupportPhase.AutoCycling);
            }

            if (Phase == SupportPhase.Healing && _healCooldown <= 0) SetPhase(SupportPhase.TargetingParty);
            else if (Phase == SupportPhase.AutoCycling && _tabCooldown <= 0) SetPhase(SupportPhase.TargetingParty);
            // Use _healCooldown as a display timer for instant-action phases so the label
            // remains visible for ~500ms instead of clearing after a single 8ms tick
            else if ((Phase == SupportPhase.SelfHealing || Phase == SupportPhase.Rezzing
                   || Phase == SupportPhase.PlacingSanctuary) && _healCooldown <= 0)
                SetPhase(SupportPhase.TargetingParty);
        }

        public void CastRezz() { InputSimulator.TapKey((VirtualKey)RezzKeyVK); RezzCount++; SetPhase(SupportPhase.Rezzing); }

        private void SetPhase(SupportPhase p)
        {
            Phase = p;
            // Healing has its own 500ms cooldown; instant-action phases get a display timer too
            if (p == SupportPhase.Healing || p == SupportPhase.SelfHealing
             || p == SupportPhase.Rezzing || p == SupportPhase.PlacingSanctuary)
                _healCooldown = 500;
            PhaseChanged?.Invoke(p);
        }
    }

    public enum SupportMode { Targeting, GroundTarget }
    public enum SupportPhase { Idle, TargetingParty, Healing, SelfHealing, Rezzing, PlacingSanctuary, AutoCycling }
}