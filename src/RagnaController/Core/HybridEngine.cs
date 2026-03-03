using System;
using System.Windows.Threading;
using RagnaController.Controller;
using RagnaController.Profiles;
using SharpDX.XInput;

namespace RagnaController.Core
{
    public class HybridEngine
    {
        // ── Sub-engines ───────────────────────────────────────────────────────────
        private readonly MovementEngine    _movement   = new();
        private readonly CursorEngine      _cursor     = new();
        private readonly CombatEngine      _combat     = new();
        private readonly AutoTargetEngine  _autoTarget = new();
        private readonly KiteEngine        _kite       = new();
        private readonly MageEngine        _mage       = new();
        private readonly SupportEngine     _support    = new();
        private readonly ControllerService _ctrl       = new();
        private FeedbackSystem _feedback = null!; // initialized in constructor
        private Profiles.Profile? _currentProfile; // für MobSweep-Parameter
        private readonly AdvancedLogger _advLogger = new();

        public AutoTargetEngine  AutoTarget  => _autoTarget;
        public KiteEngine        Kite        => _kite;
        public MageEngine        Mage        => _mage;
        public SupportEngine     Support     => _support;
        public ControllerService Controller  => _ctrl;
        public CursorEngine      Cursor      => _cursor;

        // ── Loop ──────────────────────────────────────────────────────────────────
        private readonly DispatcherTimer _timer;
        private const int TickMs = 8;  // 125fps for precise input timing

        // ── Previous frame state ──────────────────────────────────────────────────
        private GamepadButtonFlags _prevButtons;
        private bool _altHeld = false;
        // Current modifier states (available for ProcessButtons)
        private bool _curL1, _curR1, _curL2, _curR2;
        private bool _precisionMode = false;

        // ── Mob-Sweep State ───────────────────────────────────────────────────────
        private int  _sweepTabTimer    = 0;   // ms bis zum nächsten TAB-Press
        private int  _sweepAttackTimer = -1;  // ms bis zum Angriff nach TAB (-1 = kein pending)
        private bool _sweepWasActive   = false;

        public bool   IsRunning  { get; private set; }
        public string StatusText { get; private set; } = "Stopped";

        public event Action<EngineStatus>?       StatusChanged;
        public event Action<ControllerSnapshot>? SnapshotUpdated;
        public event Action<int>?                 ProfileQuickSwitch;  // +1 = next, -1 = prev
        public event Action<string>?              BatteryChanged;

        public HybridEngine()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
            _timer.Tick += OnTick;
            _autoTarget.StateChanged += _ => StatusChanged?.Invoke(IsRunning ? EngineStatus.Running : EngineStatus.Stopped);

            // Initialize feedback system
            _feedback = new FeedbackSystem(_ctrl);

            // Skill fired → short rumble on right motor
            _combat.ActionFired += _ => _feedback.TriggerSkillFired();
        }

        public void Start()
        {
            if (IsRunning) return;
            _ctrl.DetectController();

            if (!_ctrl.IsConnected)
            {
                SetStatus(EngineStatus.NoController, "No Xbox controller found");
                return;
            }

            _movement.Reset();
            _cursor.Reset();
            _prevButtons = 0;
            _altHeld     = false;
            IsRunning    = true;
            _timer.Start();
            Log("Engine started – controller: " + _ctrl.ControllerType, LogLevel.Info, "Engine");
            SetStatus(EngineStatus.Running, "Running");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _timer.Stop();
            IsRunning = false;
            _feedback.StopRumble();
            // Release Alt if still held
            if (_altHeld)
            {
                InputSimulator.KeyUp(VirtualKey.AltLeft);
                _altHeld = false;
            }
            // Ensure all engines go idle on Stop (never toggle into active state)
            if (_autoTarget.State != CombatState.Idle) _autoTarget.ToggleCombatMode();
            if (_kite.IsActive)    _kite.ToggleKiteMode();
            if (_mage.IsActive)    _mage.ToggleMageMode();
            if (_support.IsActive) _support.ToggleSupportMode();
            Log("Engine stopped.", LogLevel.Info, "Engine");
            SetStatus(EngineStatus.Stopped, "Stopped");
        }

        // ── Live Update (Slider → Engine without profile reload) ───────────────────
        public void LiveUpdateDeadzone   (float v) { _movement.Deadzone   = v; }
        public void LiveUpdateCurve      (float v) { _movement.Curve      = v; }
        public void LiveUpdateActionSpeed(float v) { _movement.LeashRadius = (int)(v * 36f); }
        public void LiveUpdateCursorSpeed   (float v) { _cursor.MaxSpeed         = v; }
        public void LiveUpdateForwardBias   (float v) { _movement.ForwardBias       = v; }
        public void LiveUpdateClickCooldown (int   v) { _movement.ClickCooldownMs   = v; }
        public void LiveUpdateActionRpg  (bool  v) { _movement.ActionRpgMode = v; }

        // ── Log ──────────────────────────────────────────────────────────────────
        public event Action<string>? LogMessage;

        public AdvancedLogger Logger   => _advLogger;
        public FeedbackSystem Feedback => _feedback;

        // Live feedback settings
        public void SetSoundEnabled (bool v) => _feedback.SoundEnabled  = v;
        public void SetRumbleEnabled(bool v) => _feedback.RumbleEnabled = v;

        public void Log(string msg, LogLevel level = LogLevel.Info, string category = "Engine")
        {
            _advLogger.Log(level, category, msg);
            // Send same format to UI log: timestamp + level indicator + message
            string lvlIcon = level switch
            {
                LogLevel.Warning => "⚠",
                LogLevel.Error   => "✖",
                LogLevel.Debug   => "◌",
                _                => "●"
            };
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {lvlIcon} [{category}] {msg}");
        }

        public void LoadProfile(Profile profile)
        {
            _movement.Sensitivity     = profile.MouseSensitivity;
            _movement.Deadzone        = profile.Deadzone;
            _movement.Curve           = profile.MovementCurve;
            _movement.ActionRpgMode   = profile.ActionRpgMode;
            _movement.LeashRadius     = (int)(profile.ActionSpeed * 36f);
            _movement.CoastFrames     = profile.MovementCoastFrames;
            _movement.CurveMode       = profile.MovementCurveMode == 0
                                        ? MovementCurveMode.Classic
                                        : MovementCurveMode.DualZone;
            _movement.ClickCooldownMs    = profile.ClickCooldownMs;
            _movement.ClickCooldownMaxMs = profile.ClickCooldownMaxMs;
            _movement.ForwardBias        = profile.MovementForwardBias;

            // Cursor engine settings from profile
            _cursor.MaxSpeed = profile.CursorMaxSpeed;
            _cursor.Deadzone = profile.CursorDeadzone;
            _cursor.Curve    = profile.CursorCurve;

            _combat.LoadProfile(profile);

            _autoTarget.AutoAttackEnabled   = profile.AutoAttackEnabled;
            _autoTarget.AutoRetargetEnabled = profile.AutoRetargetEnabled;
            _autoTarget.AttackKey_VK        = profile.AutoAttackKeyVK;
            _autoTarget.AttackIntervalMs    = profile.AutoAttackIntervalMs;
            _autoTarget.TabCycleMs          = profile.TabCycleMs;
            _autoTarget.AimSensitivity      = profile.AimSensitivity;
            _autoTarget.AimDeadzone         = profile.AimDeadzone;

            _kite.KiteEnabled           = profile.KiteEnabled;
            _kite.AttackKeyVK           = profile.KiteAttackKeyVK;
            _kite.AttackIntervalMs      = profile.KiteAttackIntervalMs;
            _kite.AttacksBeforeRetreat  = profile.KiteAttacksBeforeRetreat;
            _kite.RetreatDurationMs     = profile.KiteRetreatDurationMs;
            _kite.PivotDurationMs       = profile.KitePivotDurationMs;
            _kite.RetreatCursorDist     = profile.KiteRetreatCursorDist;
            _kite.AimSensitivity        = profile.KiteAimSensitivity;

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
            _currentProfile = profile;
            Log($"Profile loaded: {profile.Name} ({profile.Class})", LogLevel.Info, "Profile");
        }

        private int _reconnectCounter  = 0;
        private readonly System.Diagnostics.Stopwatch _tickWatch = new();
        private int _batteryCounter    = 0;
        private const int ReconnectEveryTicks = 250;  // ~2s at 8ms
        private const int BatteryEveryTicks   = 1250; // ~10s at 8ms

        private void OnTick(object? sender, EventArgs e)
        {
            _tickWatch.Restart();

            var gamepadState = _ctrl.GetGamepad();

            if (!gamepadState.HasValue)
            {
                if (IsRunning)
                {
                    _reconnectCounter++;
                    if (_reconnectCounter >= ReconnectEveryTicks)
                    {
                        _reconnectCounter = 0;
                        _ctrl.DetectController();
                        if (_ctrl.IsConnected)
                        {
                            Log("Controller reconnected: " + _ctrl.ControllerType, LogLevel.Info, "Controller");
                            Log("Engine started – controller: " + _ctrl.ControllerType, LogLevel.Info, "Engine");
            SetStatus(EngineStatus.Running, "Running");
                        }
                        else
                        {
                            Log("Controller not found, waiting...", LogLevel.Warning, "Controller");
                            SetStatus(EngineStatus.NoController, "Controller disconnected – waiting...");
                        }
                    }
                }
                return;
            }
            _reconnectCounter = 0;

            // ── Battery check (every ~10s) ───────────────────────────────────
            _batteryCounter++;
            if (_batteryCounter >= BatteryEveryTicks)
            {
                _batteryCounter = 0;
                var batt = _ctrl.GetBatteryLevel();
                BatteryChanged?.Invoke(batt);
            }

            var pad = gamepadState.Value;

            // ── Modifier layers ─────────────────────────────────────────────────────
            bool l1 = pad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
            bool r1 = pad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
            bool l2 = pad.LeftTrigger  > 50;
            bool r2 = pad.RightTrigger > 50;
            _combat.UpdateLayers(l1, r1, l2, r2);
            _curL1 = l1; _curR1 = r1; _curL2 = l2; _curR2 = r2;

            // ── Left stick → movement ──────────────────────────────────────────────
            float stickX = NormalizeAxis(pad.LeftThumbX);
            float stickY = NormalizeAxis(pad.LeftThumbY);
            _movement.Update(stickX, stickY);

            // ── X button = hold Alt (show ground items) ───────────────────────────
            bool xNow = pad.Buttons.HasFlag(GamepadButtonFlags.X);
            bool xWas = _prevButtons.HasFlag(GamepadButtonFlags.X);
            if (!l1 && !r1 && !l2 && !r2) // Only when no modifier – otherwise it would be a skill
            {
                if (xNow && !xWas && !_altHeld)
                {
                    InputSimulator.KeyDown(VirtualKey.AltLeft);
                    _altHeld = true;
                }
                else if (!xNow && xWas && _altHeld)
                {
                    InputSimulator.KeyUp(VirtualKey.AltLeft);
                    _altHeld = false;
                }
            }
            else if (_altHeld)
            {
                // Modifier pressed while Alt held → release Alt
                InputSimulator.KeyUp(VirtualKey.AltLeft);
                _altHeld = false;
            }

            // ── R3 = Double click (only in default mode; special engines handle R3 themselves) ──
            bool r3Now = pad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
            bool r3Was = _prevButtons.HasFlag(GamepadButtonFlags.RightThumb);
            // Block R3-DoubleClick only when a special engine is ACTIVELY running
            bool anySpecialEngineActive = _kite.IsActive || _mage.IsActive || _support.IsActive
                                       || _autoTarget.State != CombatState.Idle;
            if (r3Now && !r3Was && !anySpecialEngineActive)
                InputSimulator.DoubleClick();

            // ── Select button = Precision mode toggle ──────────────────────────────
            bool selectNow = pad.Buttons.HasFlag(GamepadButtonFlags.Back);
            bool selectWas = _prevButtons.HasFlag(GamepadButtonFlags.Back);
            if (selectNow && !selectWas)
            {
                _precisionMode = !_precisionMode;
                _cursor.PrecisionMode = _precisionMode;
                _feedback.Trigger(FeedbackType.PrecisionModeOn);
            }

            // ── Start+DPad = Profile Quick-Switch ───────────────────────────────
            bool startHeld = pad.Buttons.HasFlag(GamepadButtonFlags.Start);
            if (startHeld)
            {
                bool dUp    = pad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)    && !_prevButtons.HasFlag(GamepadButtonFlags.DPadUp);
                bool dDown  = pad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)  && !_prevButtons.HasFlag(GamepadButtonFlags.DPadDown);
                if (dUp)   ProfileQuickSwitch?.Invoke(+1);
                if (dDown) ProfileQuickSwitch?.Invoke(-1);
            }

            // ── Right stick → cursor / special engine aiming ──────────────────────
            float camX = NormalizeAxis(pad.RightThumbX);
            float camY = NormalizeAxis(pad.RightThumbY);
            bool  l3Now = pad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
            bool  l3Was = _prevButtons.HasFlag(GamepadButtonFlags.LeftThumb);
            bool  rbHeld = r1; // RightShoulder = R1, bereits oben gelesen

            if (_support.IsActive || (_support.SupportEnabled && l3Now && !l3Was))
            {
                bool justToggled = l3Now && !l3Was;
                if (justToggled) _support.ToggleSupportMode();
                // Pass l3=false when just toggled to prevent accidental SelfHeal on activation
                _support.Update(camX, camY, r3Now, justToggled ? false : l3Now, r2, rbHeld, TickMs);
            }
            else if (_mage.IsActive || (_mage.MageEnabled && l3Now && !l3Was))
            {
                if (l3Now && !l3Was) _mage.ToggleMageMode();
                _mage.Update(camX, camY, r3Now, r2, l2, TickMs);
            }
            else if (_kite.IsActive || (_kite.KiteEnabled && l3Now && !l3Was))
            {
                if (l3Now && !l3Was) _kite.ToggleKiteMode();
                _kite.Update(camX, camY, r3Now, r2, l2, TickMs);
            }
            else
            {
                // Default mode: right stick moves cursor
                _cursor.Update(camX, camY, TickMs);
            }

            // ── Mob-Sweep Mode (R2 + LS) ─────────────────────────────────────────────
            // R2 gehalten + Linker Stick bewegt → automatisch TAB-Cycle + Angriff
            // Ermöglicht Mobben ohne manuelles TAB-Drücken
            bool anySpecialEngineForSweep = _kite.IsActive || _mage.IsActive || _support.IsActive;
            bool sweepActive = _curR2 && !anySpecialEngineForSweep
                            && _currentProfile?.MobSweepEnabled == true
                            && (MathF.Sqrt(stickX * stickX + stickY * stickY) > _movement.Deadzone);

            if (sweepActive)
            {
                _sweepTabTimer -= TickMs;

                // Fällig: TAB drücken → nächster Mob wird Target in RO
                if (_sweepTabTimer <= 0)
                {
                    InputSimulator.TapKey(VirtualKey.Tab);
                    _sweepTabTimer    = _currentProfile!.MobSweepTabIntervalMs;
                    _sweepAttackTimer = _currentProfile!.MobSweepAttackDelayMs; // Angriff in X ms
                }

                // Kurz nach TAB: Angriff auf das neue Target feuern
                if (_sweepAttackTimer >= 0)
                {
                    _sweepAttackTimer -= TickMs;
                    if (_sweepAttackTimer <= 0)
                    {
                        InputSimulator.TapKey((VirtualKey)(_currentProfile?.MobSweepAttackKeyVK ?? 0x5A));
                        _sweepAttackTimer = -1; // Reset bis zum nächsten TAB
                    }
                }

                _sweepWasActive = true;
            }
            else if (_sweepWasActive)
            {
                // Sweep gerade deaktiviert → Timer zurücksetzen
                _sweepTabTimer    = 0;
                _sweepAttackTimer = -1;
                _sweepWasActive   = false;
            }

            // ── AutoTarget (Melee auto-attack + tab-cycle) ───────────────────────
            // Only run when no other specialised engine is active (avoids cursor conflict)
            bool noSpecialEngine = !_kite.IsActive && !_mage.IsActive && !_support.IsActive && !_sweepWasActive;
            if (noSpecialEngine && (_autoTarget.AutoAttackEnabled || _autoTarget.AutoRetargetEnabled))
            {
                bool rbHeldAT = r1;
                _autoTarget.Update(camX, camY, r3Now,
                    _prevButtons.HasFlag(SharpDX.XInput.GamepadButtonFlags.RightThumb),
                    rbHeldAT, TickMs);
            }

            // ── Macro Playback tick ──────────────────────────────────────────────
            _combat.UpdateMacroPlayback(TickMs);

            // ── Button Mappings ──────────────────────────────────────────────────────
            // Special buttons (X=Alt, RightThumb=DoubleClick) handled above directly
            ProcessButtons(pad.Buttons);

            // ── UI update ───────────────────────────────────────────────────────────
            SnapshotUpdated?.Invoke(new ControllerSnapshot
            {
                LeftX  = stickX, LeftY = stickY, RightX = camX, RightY = camY,
                L1 = l1, R1 = r1, L2 = l2, R2 = r2,
                Buttons = pad.Buttons,
                ControllerName = _ctrl.ControllerName,
                ControllerType = _ctrl.ControllerType,
                PrecisionMode  = _precisionMode,
                MobSweepActive = _sweepWasActive,
                LayerText = l1 ? "L1+" : r1 ? "R1+" : l2 ? "L2+" : r2 ? "R2+" : "BASE",
                CombatState = _autoTarget.State,
                StateLabel = _support.IsActive ? _support.PhaseLabel
                           : _mage.IsActive    ? _mage.PhaseLabel
                           : _kite.IsActive    ? _kite.PhaseLabel
                           : _sweepWasActive   ? "MOB SWEEP"
                           : _autoTarget.StateLabel
            });

            _prevButtons = pad.Buttons;

            var perfWarning = _advLogger.LogPerformance("OnTick", _tickWatch.ElapsedTicks);
            if (perfWarning != null)
                LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] ⚠ {perfWarning}");
        }

        private void ProcessButtons(GamepadButtonFlags current)
        {
            // Check if any modifier (L1/R1/L2/R2) is active
            bool anyModifier = _curL1 || _curR1 || _curL2 || _curR2
                            || _prevButtons.HasFlag(GamepadButtonFlags.LeftShoulder)
                            || _prevButtons.HasFlag(GamepadButtonFlags.RightShoulder);

            foreach (GamepadButtonFlags flag in Enum.GetValues<GamepadButtonFlags>())
            {
                if (flag == 0) continue;

                // RightThumb = Doppelklick → immer in OnTick behandelt
                if (flag == GamepadButtonFlags.RightThumb) continue;

                // X without modifier = Alt-Hold → handled in OnTick, skip here
                // X WITH modifier (L1+X = F3, R1+X = F7 etc.) → process normally
                if (flag == GamepadButtonFlags.X && !anyModifier) continue;

                // L1/R1 are pure modifiers → don't fire as buttons
                if (flag == GamepadButtonFlags.LeftShoulder || flag == GamepadButtonFlags.RightShoulder)
                    continue;

                bool now = current.HasFlag(flag);
                if (now || _prevButtons.HasFlag(flag))
                    _combat.ProcessButton(flag.ToString(), now, TickMs);
            }
        }

        private static float NormalizeAxis(short raw) => raw >= 0 ? raw / 32767f : raw / 32768f;

        private void SetStatus(EngineStatus status, string text)
        {
            StatusText = text;
            StatusChanged?.Invoke(status);
        }
    }

    public enum EngineStatus { Stopped, Running, NoController }

    public class ControllerSnapshot
    {
        public float LeftX  { get; init; }
        public float LeftY  { get; init; }
        public float RightX { get; init; }
        public float RightY { get; init; }
        public bool  L1     { get; init; }
        public bool  R1     { get; init; }
        public bool  L2     { get; init; }
        public bool  R2     { get; init; }
        public GamepadButtonFlags Buttons       { get; init; }
        public string             ControllerName { get; init; } = "";
        public string             ControllerType { get; init; } = "";
        public string             StateLabel     { get; init; } = "IDLE";
        public string             LayerText      { get; init; } = "Base";
        public bool               PrecisionMode   { get; init; } = false;
        public bool               MobSweepActive  { get; init; } = false;
        public CombatState        CombatState     { get; init; }
    }
}
