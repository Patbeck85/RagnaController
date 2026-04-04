# RagnaController — Feature Reference

**Tick rate:** 125 Hz (8 ms) · **Profiles:** 39 · **Controller brands:** 8

---

## Startup & UI Shell

### Splash Screen (`SplashWindow`)
- Shows for 3 seconds on every launch.
- Fade-in (0.5 s), animated Neon progress bar (cyan → purple with glow).
- Logo pulse animation, status text cycles: *Initializing → Loading profiles → Starting engines → Connecting controller → Ready*.
- Fades out (0.3 s) as MainWindow opens simultaneously.
- Wrapped in try/catch — startup errors shown as MessageBox, app exits cleanly.

### System Tray (`InitTrayIcon`)
- `NotifyIcon` with embedded `Assets/icon.ico`.
- Double-click → restores and activates MainWindow.
- Right-click menu: **Show RagnaController** / **Exit**.
- Disposed on `Window_Closing` — no handle leaks.

### Global Hotkeys
- Registered via Win32 `RegisterHotKey` on `SourceInitialized`.
- Unregistered via `UnregisterHotKey` on `Window_Closing`.
- **Ctrl+1–4** → switch to profile slots 1–4.
- Works even when the window is minimized to the system tray.

### Mini Mode (`MiniModeWindow`)
- 280×120 px always-on-top overlay.
- Shows: current profile name, engine state, active/inactive indicator, combat phase.
- Draggable; click X → securely returns to full MainWindow.
- Live-updated every UI tick via `UpdateState()`.

---

## Movement Engine (`MovementEngine`)

Left stick drives click-to-move via Windows `SendInput` left-click.

| Setting | Range | Effect |
|---|---|---|
| Deadzone | 0.0–1.0 | Dead zone radius before stick registers |
| Curve | 1.0–4.0 | Non-linear sensitivity curve |
| Sensitivity | 1–10 | Overall movement speed multiplier |
| Coast Frames | 0–10 | Extra move ticks after stick release (momentum) |
| Action Speed | 1.0–10.0 | Leash radius for Action RPG mode |

**Action RPG Mode** (toggleable): holds left-click while moving vs. single click per direction change.

---

## Cursor Engine (`CursorEngine`)

Right stick moves the OS cursor when no special engine owns it.

| Setting | Description |
|---|---|
| Max Speed | Pixels/second at full deflection |
| Deadzone | Dead zone before cursor moves |
| Curve | Non-linear acceleration |

**Precision Mode** (SELECT button): cursor speed divided by ~3 for fine targeting.  
Badge shown in header. Toggling triggers haptic feedback.

---

## 5-Layer Input System

| Layer | Modifier | Example |
|---|---|---|
| Base | — | A → left-click |
| L1 | Hold Left Bumper | L1+A → F1 |
| R1 | Hold Right Bumper | R1+A → F5 |
| L2 | Hold Left Trigger (>50) | L2+A → F9 |
| R2 | Hold Right Trigger (>50) | R2+A → Ctrl+F1 |

**Fixed base-layer actions (not remappable):**
- **X hold** → hold Alt (show ground items)  
- **R3** → double-click — suppressed when any special engine is active  
- **SELECT** → Precision Mode toggle  
- **START + D-Pad ↑/↓** → profile quick-switch ±1  

---

## Turbo System

Configured per-button in **Button Remap** window.

| Mode | Behaviour |
|---|---|
| **Standard** | Fixed-interval repeat while held |
| **Burst** | 3 rapid presses on first hold, then standard rate |
| **Rhythmic** | Interval oscillates via sine wave — organic, unpredictable timing |
| **Adaptive** | Instant first press, follow-ups slow to ~attack animation length |

Minimum interval: 30 ms. Turbo runs safely inside the 125 Hz tick loop.

---

## Combat Engines

Toggle on/off with **L3** (when enabled in profile). Only one engine owns the right stick at a time.

### AutoTargetEngine — Melee

FSM: **Idle → Seeking → Engaged → Attacking**

- **Seeking:** Tab key every `TabCycleMs` ms  
- **Engaged:** right-click to lock target  
- **Attacking:** fires `AutoAttackKeyVK` every `AutoAttackIntervalMs` ms  
- Right stick + R1 = directional snap aim  
- `AutoRetargetEnabled`: re-enters Seeking after target dies  

### KiteEngine — Ranged

FSM: **Lock → Attack → Retreat → Pivot → Relock**

- **Lock:** right-clicks target  
- **Attack:** fires `KiteAttackKeyVK`, counts up to `KiteAttacksBeforeRetreat`  
- **Retreat:** moves cursor back `KiteRetreatCursorDist` px over `KiteRetreatDurationMs` ms  
- **Pivot:** swings cursor back toward enemy over `KitePivotDurationMs` ms  
- **Hold R2** = hold-ground (skip retreat phase)  
- **Press L2** = force immediate retreat  

### MageEngine — Mage / Wizard / Sage

**Ground-target mode** (default):
- Right stick aims cursor
- R3 fires `MageGroundSpellKeyVK`

**Bolt mode** (hold R2):
- R3 locks target, auto-fires `MageBoltKeyVK` with `MageBoltCastDelayMs` delay

**Defensive** (hold L2):
- Fires `MageDefensiveKeyVK` with cooldown (`MageDefensiveCooldownMs`)

### SupportEngine — Priest / High Priest

- Right stick → aim cursor at ally, R3 → snap + heal (`HealKeyVK`)  
- L3 while active → self-heal (`SelfHealKeyVK`)  
- R1 → Tab cycle through party (`SupportTabCycleMs`)  
- Hold R2 → ground mode for Sanctuary (`SanctuaryKeyVK`)  
- `SupportAutoCycleEnabled`: heals party on interval automatically  

---

## Macro System (`MacroRecorder`)

**Recording:**
- Record any key + click sequence with real timing
- Minimum step delay 50 ms (noise filter)

**Storage — `%AppData%\RagnaController\Macros\`:**
- `MacroRecorder.SaveMacro(macro)` — saves as JSON, returns file path  
- `MacroRecorder.LoadMacro(path)` — deserializes JSON → `Macro`  
- `MacroRecorder.GetSavedMacroFiles()` — lists all `.json` files in folder  
- `MacroRecorder.DeleteMacro(path)` — deletes file  

**Playback:**
- Loop count: 1 = once · 0 = infinite · N = N times  
- Runs inside engine tick loop — non-blocking  

**Macro Editor:**
- Add / remove / reorder steps  
- Adjust individual step delays  
- Speed-up (`÷ multiplier`) · Slow-down (`× multiplier`) · Optimize (removes steps < 30 ms)  

**Binding:** Assign saved macro to any button or layer combo in **Button Remap** window.

---

## Profile System

**39 built-in profiles** — all RO classes from Novice to Transcended.  
Each defines: engine settings, cursor/movement tuning, F-key recommendations, class tips.

**Profile Library window:** search by name, filter by class, load, export JSON, import JSON, delete.

**Profile Wizard (4 steps):** Name + class → Engine selection → Key bindings → Review + create.

**Storage:**  
- Built-ins: code-defined, never written to disk unless modified  
- User saves: `%AppData%\RagnaController\Profiles\`  

---

## Controller Detection (`ControllerService`)

WMI query: `Win32_PnPEntity WHERE PNPClass = 'HIDClass'` — checks device name and HardwareID.

| Brand | VID | PIDs |
|---|---|---|
| Sony PS5 | 054C | 0CE6 (DualSense), 0DF2 (Edge) |
| Sony PS4 | 054C | 05C4 (v1), 09CC (v2), 0BA0 (Back Button) |
| Nintendo | 057E | 2009 (Switch Pro) |
| 8BitDo | 2DC8 | All models |
| Logitech | 046D | C21D (F310), C21F (F710), C218 (XInput) |
| Razer | 1532 | All gamepads |
| Thrustmaster | 044F | All XInput |

Unrecognised XInput device → labelled **Xbox**.  
Battery polled every ~10 s → displayed in header (🔋 FULL / 🔋 MEDIUM / ⚠ LOW / ❌ EMPTY / 🔌 WIRED).

---

## Feedback System (`FeedbackSystem`)

**Rumble events (`FeedbackType`):**  
EngineStart, EngineStop, CombatModeOn, TargetLocked, SkillFired, KiteRetreat, HealCast, LowSP, ProfileSwitched, PrecisionModeOn, Warning

**Sound:** Windows `SystemSounds` — no audio files required.  
**Toggle:** sound and rumble independently in Settings window.

---

## Advanced Logger (`AdvancedLogger`)

- Session file: `%LocalAppData%\RagnaController\Logs\session_YYYY-MM-DD_HH-mm-ss.log`
- Tick performance: sampled every tick, last 1000 stored in ring buffer
- Properties: `AverageTickTimeMs`, `MaxTickTimeMs`, `SessionDuration`, `TotalEvents`
- `ExportSession()` → writes export file, returns path (used by Export Log button)
- `ClearBuffer()` → clears in-memory log display
- Log level: Debug · Info · Warning · Error (configurable in Settings)

---

## Settings (`Models/Settings.cs`)

Persisted to `%AppData%\RagnaController\settings.json`.

| Setting | Default | Description |
|---|---|---|
| AutoStart | false | Start engine immediately on launch |
| StartMinimized | false | Launch to system tray |
| StartInMiniMode | false | Launch directly to Mini Mode overlay |
| SoundEnabled | true | Audio feedback |
| RumbleEnabled | true | Haptic rumble feedback |
| ShowControllerViz | true | Stick visualisation dots in header |
| LogLevel | Info | Minimum log level written to file |
| LastProfileName | (last used) | Auto-selected on next launch |
| WindowPosition | (last position) | Restored on next launch |