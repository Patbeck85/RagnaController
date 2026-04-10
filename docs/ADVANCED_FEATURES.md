# Advanced Pro Features Guide (v1.2.0)

---

## 1. Smart Skill Auto-Aim (Cursor Juggling)

**What it solves:** In Action RPG mode the cursor sits in front of the character. Single-target skills require aiming at the monster — normally that means stopping movement.

**How it works:** When `AutoTargetEngine` has a target locked and a skill button is pressed:
1. `_lockPos` (target cursor position) is saved by the engine every tick.
2. A `Task.Run` guarded by `SemaphoreSlim` atomically:
   - Saves current walking cursor position.
   - Snaps cursor to `_lockPos`.
   - Fires the skill key + left-click.
   - Snaps cursor back.
3. Total time: ~12 ms. Invisible to the player.

---

## 2. Focus Lock (Discord / Desktop Protection)

**What it solves:** Controller on the table, accidentally press R2, macros fire into Discord.

**How it works:** `GetForegroundWindow()` is called every ~500 ms. If the active window's process name does not match the configured game client, all `SendInput` calls are suppressed.

**Configuration:** Settings → Browse button → select your RO `.exe`. The filename without extension is stored (e.g. `ragexe`, `custom2025`). Works for Focus Lock and Window Tracker with the same value.

**Status:** Orange `⛔ FOCUS LOCK — switch to RO` in the status bar while blocked. StatusDot turns amber.

---

## 3. DPI-Aware Window Tracking

**What it solves:** On 4K monitors with 150% Windows scaling, `SendInput` mouse coordinates are in physical pixels but `GetSystemMetrics` returns logical pixels — the cursor drifts from the character.

**How it works:** `WindowTracker` calls:
- `GetClientRect` → inner drawable area
- `ClientToScreen` → real screen origin
- `MonitorFromWindow` + `GetDpiForMonitor` → actual DPI of that monitor
- Scale factor = `monitorDPI / 96` applied to all coordinates

**Multi-client:** `WindowTracker.Refresh()` always checks the foreground window first. After a Window Switch, `ForceRefreshOnNextTick()` triggers an immediate re-centre so the new client's monitor/DPI is used within 200 ms.

---

## 4. Multi-Client Window Switcher

**Trigger:** Map any button to `Action Type: Switch Window`.

**How it works:** `WindowSwitcher.Toggle(targetProcess)` runs on a background `Task.Run` — `AttachThreadInput` never blocks the WPF UI thread, even if the target client is frozen during a loading screen.

**Multi-monitor:** After the switch, `WindowTracker` detects the new foreground window, reads its monitor's DPI, and recalculates the character centre. Movement, loot vacuum, and Smart Skill all use the correct position within the next 500 ms tick.

---

## 5. Voice-to-Chat

**Trigger:** `Back + L1`

Activates local Windows Speech Recognition. Speak naturally. The app types the recognised text into RO chat using Unicode `SendInput` events. Multiple rapid activations queue safely — `_isChatting` flag prevents keystroke interleaving.

---

## 6. Daisy Wheel Keyboard

**Trigger:** `Back + R1`

Circular on-screen keyboard. Left stick selects sector, face buttons type. L3 = Backspace, R3 = Space, Start = submit to RO chat. Y-axis matches physical controller direction (inverted in `Atan2` calculation).

---

## 7. Radial Emote Menu

**Trigger:** Hold `LT + RT`

Up to 8 emote/item slots with downloaded RO emote images. Point right stick, release triggers to execute. The window is kept alive in memory and shown/hidden via `Visibility` — no WPF transparency reinitialisation on rapid button presses.

Run `GetEmotes.ps1` to download all 60 RO emotes from iROWiki (4× nearest-neighbour upscaled).

---

## 8. Panic Emergency Heal

**Trigger:** `L3 + R3` (both thumbsticks simultaneously)

Bypasses all delays. Triggers haptic warning rumble and fires `F4` 10 times in 100 ms.

---

## 9. Loot Vacuum

**Trigger:** `LB + RB`

Spirals the cursor around the character centre in an expanding/contracting pattern, clicking every 50 ms to pick up all drops. Centre position is DPI-corrected from `WindowTracker`.

---

## 10. Combo Engine

Class-aware sequential skill chains. Each step has its own delay (Pre-Renewal vs Renewal timing). Configured in the **Combo** window.

---

## 11. Timer Precision

`timeBeginPeriod(1)` is called on engine startup, setting the Windows multimedia timer to 1 ms resolution. This reduces `DispatcherTimer` jitter from the default ±5 ms to ±0.5 ms — analog stick movement is noticeably smoother. `timeEndPeriod(1)` is called on shutdown to restore the OS default.
