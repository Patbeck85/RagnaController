using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using RagnaController.Controller;
using RagnaController.Profiles;
using SharpDX.XInput;

namespace RagnaController.Core
{
    public enum EngineStatus { Stopped, Running, NoController }

    public class ControllerSnapshot
    {
        public float LeftX  { get; init; }
        public float LeftY  { get; init; }
        public float RightX { get; init; }
        public float RightY { get; init; }
        public bool L1 { get; init; }
        public bool R1 { get; init; }
        public bool L2 { get; init; }
        public bool R2 { get; init; }
        public string StateLabel      { get; init; } = "IDLE";
        public string LayerText       { get; init; } = "BASE";
        public CombatState CombatState { get; init; }
        public bool MobSweepActive    { get; init; }
        public bool PrecisionMode     { get; init; }
        public bool VacuumActive      { get; init; }
        public bool PanicActive       { get; init; }
        public double TickMs          { get; init; }
        public bool GroundSpellPending { get; init; }
        public bool GroundAimHeld     { get; init; }
        public bool ComboActive       { get; init; }
        public string ComboLabel      { get; init; } = "";
    }

    public class HybridEngine
    {
        private readonly MovementEngine   _movement   = new();
        private readonly CursorEngine     _cursor     = new();
        private readonly CombatEngine     _combat     = new();
        private readonly AutoTargetEngine _autoTarget = new();
        private readonly KiteEngine       _kite       = new();
        private readonly MageEngine       _mage       = new();
        private readonly SupportEngine    _support    = new();
        private readonly ComboEngine      _combo      = new();
        private readonly ControllerService _ctrl      = new();
        private readonly AdvancedLogger   _advLogger  = new();
        private readonly VoiceChatService _voice      = new();
        private FeedbackSystem _feedback = null!;
        private Profile? _currentProfile;
        private bool _isRenewal = true;
        private RagnaController.RadialMenuWindow?   _radial    = null;
        private RagnaController.DaisyWheelWindow?   _daisy     = null;

        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _watchTimer;
        private const int TickMs = 8;
        private int  _uiCounter, _batt, _sweepTab, _sweepAtk = -1, _panicCooldown;
        private const int UISkip = 4; 
        private GamepadButtonFlags _prevButtons;
        private bool _altHeld;

        // Eigenschaften bleiben für die Kompatibilität der UI erhalten, tun aber nichts mehr
        public bool   FocusLockEnabled  { get; set; } = false;
        public string FocusLockProcess  { get; set; } = "";
        public bool   IsFocusLocked     { get; private set; }

        private readonly System.Diagnostics.Stopwatch _tickWatch = new();

        private static readonly GamepadButtonFlags[] _relevant =
            ((GamepadButtonFlags[])Enum.GetValues(typeof(GamepadButtonFlags))).Where(f => f != 0).ToArray();
        private static readonly Dictionary<GamepadButtonFlags, string> _btnCache =
            _relevant.ToDictionary(f => f, f => f.ToString());

        public bool   IsRunning      { get; private set; }
        public bool   IsPaused       { get; private set; }
        public string StatusText     { get; private set; } = "Waiting for Controller…";
        public string ControllerName => _ctrl.ControllerName;

        public event Action<EngineStatus>?       StatusChanged;
        public event Action<ControllerSnapshot>? SnapshotUpdated;
        public event Action<int>?                ProfileQuickSwitch;
        public event Action?                     RestoreMainWindowRequested;
        public event Action<string>?             BatteryChanged, LogMessage;
        public event Action<string>?             ControllerConnected;
        public event Action?                     ControllerDisconnected;
        public event Action<string>?             VoiceStatusChanged;

        public HybridEngine()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(TickMs)
            };
            _timer.Tick += OnTick;

            _watchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _watchTimer.Tick += OnWatch;

            _autoTarget.StateChanged += (_) =>
                StatusChanged?.Invoke(IsRunning ? EngineStatus.Running : EngineStatus.NoController);

            _feedback = new FeedbackSystem(_ctrl);
            
            _combat.ActionFired += (action) =>
            {
                _feedback.TriggerSkillFired();

                if (action.Type == ActionType.SwitchWindow)
                {
                    WindowSwitcher.Toggle(action.WindowTarget);
                    return;
                }

                if (_autoTarget.SmartSkillEnabled
                    && _autoTarget.State != CombatState.Idle
                    && _autoTarget.IsTargetLocked
                    && action.Type == ActionType.Key
                    && !action.IsGroundSpell)
                {
                    _autoTarget.FireSmartSkill(action.Key);
                }
                else
                {
                    if      (action.Type == ActionType.Key)        InputSimulator.TapKey(action.Key);
                    else if (action.Type == ActionType.LeftClick)  InputSimulator.LeftClick();
                    else if (action.Type == ActionType.RightClick) InputSimulator.RightClick();
                    _autoTarget.NotifySkillFired();
                }
            };

            _combat.GroundSpellFired += () =>
            {
                if (_mage.IsActive) _mage.EnterGroundAim(false, false, false, false);
            };

            _combo.ComboStepFired += (step) =>
            {
                _feedback.TriggerSkillFired();
                Log($"Combo step {step}/{_combo.TotalSteps}", LogLevel.Debug);
            };

            _voice.StatusChanged  += msg => VoiceStatusChanged?.Invoke(msg);
            _voice.TextRecognized += txt => Log($"🎤 Recognised: \"{txt}\"");

            _watchTimer.Start();
        }

        private void OnWatch(object? sender, EventArgs e)
        {
            if (IsRunning || IsPaused) return;
            _ctrl.DetectController();
            if (_ctrl.IsConnected) AutoStart();
        }

        private void AutoStart()        
        {
            _timer.Stop();
            _movement.Reset(); 
            _cursor.Reset(); 
            _combo.Reset();
            
            if (_currentProfile != null) LoadProfile(_currentProfile);
            
            IsRunning = true;
            ControllerConnected?.Invoke(_ctrl.ControllerName);
            SetStatus(EngineStatus.Running, "Running");
            Log($"Controller connected: {_ctrl.ControllerName}");
            _timer.Start();
        }

        private void AutoStop()
        {
            _timer.Stop(); 
            IsRunning = false; 
            _feedback.StopRumble();
            _combo.Reset(); 
            _voice.StopListening();
            
            if (_daisy != null) { _daisy.Close(); _daisy = null; }
            if (_altHeld) { InputSimulator.KeyUp(VirtualKey.AltLeft); _altHeld = false; }
            
            _feedback.Trigger(FeedbackType.Warning); 
            ControllerDisconnected?.Invoke();
            SetStatus(EngineStatus.NoController, "Waiting for Controller…");
            Log("Controller disconnected — waiting for connection...");
        }

        public void Pause()
        {
            if (!IsRunning) return;
            _timer.Stop(); 
            IsPaused = true; 
            IsRunning = false;
            _feedback.StopRumble();
            if (_altHeld) { InputSimulator.KeyUp(VirtualKey.AltLeft); _altHeld = false; }
            SetStatus(EngineStatus.Stopped, "Paused");
            Log("Engine paused.");
        }

        public void Resume()
        {
            if (!IsPaused || !_ctrl.IsConnected) return;
            IsPaused = false; 
            IsRunning = true;
            _timer.Start();
            SetStatus(EngineStatus.Running, "Running");
            Log("Engine resumed.");
        }

        public void Shutdown()
        {
            _watchTimer.Stop();
            _timer.Stop(); 
            IsRunning = false; 
            IsPaused = false;
            _feedback.StopRumble(); 
            _voice.StopListening();
            
            if (_daisy != null) { _daisy.Close(); _daisy = null; }
            if (_altHeld) { InputSimulator.KeyUp(VirtualKey.AltLeft); _altHeld = false; }
        }

        public void SetSoundEnabled(bool v)        => _feedback.SoundEnabled  = v;
        public void SetRumbleEnabled(bool v)       => _feedback.RumbleEnabled = v;
        public void LiveUpdateDeadzone(float v)    => _movement.Deadzone      = v;
        public void LiveUpdateCurve(float v)       => _movement.Curve         = v;
        public void LiveUpdateActionSpeed(float v) => _movement.LeashRadius   = (int)(v * 36f);
        public void LiveUpdateCursorSpeed(float v) => _cursor.MaxSpeed        = v;
        public void LiveUpdateActionRpg(bool v)    => _movement.ActionRpgMode = v;
        public void RecalibrateCursor()            => _mage.RecalibrateCursor();

        public void Log(string msg, LogLevel l = LogLevel.Info)
        {
            _advLogger.Log(l, "Engine", msg);
            LogMessage?.Invoke(msg);
        }

        public void LoadProfile(Profile p)
        {
            _currentProfile = p; 
            
            _movement.Sensitivity   = p.MouseSensitivity;
            _movement.Deadzone      = p.Deadzone;
            _movement.Curve         = p.MovementCurve;
            _movement.ActionRpgMode = p.ActionRpgMode;
            _combat.LoadProfile(p);

            _autoTarget.AutoAttackEnabled   = p.AutoAttackEnabled;
            _autoTarget.AutoRetargetEnabled = p.AutoRetargetEnabled;
            _autoTarget.SmartSkillEnabled   = p.SmartSkillEnabled;
            _autoTarget.AttackKey_VK        = p.AutoAttackKeyVK;
            _autoTarget.TabCycleMs          = p.TabCycleMs;
            _autoTarget.AimSensitivity      = p.AimSensitivity;
            _autoTarget.AimDeadzone         = p.AimDeadzone;

            _mage.MageEnabled         = p.MageEnabled;
            _mage.MageBoltKeyVK       = p.MageBoltKeyVK;
            _mage.MageBoltCastDelayMs = p.MageBoltCastDelayMs;

            _kite.KiteEnabled       = p.KiteEnabled;
            _kite.AttackKeyVK       = p.KiteAttackKeyVK;
            _kite.AttackIntervalMs  = p.KiteAttackIntervalMs;

            _support.SupportEnabled = p.SupportEnabled;
            _support.HealKeyVK      = p.SupportHealKeyVK;

            _combo.Enabled  = p.ComboEnabled;
            _combo.Sequence = p.ComboSequenceVK;

            ApplyGameMode(_isRenewal);
        }

        public void ApplyGameMode(bool isRenewal)
        {
            _isRenewal = isRenewal;
            if (_currentProfile == null) return;
            _autoTarget.AttackIntervalMs = isRenewal
                ? _currentProfile.RenewalAttackIntervalMs
                : _currentProfile.PreRenewalAttackIntervalMs;
            _autoTarget.SkillInterruptMs = isRenewal
                ? _currentProfile.RenewalSkillInterruptMs
                : _currentProfile.PreRenewalSkillInterruptMs;
            
            _combo.CurrentDelays = isRenewal
                ? _currentProfile.RenewalComboDelays
                : _currentProfile.PreRenewalComboDelays;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            try 
            {
                _tickWatch.Restart();
                
                var state = _ctrl.GetGamepad();
                if (!state.HasValue)
                {
                    AutoStop();
                    return;
                }

                var pad = state.Value;

                // Hardware Sanity Check
                if (pad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && pad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) return;
                if (pad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft) && pad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) return;
                
                if (++_batt >= 1250) { _batt = 0; BatteryChanged?.Invoke(_ctrl.GetBatteryLevel()); }

                // FOCUS LOCK DEAKTIVIERT - Controller ist IMMER aktiv!
                IsFocusLocked = false;
                
                bool l1 = pad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
                bool r1 = pad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
                bool l2 = pad.LeftTrigger  > 50;
                bool r2 = pad.RightTrigger > 50;
                bool l3 = pad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
                bool r3 = pad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);

                _combat.UpdateLayers(l1, r1, l2, r2);

                if (pad.Buttons.HasFlag(GamepadButtonFlags.Start))
                {
                    if (pad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)   && !_prevButtons.HasFlag(GamepadButtonFlags.DPadUp))   ProfileQuickSwitch?.Invoke(1);
                    if (pad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) && !_prevButtons.HasFlag(GamepadButtonFlags.DPadDown)) ProfileQuickSwitch?.Invoke(-1);
                    if (pad.Buttons.HasFlag(GamepadButtonFlags.Back) && !_prevButtons.HasFlag(GamepadButtonFlags.Back))
                        RestoreMainWindowRequested?.Invoke();
                }

                if (l3 && l1 && !r1 && !l2 && !r2 && _mage.MageEnabled)
                {
                    if (!_prevButtons.HasFlag(GamepadButtonFlags.LeftShoulder) || !_prevButtons.HasFlag(GamepadButtonFlags.LeftThumb))
                        { _mage.ToggleMageMode(); _feedback.Trigger(FeedbackType.PhaseChange); _movement.CenterCursor(); }
                }
                else if (l3 && r1 && !l1 && !l2 && !r2 && _kite.KiteEnabled)
                {
                    if (!_prevButtons.HasFlag(GamepadButtonFlags.RightShoulder) || !_prevButtons.HasFlag(GamepadButtonFlags.LeftThumb))
                        { _kite.ToggleKiteMode(); _feedback.Trigger(FeedbackType.PhaseChange); _movement.CenterCursor(); }
                }
                else if (l3 && l2 && !l1 && !r1 && !r2 && _support.SupportEnabled)
                {
                    if (!_prevButtons.HasFlag(GamepadButtonFlags.LeftThumb))
                        { _support.ToggleSupportMode(); _feedback.Trigger(FeedbackType.PhaseChange); _movement.CenterCursor(); }
                }

                bool x = pad.Buttons.HasFlag(GamepadButtonFlags.X);
                if (!(l1 || r1 || l2 || r2))
                {
                    if ( x && !_prevButtons.HasFlag(GamepadButtonFlags.X) && !_altHeld) { InputSimulator.KeyDown(VirtualKey.AltLeft); _altHeld = true; }
                    else if (!x && _prevButtons.HasFlag(GamepadButtonFlags.X) && _altHeld) { InputSimulator.KeyUp(VirtualKey.AltLeft); _altHeld = false; }
                }
                else if (_altHeld) { InputSimulator.KeyUp(VirtualKey.AltLeft); _altHeld = false; }

                float lx = 0, ly = 0, rx = 0, ry = 0;
                
                float rawLx = pad.LeftThumbX  / 32768f;
                float rawLy = pad.LeftThumbY  / 32768f;
                float rawRx = pad.RightThumbX / 32768f;
                float rawRy = pad.RightThumbY / 32768f;
                
                if (!float.IsNaN(rawLx) && !float.IsInfinity(rawLx)) lx = Math.Clamp(rawLx, -1f, 1f);
                if (!float.IsNaN(rawLy) && !float.IsInfinity(rawLy)) ly = Math.Clamp(rawLy, -1f, 1f);
                if (!float.IsNaN(rawRx) && !float.IsInfinity(rawRx)) rx = Math.Clamp(rawRx, -1f, 1f);
                if (!float.IsNaN(rawRy) && !float.IsInfinity(rawRy)) ry = Math.Clamp(rawRy, -1f, 1f);

                bool backBtn = pad.Buttons.HasFlag(GamepadButtonFlags.Back);
                if (backBtn && r1 && !_prevButtons.HasFlag(GamepadButtonFlags.RightShoulder))
                {
                    if (_daisy == null)
                    {
                        _daisy = new RagnaController.DaisyWheelWindow();
                        _daisy.Show();
                        _feedback.Trigger(FeedbackType.CombatModeOn);
                    }
                    else
                    {
                        _daisy.Close(); _daisy = null;
                    }
                }

                if (backBtn && l1 && !_prevButtons.HasFlag(GamepadButtonFlags.LeftShoulder))
                {
                    if (_voice.IsListening) { _voice.StopListening(); _feedback.Trigger(FeedbackType.CombatModeOff); }
                    else                   { _voice.StartListening(); _feedback.Trigger(FeedbackType.CombatModeOn); }
                }

                if (_daisy != null)
                {
                    bool closed = _daisy.UpdateInput(
                        lx, ly,
                        pad.Buttons.HasFlag(GamepadButtonFlags.A),
                        pad.Buttons.HasFlag(GamepadButtonFlags.B),
                        pad.Buttons.HasFlag(GamepadButtonFlags.X),
                        pad.Buttons.HasFlag(GamepadButtonFlags.Y),
                        l3, r3,
                        pad.Buttons.HasFlag(GamepadButtonFlags.Start),
                        pad.Buttons.HasFlag(GamepadButtonFlags.B));
                    if (closed) _daisy = null;
                    _prevButtons = pad.Buttons;
                    return; 
                }

                if (l2 && r2 && _currentProfile != null)
                {
                    if (_radial == null) { _radial = new RagnaController.RadialMenuWindow(_currentProfile.RadialMenuItems); _radial.Show(); }
                    _radial.UpdateSelection(rx, ry);
                }
                else if (_radial != null) { _radial.ExecuteAndClose(); _radial = null; }

                if (l3 && r3 && _panicCooldown <= 0)
                {
                    InputSimulator.PanicHeal(VirtualKey.F4);
                    _feedback.Trigger(FeedbackType.Error);
                    _panicCooldown = 125;
                }
                _panicCooldown = Math.Max(0, _panicCooldown - 1);

                bool vacuum = l1 && r1 && _radial == null;
                bool sweep  = false;

                bool comboHeld = _combo.Enabled && _combat.IsComboActionHeld(pad.Buttons);
                _combo.Update(comboHeld, 8);

                if (vacuum)
                {
                    _movement.PerformLootVacuum(8);
                }
                else if (_radial == null)
                {
                    if (!((_mage.IsActive && _mage.GroundAimHeld) || _autoTarget.SuppressMovementClicks))
                        _movement.Update(lx, ly);
                    else if (_mage.GroundAimHeld)
                        _movement.Reset();

                    sweep = r2 && !(_mage.IsActive || _support.IsActive || _kite.IsActive)
                               && _currentProfile?.MobSweepEnabled == true
                               && (lx * lx + ly * ly > 0.04f);
                    if (sweep)
                    {
                        if ((_sweepTab -= 8) <= 0) { InputSimulator.TapKey(VirtualKey.Tab); _sweepTab = _currentProfile!.MobSweepTabIntervalMs; _sweepAtk = _currentProfile!.MobSweepAttackDelayMs; }
                        if (_sweepAtk >= 0 && (_sweepAtk -= 8) <= 0) { InputSimulator.TapKey((VirtualKey)(_currentProfile?.MobSweepAttackKeyVK ?? 90)); _sweepAtk = -1; }
                    }
                    else { _sweepTab = 0; _sweepAtk = -1; }

                    if      (_support.IsActive) _support.Update(rx, ry, r3, l3, r2, r1, 8);
                    else if (_mage.IsActive)    _mage.Update(rx, ry, r3, r2, l2, l1, r1, 8);
                    else if (_kite.IsActive)    _kite.Update(rx, ry, r3, r2, l2, 8);
                    else
                    {
                        _cursor.Update(rx, ry, 8);
                        _autoTarget.Update(rx, ry, r3, _prevButtons.HasFlag(GamepadButtonFlags.RightThumb), r2, 8, lx, ly);
                    }
                }

                _combat.UpdateMacroPlayback(8);
                
                if (_radial == null && !vacuum)
                {
                    foreach (var f in _relevant)
                    {
                        if (f == GamepadButtonFlags.RightThumb) continue;
                        if (f == GamepadButtonFlags.X && !(l1 || r1 || l2 || r2)) continue;
                        
                        bool n = pad.Buttons.HasFlag(f);
                        if (n || _prevButtons.HasFlag(f)) _combat.ProcessButton(_btnCache[f], n, 8);
                    }
                }

                _prevButtons = pad.Buttons;

                if (++_uiCounter >= UISkip)
                {
                    _uiCounter = 0;
                    SnapshotUpdated?.Invoke(new ControllerSnapshot
                    {
                        LeftX  = lx, LeftY  = ly, RightX = rx, RightY = ry,
                        L1 = l1, R1 = r1, L2 = l2, R2 = r2,
                        LayerText  = l1 ? "L1+" : r1 ? "R1+" : l2 ? "L2+" : r2 ? "R2+" : "BASE",
                        StateLabel = _radial != null   ? "RADIAL"
                                   : vacuum            ? "VACUUM"
                                   : sweep             ? "SWEEP"
                                   : _panicCooldown > 0 ? "PANIC!"
                                   : _combo.IsActive   ? _combo.StateLabel
                                   : _autoTarget.StateLabel,
                        CombatState       = _autoTarget.State,
                        MobSweepActive    = sweep,
                        PrecisionMode     = _cursor.PrecisionMode,
                        VacuumActive      = vacuum,
                        PanicActive       = _panicCooldown > 0,
                        TickMs            = _tickWatch.Elapsed.TotalMilliseconds,
                        GroundSpellPending = _mage.GroundSpellPending,
                        GroundAimHeld     = _mage.GroundAimHeld,
                        ComboActive       = _combo.IsActive,
                        ComboLabel        = _combo.StateLabel
                    });
                }
                
                _advLogger.LogPerformance("Tick", _tickWatch.ElapsedTicks);
            }
            catch (Exception ex)
            {
                _advLogger.Log(LogLevel.Error, "TICK_CRASH", ex.Message);
                _movement.Reset();
                _cursor.Reset();
                _feedback.StopRumble();
            }
        }

        private void SetStatus(EngineStatus s, string t) { StatusText = t; StatusChanged?.Invoke(s); }
        public AdvancedLogger   Logger     => _advLogger;
        public AutoTargetEngine AutoTarget => _autoTarget;
    }
}