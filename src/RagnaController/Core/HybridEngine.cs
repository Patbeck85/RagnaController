using System;
using System.Windows.Threading;
using RagnaController.Controller;
using RagnaController.Profiles;
using SharpDX.XInput;

namespace RagnaController.Core
{
    /// <summary>
    /// Central orchestrator. 60 Hz tick loop that reads the controller,
    /// feeds movement / combat / auto-target engines and fires input events.
    /// </summary>
    public class HybridEngine
    {
        // ── Sub-engines ───────────────────────────────────────────────────────────
        private readonly MovementEngine    _movement   = new();
        private readonly CombatEngine      _combat     = new();
        private readonly AutoTargetEngine  _autoTarget = new();
        private readonly KiteEngine        _kite       = new();
        private readonly MageEngine        _mage       = new();
        private readonly SupportEngine     _support    = new();
        private readonly ControllerService _ctrl       = new();

        // Expose for UI and feedback
        public AutoTargetEngine AutoTarget => _autoTarget;
        public KiteEngine       Kite       => _kite;
        public MageEngine       Mage       => _mage;
        public SupportEngine    Support    => _support;
        public ControllerService Controller => _ctrl;

        // ── Loop ──────────────────────────────────────────────────────────────────
        private readonly DispatcherTimer _timer;
        private const int TickMs = 16;

        // ── Previous frame state ──────────────────────────────────────────────────
        private GamepadButtonFlags _prevButtons;
        private bool _prevL2;
        private bool _prevR2;
        private bool _prevR3;          // Right stick click — for snap-target

        // ── Public state ──────────────────────────────────────────────────────────
        public bool   IsRunning  { get; private set; }
        public string StatusText { get; private set; } = "Stopped";

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<EngineStatus>?     StatusChanged;
        public event Action<ControllerSnapshot>? SnapshotUpdated;

        // ── Constructor ───────────────────────────────────────────────────────────
        public HybridEngine()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
            _timer.Tick += OnTick;

            // Wire auto-target state changes to engine status update
            _autoTarget.StateChanged += _ => StatusChanged?.Invoke(
                IsRunning ? EngineStatus.Running : EngineStatus.Stopped);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            if (!_ctrl.IsConnected)
            {
                SetStatus(EngineStatus.NoController, "No controller found");
                return;
            }
            _movement.Reset();
            _prevButtons = 0;
            _prevR3      = false;
            IsRunning    = true;
            _timer.Start();
            SetStatus(EngineStatus.Running, "Running");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _timer.Stop();
            IsRunning = false;
            _autoTarget.ToggleCombatMode(); // Force idle if was active
            SetStatus(EngineStatus.Stopped, "Stopped");
        }

        public void LoadProfile(Profile profile)
        {
            _movement.Sensitivity   = profile.MouseSensitivity;
            _movement.Deadzone      = profile.Deadzone;
            _movement.Curve         = profile.MovementCurve;
            _movement.ActionRpgMode = profile.ActionRpgMode;
            _movement.ActionSpeed   = profile.ActionSpeed;

            _combat.LoadProfile(profile);

            // Auto-target settings
            _autoTarget.AutoAttackEnabled   = profile.AutoAttackEnabled;
            _autoTarget.AutoRetargetEnabled = profile.AutoRetargetEnabled;
            _autoTarget.AttackKey_VK        = profile.AutoAttackKeyVK;
            _autoTarget.AttackIntervalMs    = profile.AutoAttackIntervalMs;
            _autoTarget.TabCycleMs          = profile.TabCycleMs;
            _autoTarget.AimSensitivity      = profile.AimSensitivity;
            _autoTarget.AimDeadzone         = profile.AimDeadzone;

            // Kite engine settings
            _kite.KiteEnabled           = profile.KiteEnabled;
            _kite.AttackKeyVK           = profile.KiteAttackKeyVK;
            _kite.AttackIntervalMs      = profile.KiteAttackIntervalMs;
            _kite.AttacksBeforeRetreat  = profile.KiteAttacksBeforeRetreat;
            _kite.RetreatDurationMs     = profile.KiteRetreatDurationMs;
            _kite.PivotDurationMs       = profile.KitePivotDurationMs;
            _kite.RetreatCursorDist     = profile.KiteRetreatCursorDist;
            _kite.AimSensitivity        = profile.KiteAimSensitivity;

            // Mage engine settings
            _mage.MageEnabled              = profile.MageEnabled;
            _mage.GroundSpellKeyVK         = profile.MageGroundSpellKeyVK;
            _mage.GroundAimSensitivity     = profile.MageGroundAimSensitivity;
            _mage.GroundAimDeadzone        = profile.MageGroundAimDeadzone;
            _mage.BoltKeyVK                = profile.MageBoltKeyVK;
            _mage.BoltCastDelayMs          = profile.MageBoltCastDelayMs;
            _mage.BoltAimSensitivity       = profile.MageBoltAimSensitivity;
            _mage.DefensiveKeyVK           = profile.MageDefensiveKeyVK;
            _mage.DefensiveCooldownMs      = profile.MageDefensiveCooldownMs;
            _mage.CastsBeforeSPWarning     = profile.MageCastsBeforeSPWarning;

            // Support engine settings
            _support.SupportEnabled             = profile.SupportEnabled;
            _support.HealKeyVK                  = profile.SupportHealKeyVK;
            _support.HealIntervalMs             = profile.SupportHealIntervalMs;
            _support.SelfHealKeyVK              = profile.SupportSelfHealKeyVK;
            _support.RezzKeyVK                  = profile.SupportRezzKeyVK;
            _support.SanctuaryKeyVK             = profile.SupportSanctuaryKeyVK;
            _support.GroundAimSensitivity       = profile.SupportGroundAimSensitivity;
            _support.GroundAimDeadzone          = profile.SupportGroundAimDeadzone;
            _support.TargetAimSensitivity       = profile.SupportTargetAimSensitivity;
            _support.TabCycleMs                 = profile.SupportTabCycleMs;
            _support.AutoCycleEnabled           = profile.SupportAutoCycleEnabled;
            _support.AutoCycleIntervalMs        = profile.SupportAutoCycleIntervalMs;
        }

        // ── Tick ──────────────────────────────────────────────────────────────────

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_ctrl.IsConnected)
            {
                Stop();
                SetStatus(EngineStatus.NoController, "Controller disconnected");
                return;
            }

            var pad = _ctrl.GetGamepad()!.Value;

            // ── Layer triggers ───────────────────────────────────────────────────
            bool l2 = pad.LeftTrigger  > 50;
            bool r2 = pad.RightTrigger > 50;
            _combat.UpdateLayers(l2, r2);

            // ── Left stick: movement ─────────────────────────────────────────────
            float stickX = NormalizeAxis(pad.LeftThumbX);
            float stickY = NormalizeAxis(pad.LeftThumbY);
            _movement.Update(stickX, stickY);

            // ── Right stick: support / mage / kite / auto-target / free-look ─────
            float camX   = NormalizeAxis(pad.RightThumbX);
            float camY   = NormalizeAxis(pad.RightThumbY);
            bool  r3Now  = pad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
            bool  rbHeld = pad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
            bool  l3Now  = pad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
            bool  l3Was  = _prevButtons.HasFlag(GamepadButtonFlags.LeftThumb);

            if (_support.SupportEnabled || _support.IsActive)
            {
                // ── SUPPORT: Party heal / buff / rezz ────────────────────────────
                bool r2Held = pad.RightTrigger > 50; // R2 = ground-target (Sanctuary)
                bool rbPressed = rbHeld && !_prevButtons.HasFlag(GamepadButtonFlags.RightShoulder);

                _support.Update(camX, camY, r3Now, l3Now, r2Held, rbPressed, TickMs);

                // L3 = toggle support mode on/off
                if (l3Now && !l3Was) _support.ToggleSupportMode();
            }
            else if (_mage.MageEnabled || _mage.IsActive)
            {
                // ── MAGE: Ground-target or Bolt spam ─────────────────────────────
                bool r2Held = pad.RightTrigger > 50; // R2 = switch to bolt mode
                bool l2Held = l2;                    // L2 = quick defensive cast

                _mage.Update(camX, camY, r3Now, r2Held, l2Held, TickMs);

                // L3 = toggle mage mode on/off
                if (l3Now && !l3Was) _mage.ToggleMageMode();
            }
            else if (_kite.KiteEnabled || _kite.IsActive)
            {
                // ── RANGED: Kite engine takes right stick + special buttons ────────
                bool r2Held        = pad.RightTrigger > 50; // R2 = hold ground / keep firing
                bool manualRetreat = l2;                    // L2 = force retreat NOW

                _kite.Update(camX, camY, r3Now, r2Held, manualRetreat, TickMs);

                // L3 = toggle kite mode on/off
                if (l3Now && !l3Was) _kite.ToggleKiteMode();
            }
            else if (_autoTarget.AutoAttackEnabled || _autoTarget.State != CombatState.Idle)
            {
                // ── MELEE: Auto-target engine ─────────────────────────────────────
                _autoTarget.Update(camX, camY, r3Now, _prevR3, rbHeld, TickMs);

                // L3 = toggle combat mode on/off
                if (l3Now && !l3Was && _autoTarget.AutoAttackEnabled)
                    _autoTarget.ToggleCombatMode();
            }
            else
            {
                // ── FREE: camera / cursor nudge ───────────────────────────────────
                float camMag = MathF.Sqrt(camX * camX + camY * camY);
                if (camMag > 0.15f)
                    InputSimulator.MoveMouseRelative((int)(camX * 8), (int)(-camY * 8));
            }

            // ── Button events (combat layer mappings) ─────────────────────────────
            ProcessButtons(pad.Buttons);

            // ── UI snapshot ───────────────────────────────────────────────────────
            string stateLabel;
            CombatState combatState;
            KitePhase kitePhase       = KitePhase.Idle;
            MagePhase magePhase       = MagePhase.Idle;
            SupportPhase supportPhase = SupportPhase.Idle;

            if (_support.IsActive)
            {
                stateLabel    = _support.PhaseLabel;
                combatState   = CombatState.Attacking; // Generic "active"
                supportPhase  = _support.Phase;
            }
            else if (_mage.IsActive)
            {
                stateLabel   = _mage.PhaseLabel;
                combatState  = CombatState.Attacking;
                magePhase    = _mage.Phase;
            }
            else if (_kite.IsActive)
            {
                stateLabel   = _kite.PhaseLabel;
                combatState  = CombatState.Attacking;
                kitePhase    = _kite.Phase;
            }
            else
            {
                stateLabel   = _autoTarget.StateLabel;
                combatState  = _autoTarget.State;
            }

            SnapshotUpdated?.Invoke(new ControllerSnapshot
            {
                LeftX       = stickX,
                LeftY       = stickY,
                RightX      = camX,
                RightY      = camY,
                L2          = l2,
                R2          = r2,
                Buttons     = pad.Buttons,
                LayerText   = l2 ? "L2 Layer" : r2 ? "R2 Layer" : "Base Layer",
                CombatState = combatState,
                StateLabel  = stateLabel,
                KitePhase   = kitePhase,
                MagePhase   = magePhase,
                MageSPWarning = _mage.SPWarning,
                MageCastCount = _mage.CastCount,
                SupportPhase    = supportPhase,
                SupportHealCount = _support.HealCount,
                SupportRezzCount = _support.RezzCount,
                ControllerName = _ctrl.ControllerName,
                ControllerType = _ctrl.ControllerType,
            });

            _prevButtons = pad.Buttons;
            _prevL2      = l2;
            _prevR2      = r2;
            _prevR3      = r3Now;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void ProcessButtons(GamepadButtonFlags current)
        {
            foreach (GamepadButtonFlags flag in Enum.GetValues<GamepadButtonFlags>())
            {
                if (flag == 0) continue;
                bool nowPressed = current.HasFlag(flag);
                bool wasPressed = _prevButtons.HasFlag(flag);
                if (nowPressed != wasPressed || nowPressed)
                    _combat.ProcessButton(flag.ToString(), nowPressed, TickMs);
            }
        }

        private static float NormalizeAxis(short raw)
            => raw >= 0 ? raw / 32767f : raw / 32768f;

        private void SetStatus(EngineStatus status, string text)
        {
            StatusText = text;
            StatusChanged?.Invoke(status);
        }
    }

    // ── Supporting types ──────────────────────────────────────────────────────────

    public enum EngineStatus { Stopped, Running, NoController }

    public class ControllerSnapshot
    {
        public float LeftX  { get; init; }
        public float LeftY  { get; init; }
        public float RightX { get; init; }
        public float RightY { get; init; }
        public bool  L2     { get; init; }
        public bool  R2     { get; init; }
        public GamepadButtonFlags Buttons     { get; init; }
        public string             LayerText   { get; init; } = "Base Layer";
        public CombatState        CombatState { get; init; } = CombatState.Idle;
        public string             StateLabel  { get; init; } = "IDLE";
        public KitePhase          KitePhase   { get; init; } = KitePhase.Idle;
        public MagePhase          MagePhase   { get; init; } = MagePhase.Idle;
        public bool               MageSPWarning { get; init; } = false;
        public int                MageCastCount { get; init; } = 0;
        public SupportPhase       SupportPhase  { get; init; } = SupportPhase.Idle;
        public int                SupportHealCount { get; init; } = 0;
        public int                SupportRezzCount { get; init; } = 0;
        public string             ControllerName { get; init; } = "Unknown";
        public string             ControllerType { get; init; } = "Unknown";
    }
}
