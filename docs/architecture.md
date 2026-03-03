# RagnaController — Architecture

## System Overview

```
EXE startup
    │
    └── App.xaml.cs: ShowSplashThenMain()
           │
           ├── SplashWindow (3 s animated)
           │
           └── MainWindow
                  │
                  ├── HybridEngine  ←──────────────── 125 Hz DispatcherTimer
                  │     │
                  │     ├── ControllerService   (XInput poll + WMI detect)
                  │     ├── MovementEngine      (left stick → SendInput click)
                  │     ├── CursorEngine        (right stick → mouse move)
                  │     ├── CombatEngine        (button map · turbo · macro)
                  │     ├── AutoTargetEngine    (Melee FSM)
                  │     ├── KiteEngine          (Ranged FSM)
                  │     ├── MageEngine          (Mage FSM)
                  │     ├── SupportEngine       (Support FSM)
                  │     ├── FeedbackSystem      (rumble + SystemSounds)
                  │     └── AdvancedLogger      (tick metrics + file log)
                  │
                  ├── Global hotkeys   (Win32 RegisterHotKey / WM_HOTKEY)
                  ├── System tray      (WinForms NotifyIcon)
                  ├── ProfileManager   (39 built-ins + user JSON)
                  └── Settings         (JSON in %AppData%)
```

---

## Tick Loop (HybridEngine.OnTick — 8 ms)

```
1.  ControllerService.GetGamepad()         ← SharpDX XInput poll
2.  If no gamepad → reconnect counter      ← tries DetectController() every ~2 s
3.  Battery check every ~10 s              → BatteryChanged event → header icon
4.  Read modifier flags L1/R1/L2/R2
5.  Left stick → MovementEngine.Update()
6.  X button (no modifier) → Alt hold/release (SendInput KeyDown/KeyUp)
7.  R3 (no special engine) → InputSimulator.DoubleClick()
8.  SELECT → toggle PrecisionMode on CursorEngine
9.  START + D-Pad → ProfileQuickSwitch event (+1 / -1)
10. Right stick → active special engine OR CursorEngine.Update()
11. AutoTargetEngine.Update()  (only when no Kite/Mage/Support active)
12. CombatEngine.UpdateMacroPlayback()
13. CombatEngine.ProcessButton() for each changed button flag
14. SnapshotUpdated event → MainWindow UI update (via Dispatcher.Invoke)
```

---

## Input Layer Resolution (CombatEngine)

```
Button pressed
    │
    ├─ L1 held? → look up "L1+{button}" in profile.ButtonMappings
    ├─ R1 held? → look up "R1+{button}"
    ├─ L2 held? → look up "L2+{button}"
    ├─ R2 held? → look up "R2+{button}"
    └─ none     → look up "{button}"          ← base layer

Special base-layer overrides (handled before CombatEngine):
    X (no modifier) = Alt hold       — not in ButtonMappings
    R3 (no modifier) = DoubleClick   — not in ButtonMappings
```

---

## Engine Activation Rules

```
L3 press
    │
    ├─ SupportEnabled && !support.IsActive  → ToggleSupportMode()
    ├─ MageEnabled    && !mage.IsActive     → ToggleMageMode()
    ├─ KiteEnabled    && !kite.IsActive     → ToggleKiteMode()
    └─ else                                 → ToggleAutoTarget()

Cursor ownership:
    Support.IsActive  → right stick owned by SupportEngine
    Mage.IsActive     → right stick owned by MageEngine
    Kite.IsActive     → right stick owned by KiteEngine
    AutoTarget active → right stick shared (aim + auto-attack)
    none              → right stick → CursorEngine (free cursor)
```

---

## Input Flow

```
Physical controller button press
    ↓
Windows XInput driver
    ↓
SharpDX.XInput (GetState)
    ↓
HybridEngine.OnTick()
    ↓
CombatEngine / MovementEngine / CursorEngine / special FSM
    ↓
InputSimulator (P/Invoke user32.dll SendInput)
    ↓
Windows input queue
    ↓
Ragnarok Online process
```

---

## Controller Detection Flow

```
ControllerService.DetectController()
    │
    └── Try XInput slots 0–3
           │
           └── If connected:
                  │
                  └── DetectControllerType() ← WMI query Win32_PnPEntity
                         │
                         ├── Name/HardwareID contains VID_054C + PID_0CE6/0DF2 → "PS5"
                         ├── Name/HardwareID contains VID_054C + PID_05C4/09CC → "PS4"
                         ├── VID_057E + PID_2009                                → "Switch"
                         ├── VID_2DC8                                           → "8BitDo"
                         ├── VID_046D + PID_C21D/C21F/C218                     → "Logitech"
                         ├── VID_1532                                           → "Razer"
                         ├── VID_044F                                           → "Thrustmaster"
                         └── fallback                                           → "Xbox"
```

---

## Profile Storage

| Type | Location | Notes |
|---|---|---|
| Built-in profiles | Compiled into exe (`ProfileManager.cs`) | 39 classes, read-only |
| User overrides | `%AppData%\RagnaController\Profiles\*.json` | Saved when user edits |
| Macros | `%AppData%\RagnaController\Macros\*.json` | Saved by MacroRecorder |
| Settings | `%AppData%\RagnaController\settings.json` | Saved on every change |
| Session logs | `%LocalAppData%\RagnaController\Logs\` | Written during session |

---

## Startup Sequence

```
1. App.OnStartup()        ← runtime version check (.NET 8+)
2. ShowSplashThenMain()   ← async: splash shown immediately
3. Task.Delay(3000)       ← 3-second display
4. splash.FadeAndClose()  ← 300 ms fade-out, then Close()
5. new MainWindow()
      ├── InitializeComponent()
      ├── SourceInitialized → RegisterGlobalHotkeys()
      ├── SubscribeEngineEvents()
      ├── PopulateProfiles()
      ├── SelectLastProfile()
      ├── ApplySettings()
      ├── InitTrayIcon()
      ├── RestoreWindowPosition()
      ├── if AutoStart → _engine.Start()
      ├── if StartInMiniMode → SwitchToMiniMode()
      └── CheckForUpdatesAsync() (after 3 s delay)
```

---

## Key Files

| File | Responsibility |
|---|---|
| `App.xaml.cs` | Startup, splash, runtime check |
| `SplashWindow.xaml.cs` | Animated intro, FadeAndClose() |
| `MainWindow.xaml.cs` | Main UI, tray icon, global hotkeys, engine wiring |
| `Core/HybridEngine.cs` | 125 Hz tick loop, event bus, engine orchestration |
| `Core/CombatEngine.cs` | Button mapping, 5 layers, turbo, macro playback |
| `Core/AutoTargetEngine.cs` | Melee tab+attack FSM |
| `Core/KiteEngine.cs` | Ranged 5-phase kite FSM |
| `Core/MageEngine.cs` | Ground-target / bolt mode FSM |
| `Core/SupportEngine.cs` | Heal / party cycle / ground mode FSM |
| `Core/MacroRecorder.cs` | Record, playback, JSON save/load, AppData storage |
| `Core/InputSimulator.cs` | SendInput P/Invoke (keyboard + mouse) |
| `Core/FeedbackSystem.cs` | Rumble + SystemSounds, per-event patterns |
| `Core/AdvancedLogger.cs` | Tick metrics, session log, export |
| `Core/UpdateChecker.cs` | GitHub releases API check |
| `Controller/ControllerService.cs` | XInput poll, 8-brand WMI detection, battery |
| `Profiles/ProfileManager.cs` | 39 built-in profiles + user JSON load/save |
| `Models/Settings.cs` | App settings with JSON persistence |
