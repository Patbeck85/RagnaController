# RagnaController — Architecture (v1.2.0)

## System Overview

```text
EXE startup
    │
    └── App.xaml.cs: ShowSplashThenMain()
           │
           ├── SplashWindow (3 s animated)
           │
           └── MainWindow
                  │
                  ├── HybridEngine  ←──────────────── 125 Hz DispatcherTimer (Priority: Input)
                  │     │
                  │     ├── ControllerService   (XInput poll + WMI detect)
                  │     ├── MovementEngine      (Left stick → SendInput click)
                  │     ├── CursorEngine        (Right stick → Mouse move)
                  │     ├── CombatEngine        (Button mapping · Turbo · Macro)
                  │     ├── ComboEngine         (Sequential Skill Chains & Timings)
                  │     ├── AutoTargetEngine    (Melee FSM)
                  │     ├── KiteEngine          (Ranged FSM)
                  │     ├── MageEngine          (Mage FSM)
                  │     ├── SupportEngine       (Support FSM)
                  │     ├── FeedbackSystem      (Rumble + SystemSounds)
                  │     └── AdvancedLogger      (Tick metrics + File log)
                  │
                  ├── Global hotkeys   (Win32 RegisterHotKey / WM_HOTKEY)
                  ├── RadialMenuWindow (Built-in high-res Emotes & Item Wheel)
                  ├── DaisyWheelWindow (Circular On-Screen Keyboard)
                  ├── ProfileManager   (39 built-ins + user JSON overrides)
                  └── Settings         (JSON stored in %AppData%)
Tick Loop (HybridEngine.OnTick — 8 ms)
The entire input loop is wrapped in a secure try-catch block to prevent Windows, broken USB packets, or Anti-Cheat software from crashing the application.
code
Text
1.  ControllerService.GetGamepad()         ← SharpDX XInput poll.
2.  Hardware Sanity Check                  ← Drop frame if impossible inputs detected (e.g., Up+Down).
3.  If no gamepad → AutoStop()             ← Shuts down engines safely, waits for reconnection.
4.  Battery Check                          ← Fires every ~10s. Reports "WIRED" for USB connections.
5.  Read Modifier Flags                    ← Evaluates L1, R1, L2, R2 states.
6.  Pro Shortcuts                          ← Checks for Loot Vacuum (L1+R1), Panic Heal (L3+R3).
7.  Overlay Triggers                       ← Checks for Daisy Wheel (Back+R1) or Radial Menu (L2+R2).
8.  Left Stick                             ← Passed to MovementEngine.Update().
9.  Right Stick                            ← Passed to active special engine OR CursorEngine.Update().
10. AutoTargetEngine.Update()              ← Evaluates tab-targeting and auto-attacks.
11. CombatEngine.ProcessButton()           ← Resolves 5-layer mappings for pushed face buttons.
12. UI Update                              ← UI Snapshot updated every 4th tick (~30 FPS) to prevent WPF UI freezes.
Asynchronous Anti-Freeze Input Pipeline (InputSimulator.cs)
To prevent user32.dll SendInput from freezing the 125Hz Tick Loop when Anti-Cheat systems (like Gepard Shield) or Windows User Interface Privilege Isolation (UIPI) hook into Windows APIs, all physical outputs are detached into background tasks.
code
C#
// Example from InputSimulator.cs
public static void MoveMouseRelative(int dx, int dy) 
{ 
    // Frame-Drop Logic: If the previous command is still blocked by the OS, 
    // skip this frame to keep the Controller Tick Loop alive and responsive.
    if (_mouseMoveInFlight) return; 
    
    _mouseMoveInFlight = true;
    Task.Run(() => 
    {
        try { SendInput(...); } finally { _mouseMoveInFlight = false; }
    });
}
Input Layer Resolution (CombatEngine.cs)
Inputs are resolved sequentially based on active modifier holds:
code
Text
Button pressed
    │
    ├─ L1 held? → Look up "L1+{button}" in profile.ButtonMappings
    ├─ R1 held? → Look up "R1+{button}"
    ├─ L2 held? → Look up "L2+{button}"
    ├─ R2 held? → Look up "R2+{button}"
    └─ none     → Look up "{button}"          ← Base layer

Special Base-Layer Overrides (Handled before CombatEngine):
    X (no modifier) = Alt hold       — Toggles ground items
    R3 (no modifier) = DoubleClick   — Suppressed if Mage/Support active
    START + D-Pad = Profile Switch   — Shifts profile index +1 / -1
Engine Activation Rules
code
Text
L3 (Left Stick Click) Pressed
    │
    ├─ SupportEnabled && !support.IsActive  → ToggleSupportMode()
    ├─ MageEnabled    && !mage.IsActive     → ToggleMageMode()
    ├─ KiteEnabled    && !kite.IsActive     → ToggleKiteMode()
    └─ else                                 → ToggleAutoTarget()

Right Stick Ownership:
    Support.IsActive  → Owned by SupportEngine (Ally aiming)
    Mage.IsActive     → Owned by MageEngine (Ground spell targeting)
    Kite.IsActive     → Owned by KiteEngine (Retreat calculations)
    Radial is Open    → Owned by RadialMenu (Sector highlighting)
    AutoTarget active → Shared (Directional Snap Aim)
    none              → Owned by CursorEngine (Free mouse movement)
Controller Detection Flow (ControllerService.cs)
To accurately display the brand badge (Xbox vs PlayStation) despite wrapper tools like DS4Windows masking the input protocol, RagnaController uses a WMI hardware query.
code
Text
ControllerService.DetectController()
    │
    └── Try XInput slots 0–3
           │
           └── If connected:
                  │
                  └── DetectControllerType() ← WMI query Win32_PnPEntity
                         │
                         ├── Contains VID_054C + PID_0CE6/0DF2 → "PS5"
                         ├── Contains VID_054C + PID_05C4/09CC → "PS4"
                         ├── Contains VID_057E + PID_2009      → "Switch"
                         ├── Contains VID_2DC8                 → "8BitDo"
                         ├── Contains VID_046D                 → "Logitech"
                         └── fallback                          → "Xbox"
Startup Sequence (App.xaml.cs & MainWindow.xaml.cs)
code
Text
1. App.OnStartup()        ← Global exception handlers attached.
2. StartWorkflow()        ← Async startup wrapper.
3. SplashWindow.Show()    ← 3-second visual display.
4. Voice Synthesis        ← Triggers "Ragna Controller" voice greeting.
5. MainWindow.Init()
      ├── RegisterGlobalHotkeys()
      ├── SubscribeEngineEvents()
      ├── PopulateProfiles()
      ├── ApplySettings()
      ├── if AutoStart → _engine.Start()
      ├── if StartInMiniMode → SwitchToMiniMode()
      └── CheckForUpdatesAsync() (Fires silently in background)
6. Splash.FadeAndClose()  ← Graceful transition to MainWindow.
