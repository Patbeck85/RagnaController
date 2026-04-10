# RagnaController — Feature Reference

**Tick rate:** 125 Hz (8 ms, ±0.5 ms jitter) · **Profiles:** 39 · **Controller brands:** 8

---

## Startup & UI Shell

### Obsidian & Gold UI
- Fully custom WPF theme: Glassmorphism panels, DropShadow effects, gold NeonGlow on active elements.
- Micro-animations on buttons (150 ms fade-in, 250 ms fade-out via `ColorAnimation` Storyboards).
- Tab bar active state: selected tab gets gold bottom-border via `TabButtonActive` style.
- All toolbar icons are WPF `Path` vector graphics (Feather-style, stroke-based) — crisp at any DPI, tinted by button `Foreground` binding.

### Focus Lock
- `GetForegroundWindow()` polled every ~500 ms (same counter as WindowTracker).
- If the foreground window is not the configured RO process, all controller input is suppressed.
- Status bar shows `⛔ FOCUS LOCK — switch to RO` in orange while blocked.
- Process name configurable in Settings with a file-picker (Browse button → `.exe` selected → filename without extension stored).
- Applies to Focus Lock **and** WindowTracker — one setting, both systems.

### Visual Deadzone Ring
- Red semi-transparent ellipse rendered behind the stick dot on both L-STICK and R-STICK visualisers.
- Diameter = `deadzone × 50 px` (visualiser is 50 × 50 px = ±1.0 stick range).
- Updates live on slider drag, profile load, and double-click reset.
- Makes stick-drift calibration visual and instant.

### Mini Mode (`MiniModeWindow`)
- 280 × 120 px always-on-top overlay showing profile name and engine state.
- Right-click: toggle click-through (`WS_EX_TRANSPARENT`) — turns border blue when active.
- **Emergency escape:** press `Start + Back` on the controller to restore the main window from any state, including click-through.
- Tooltip explains the shortcut.

---

## Window Tracking (`WindowTracker`)

Finds the RO client window and computes its exact centre in physical screen pixels, accounting for DPI scaling and window position.

**Win32 call chain:**
1. `GetForegroundWindow()` — check if active window is RO (priority for multi-client)
2. `GetProcessById()` — confirm process name matches setting
3. `GetClientRect()` — inner drawable area (excludes title bar and borders)
4. `ClientToScreen()` — convert client origin to screen coordinates
5. `MonitorFromWindow()` — find which monitor the window is on
6. `GetDpiForMonitor()` — get that monitor's actual DPI (e.g. 192 on 4K @ 200%)
7. Scale: `physicalPixel = logicalPixel × (monitorDPI / 96)`

**Multi-client behaviour:** When using Window Switcher, `WindowTracker` always prefers the current foreground window. After a switch, `ForceRefreshOnNextTick()` triggers an immediate re-centre (within 200 ms) so the new client's monitor and DPI are used.

**Status display:** Tick-latency field shows `2.3ms | RO 1.50x DPI` or `2.3ms | RO: not found`.

---

## Movement Engine (`MovementEngine`)

Left stick drives click-to-move via Windows `SendInput` left-click. Centre position comes from `WindowTracker` (DPI-corrected) with screen-centre fallback.

| Setting | Range | Effect |
|---|---|---|
| Deadzone | 0.0–0.5 | Dead zone radius (visualised by red ring) |
| Curve | 1.0–4.0 | Non-linear sensitivity curve |
| Action Speed | 1.0–10.0 | Leash radius for Action RPG mode |
| Max Cursor Speed | px/s | Cursor top speed |

**Action RPG Mode:** holds left-click while moving vs. single click per direction change.

**Loot Vacuum** (`LB + RB`): spirals cursor around character centre, one click every 50 ms (throttled to prevent GC pressure from `Task.Run` spam).

---

## 5-Layer Input System

| Layer | Modifier |
|---|---|
| Base | — |
| L1 | Hold Left Bumper |
| R1 | Hold Right Bumper |
| L2 | Hold Left Trigger |
| R2 | Hold Right Trigger |

**Fixed shortcuts (not remappable):**
- `X hold` → Alt hold (show ground items)
- `R3` → double-click
- `Start + D-Pad ↑/↓` → profile quick-switch
- `Start + Back` → restore main window from Mini-Mode
- `Back + L1` → Voice-to-Chat
- `Back + R1` → Daisy Wheel keyboard
- `LT + RT` → Radial emote menu
- `L3 + R3` → Panic heal

---

## Combat Engines

Toggle with **L3**. Only one engine owns the right stick at a time.

### AutoTargetEngine — Melee
FSM: **Idle → Seeking → Engaged → Attacking**

**Smart Skill Auto-Aim (Cursor Juggling):** when a target is locked and a skill button is pressed, the engine atomically: saves cursor position → snaps to `_lockPos` → fires skill + click → restores cursor. Completes in ~12 ms via `SemaphoreSlim`-guarded `Task.Run`.

### KiteEngine — Ranged
FSM: **Lock → Attack → Retreat → Pivot → Relock**

Automates hit-and-run for Archers/Hunters. Hold R2 to hold ground, L2 to force immediate retreat.

### MageEngine — Mage / Wizard / Sage
- Ground-target: right stick aims, R3 places spell.
- Bolt mode (hold R2): R3 locks target, auto-fires bolts.

### SupportEngine — Priest / High Priest
- Right stick aims at ally, R3 snaps + casts Heal.
- Tab-cycles through party automatically or manually.

---

## Voice-to-Chat (`VoiceChatService`)

**Trigger:** `Back + L1`

Uses Windows Speech Recognition (`System.Speech`). Listens until silence, then opens RO chat, types the recognised string as Unicode key events, and submits. `SendChatString` is serialised with `_isChatting` flag — rapid double-fires queue instead of interleaving.

---

## Daisy Wheel Keyboard (`DaisyWheelWindow`)

**Trigger:** `Back + R1`

Circular on-screen keyboard. Left stick selects sector, A/B/X/Y types characters. L3 = Backspace, R3 = Space, Start = submit. Y-axis correctly inverted (`Atan2(-ly, lx)`) to match physical stick direction.

---

## Radial Emote Menu (`RadialMenuWindow`)

**Trigger:** Hold `LT + RT`

Up to 8 emote slots with downloaded RO emote images. Window instance is reused (`Visibility.Hidden`/`Visible`) — no WPF transparency re-init on rapid trigger presses.

---

## Macro System (`MacroRecorder`)

- Record any key + click sequence with real timing (minimum step 50 ms).
- Editor: add/remove/reorder steps, adjust delays, Optimize (remove steps < 30 ms).
- Storage: `%AppData%\RagnaController\Macros\*.json`
- Loop: 1 = once, 0 = infinite, N = N times.

---

## Profile System

39 built-in profiles — all RO classes from Novice to Transcended.

**Profile Library:** search, filter by class, load, export JSON, import JSON, delete.

**Profile Wizard:** 4-step guided creation.

**Duplicate protection:** `AddAndSave` replaces existing profiles with the same name instead of duplicating.

**Corruption recovery:** `Load()` falls back to `.bak.json` if the primary `.json` is unreadable (e.g. after a crash during save).

---

## Feedback System (`FeedbackSystem`)

Rumble patterns check `_rumbleEnabled` after every `await Task.Delay` — engine pause or disconnect stops vibration immediately (no ghost rumble).

---

## Input Simulator (`InputSimulator`)

- `INPUT` struct uses `LayoutKind.Explicit` with `FieldOffset(8)` for correct 64-bit alignment.
- `SendChatString` serialised with `volatile bool _isChatting` — prevents key interleaving from concurrent callers.
- Detects Wine/Proton at startup and adjusts key/click delays accordingly.

---

## Performance

| Item | Detail |
|---|---|
| Timer resolution | `timeBeginPeriod(1)` on startup — 1 ms Windows scheduler precision |
| Tick jitter | ±0.5 ms (was ±5 ms with default 15.6 ms resolution) |
| Perf log threshold | 25 ms (was 8 ms — was flooding log at normal CPU load) |
| Vacuum click rate | 1 click/50 ms (was 1 click/8 ms — was spawning ~125 Tasks/sec) |
| WindowTracker cadence | Refresh every 500 ms; forced immediately after window switch |

---

## Settings (`Models/Settings.cs`)

Persisted to `%AppData%\RagnaController\settings.json`.

| Setting | Default | Description |
|---|---|---|
| FocusLockEnabled | true | Pause engine when RO loses focus |
| FocusLockProcess | `ragexe` | Process name (set via Browse button) |
| AutoStart | false | Start engine on launch |
| StartInMiniMode | false | Launch to Mini-Mode overlay |
| SoundEnabled | true | Audio feedback |
| RumbleEnabled | true | Haptic rumble |
| LogLevel | Info | Debug / Info / Warning / Error |
