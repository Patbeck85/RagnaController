# RagnaController — Testing Checklist (v1.2.0)

Use this document before every release to ensure maximum stability. Check off each item manually.

---

## 0. Build Verification

- [ ] Run `START.bat` and select `[4] Build All`.
- [ ] Verify that the script successfully downloads the .NET 8 SDK (if missing).
- [ ] Verify that 3 ZIP files are generated in the `publish/` folder with the correct version number (e.g., `RagnaController_v1.2.0_Win_Standalone.zip`).
- [ ] Extract the `Win_Standalone` zip and launch the EXE without .NET installed on the machine.
- [ ] Splash screen appears for ~3 seconds, then fades out smoothly.
- [ ] MainWindow opens and shows "NO CONTROLLER" state correctly (if no gamepad is connected).

---

## 1. Controller Detection & Hardware Sanity

**Xbox controller:**
- [ ] Plug in → badge shows **XBOX** (green).
- [ ] Unplug → badge shows **NO CONTROLLER** (grey) within ~2 seconds.
- [ ] Reconnect → badge shows **XBOX** again and engine resumes if AutoStart was enabled.

**PS4/PS5 (via DS4Windows in XInput mode):**
- [ ] Badge shows **PS4** or **PS5** (blue).

**Battery display:**
- [ ] Wired controller → 🔌 WIRED in header (Full green bar).
- [ ] Wireless full → 🔋 FULL (green).
- [ ] Wireless low → ⚠ LOW (red).

**Hardware Sanity Check (Freeze Protection):**
- [ ] Connect a controller. Attempt to press D-Pad UP and D-Pad DOWN simultaneously (or use software to send this signal). The UI should ignore the impossible input gracefully and **not freeze**.

---

## 2. Movement & Cursor Engines

- [ ] **Left Stick (Movement):** Pushing stick → character walks toward cursor position.
- [ ] **Release stick:** Character stops (with coast frames if configured).
- [ ] **Action RPG Mode ON:** Holding stick holds the click continuously.
- [ ] **Action RPG Mode OFF:** Each new direction fires a fresh click.
- [ ] **Right Stick (Cursor):** Moves cursor smoothly. Stops completely when stick centers (no drift with default deadzone).
- [ ] **Precision Mode:** Press SELECT → PRECISION badge appears in header. Cursor is noticeably slower (÷3). Press SELECT again to restore speed.

---

## 3. Layer System

For each layer, hold the modifier and verify all 4 face buttons fire correct keys (checked via Notepad or the Macro window):

| Hold | Press | Expected key |
|---|---|---|
| (none) | X | Hold Alt |
| L1 | A | F1 |
| R1 | B | F6 |
| L2 | X | F11 |
| R2 | Y | Ctrl+F4 |

- [ ] Layer badge in header (bottom right) updates when modifier is held (e.g., "L1+" / "R1+").
- [ ] Releasing modifier → badge returns to BASE.
- [ ] START + D-Pad ↑ → next profile selected.
- [ ] START + D-Pad ↓ → previous profile selected (wraps around).

---

## 4. Advanced Pro Features

- [ ] **Radial Menu (L2+R2):** Hold triggers, verify radial UI appears instantly (Emotes are now built-in, no download needed!). Right stick highlights emojis. Release triggers to close and send the command to chat.
- [ ] **Loot Vacuum (L1+R1):** Hold bumpers, verify cursor spirals around the center rapidly while spamming Left-Click.
- [ ] **Panic Heal (L3+R3):** Click both sticks, verify a strong rumble is felt and log shows "PANIC" execution.
- [ ] **Daisy Wheel (Back+R1):** Press Select and Right Bumper to open the circular keyboard. Input text using A/B/X/Y and close with Start.

---

## 5. Combat Engines

- **AutoTarget (Melee):**
  - [ ] L3 press → engine activates (status badge shows "MELEE").
  - [ ] Tabs to target, right clicks to lock.
  - [ ] Fires attack key at configured interval.
- **Kite (Ranged):**
  - [ ] L3 press (KiteEnabled profile) → engine activates.
  - [ ] Cycle: Lock → Attack → Retreat → Pivot → Relock.
  - [ ] Hold R2 during Attack → stays and continues attacking (skips retreat).
  - [ ] Press L2 during Attack → triggers immediate retreat.
- **Mage:**
  - [ ] L3 press (MageEnabled profile) → engine activates.
  - [ ] Right stick aims → R3 places ground AoE.
  - [ ] Hold R2 → locks target and fires bolt.
  - [ ] Hold L2 → fires defensive key (with cooldown).
- **Support:**
  - [ ] L3 press (SupportEnabled profile) → engine activates.
  - [ ] Right stick aims at ally → R3 snaps + heals.
  - [ ] L3 again (while active) → self-heal fires.
  - [ ] R1 → Tab to next party member.

---

## 6. Turbo & Macro System

**Turbo System:**
- [ ] Standard: hold button → key fires repeatedly at fixed rate.
- [ ] Burst: first press fires 3 rapid shots, then settles to normal rate.
- [ ] Turbo stops immediately on button release.

**Macro Recorder:**
- [ ] Open Macro window → click Record → press 3–4 keys → click Stop.
- [ ] Steps list shows recorded keys with timings.
- [ ] Click Save → file created in `%AppData%\RagnaController\Macros\`.
- [ ] Load saved macro → play once → all keys fire in correct order.
- [ ] Open macro in Editor → can add/delete/reorder steps.
- [ ] Optimize removes delays < 30 ms safely without deleting keypresses.

---

## 7. Profile Library & Wizard

- [ ] Open Profile Library → all 39 built-in profiles listed.
- [ ] Search by name filters list in real time.
- [ ] Export profile → JSON file saved.
- [ ] Import JSON → profile appears in list.
- [ ] Open Wizard → 4 steps progress correctly.
- [ ] Created wizard profile appears in combobox and can be loaded.

---

## 8. Stability & Anti-Freeze Protection (v1.2.0 Core Test)

- [ ] Move mouse wildly with the right stick while RO (or any admin-level game/app) is open. **No freezing should occur.** (Asynchronous `SendInput` test).
- [ ] Switch to a different app (e.g., Discord). The controller should remain active and responsive (Focus Lock was removed by design to support renamed RO executables).
- [ ] Open Settings, change "Log Level" to Debug, check the "Log" tab. Ensure no exceptions are spammed while the controller is idle.

---

## 9. UI Shell & Mini Mode

- [ ] **Mini Mode:** Click `Mini` button → Mini Mode overlay appears (280×120 px), main window hides.
- [ ] Start/stop engine → overlay color updates in real time.
- [ ] **Mini Mode Close Fix:** Click the `X` on the mini overlay → it cleanly closes the overlay and restores the Main Window.
- [ ] Move window to corner → close app → reopen → window at same position.
- [ ] Click minimize (—) → app disappears from screen, tray icon visible.
- [ ] Double-click tray icon → app restored and focused.

---

## 10. Final Performance Checklist

Open Task Manager while running the app and actively using the controller:

- [ ] CPU usage < 5% with engine running.
- [ ] Memory < 120 MB.
- [ ] No memory growth over 5 minutes (no memory leak in the 8ms tick loop).
- [ ] Log tab shows `avg tick < 2 ms`, `max tick < 8 ms`.
- [ ] Zero crashes during a 10-minute active play session.