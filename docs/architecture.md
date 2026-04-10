# RagnaController — Architecture

## System Overview

```
EXE startup
    │
    └── App.xaml.cs: StartWorkflow()
           │
           ├── SplashWindow (3 s animated intro)
           │
           └── MainWindow (Obsidian & Gold UI)
                  │
                  ├── HybridEngine  ←──────────────── 125 Hz DispatcherTimer (1 ms precision)
                  │     │
                  │     ├── ControllerService    (XInput poll + WMI brand detection)
                  │     ├── MovementEngine       (left stick → SendInput click, DPI-aware centre)
                  │     ├── CursorEngine         (right stick → smooth mouse move)
                  │     ├── CombatEngine         (5-layer input · turbo · macro playback)
                  │     ├── ComboEngine          (class-aware sequential skill chains)
                  │     ├── AutoTargetEngine     (Melee FSM + Smart Skill cursor juggling)
                  │     ├── KiteEngine           (Ranged 5-phase FSM)
                  │     ├── MageEngine           (ground-target / bolt mode FSM)
                  │     ├── SupportEngine        (heal / party cycle / ground mode FSM)
                  │     ├── VoiceChatService     (System.Speech recognition → chat string)
                  │     ├── WindowSwitcher       (Win32 AttachThreadInput, background thread)
                  │     ├── WindowTracker        (GetClientRect + GetDpiForMonitor → physical centre)
                  │     ├── FeedbackSystem       (rumble patterns + SystemSounds)
                  │     └── AdvancedLogger       (tick metrics + ring buffer log)
                  │
                  ├── Overlay Windows     (RadialMenuWindow, DaisyWheelWindow, MiniModeWindow)
                  ├── ProfileManager      (39 built-ins + user JSON, bak recovery)
                  └── Settings            (AppData JSON persistence, FocusLockProcess)
```

---

## Tick Loop (`HybridEngine.OnTick` — 8 ms / 125 Hz)

```
1.  Poll gamepad              ← ControllerService.GetGamepad() via SharpDX XInput
2.  Battery check             → every ~10 s → BatteryChanged event
3.  Focus Lock + WindowTracker → every ~500 ms:
        a. WindowTracker.Refresh()  → foreground-first process scan
        b. MovementEngine.SetCenter()  → DPI-corrected client centre
        c. IsFocusLocked check  → suppress all input if RO not foreground
4.  If FocusLocked → return early (no input passes through)
5.  Read modifiers            ← L1, R1, L2 (>50), R2 (>50)
6.  Global overlays:
        Back + R1 → DaisyWheelWindow.Show()
        Back + L1 → VoiceChatService.StartListening()
        LT + RT   → RadialMenuWindow.Reopen() or .ExecuteAndClose()
7.  Pro triggers:
        L3 + R3   → Panic heal (F4 × 10)
        LB + RB   → Loot vacuum (throttled 50 ms/click)
8.  Left stick  → MovementEngine.Update()
9.  Engine routing → right stick to active FSM or CursorEngine
10. ComboEngine.Tick()
11. CombatEngine.ProcessButton() for each changed button flag
12. Window switch (ActionType.SwitchWindow) → Task.Run background
13. SnapshotUpdated event → MainWindow UI refresh
14. AdvancedLogger.LogPerformance() → warn if tick > 25 ms
```

---

## WindowTracker Resolution

```
OnTick every ~500 ms
    │
    ├── GetForegroundWindow()
    │       └── GetWindowThreadProcessId() + Process.GetProcessById()
    │               └── ProcessName contains FocusLockProcess?
    │                       YES → use this HWND  (multi-client: always tracks active window)
    │                       NO  → fall through to process scan
    │
    └── Process.GetProcesses() scan
            └── first match with ProcessName + MainWindowHandle
                    └── UpdateGeometry(HWND):
                            GetClientRect()    → logical client dimensions
                            ClientToScreen()   → physical screen origin
                            MonitorFromWindow() → which monitor
                            GetDpiForMonitor() → dpiX (e.g. 144 at 150%)
                            scale = dpiX / 96
                            CenterX = origin.X × scale + (clientW × scale) / 2
                            CenterY = origin.Y × scale + (clientH × scale) / 2
```

---

## Smart Skill Auto-Aim (Cursor Juggling)

```
AutoTargetEngine.OnTick()
    └── target locked → saves _lockPos (screen coords) every tick

CombatEngine.ProcessButton() → ActionType.Key
    └── HybridEngine intercepts if AutoTarget active + target locked
            └── Task.Run (SemaphoreSlim guard):
                    1. GetCursorPos() → save walkPos
                    2. SetCursorPos(_lockPos)
                    3. TapKey(skill) + LeftClick
                    4. SetCursorPos(walkPos)
                    Total: ~12 ms
```

---

## Input Layer Resolution

```
Button pressed
    │
    ├─ L1 held → look up "L1+{button}"
    ├─ R1 held → look up "R1+{button}"
    ├─ L2 held → look up "L2+{button}"
    ├─ R2 held → look up "R2+{button}"
    └─ none    → look up "{button}"  (base layer)

Fixed overrides (before CombatEngine):
    X (no modifier)     → Alt hold
    R3 (no modifier)    → DoubleClick
    Start + DPad        → ProfileQuickSwitch
    Start + Back        → RestoreMainWindowRequested
    Back + L1           → VoiceChat
    Back + R1           → DaisyWheel
```

---

## Input Flow & Win32 Interop

```
Physical controller press
    ↓
Windows XInput driver
    ↓
SharpDX.XInput (GetState)
    ↓
HybridEngine.OnTick()
    ↓
[Focus Lock check — if locked, stop here]
    ↓
CombatEngine → resolves L1+A to VirtualKey.F1
    ↓
InputSimulator.TapKey / SendInput
    (INPUT struct: LayoutKind.Explicit, FieldOffset(8) for 64-bit alignment)
    ↓
Windows input queue
    ↓
Ragnarok Online process (ragexe.exe)
```

*UIPI note:* If RO runs elevated (e.g. Gepard Shield), `SendInput` from a lower-privileged process is blocked. RagnaController shows an orange admin warning banner on startup.

---

## Profile Storage

| Type | Location | Notes |
|---|---|---|
| Built-in profiles | Compiled into exe | 39 classes, read-only |
| User overrides | `%AppData%\RagnaController\Profiles\*.json` | Written on save |
| Backups | `*.bak.json` (same folder) | Restored automatically if primary is corrupt |
| Macros | `%AppData%\RagnaController\Macros\*.json` | Saved by MacroRecorder |
| Settings | `%AppData%\RagnaController\settings.json` | Saved on every change |
| Share codes | `%AppData%\RagnaController\share_codes.json` | Gist ID cache |
| Session logs | `%LocalAppData%\RagnaController\Logs\` | Written during session |

---

## Key Files

| File | Responsibility |
|---|---|
| `App.xaml.cs` | Startup, splash, global Obsidian/Gold styles |
| `MainWindow.xaml.cs` | Main UI, engine wiring, admin check, tab active state |
| `Core/HybridEngine.cs` | 125 Hz tick loop, Focus Lock, WindowTracker integration, multimedia timer |
| `Core/WindowTracker.cs` | DPI-aware window geometry — foreground-first for multi-client |
| `Core/CombatEngine.cs` | 5-layer button mapping, turbo, macro playback, window switch |
| `Core/AutoTargetEngine.cs` | Melee FSM + Smart Skill cursor juggling |
| `Core/InputSimulator.cs` | `SendInput` P/Invoke (64-bit aligned struct, chat serialisation) |
| `Core/FeedbackSystem.cs` | Rumble patterns with pause-aware cancellation |
| `Core/VoiceChatService.cs` | Local speech-to-text integration |
| `Core/WindowSwitcher.cs` | Win32 focus management (background thread) |
| `Core/UpdateChecker.cs` | GitHub releases API check |
| `Profiles/ProfileManager.cs` | 39 built-ins, user JSON, duplicate protection, bak recovery |
| `Models/Settings.cs` | App settings with JSON persistence, FocusLockProcess |
