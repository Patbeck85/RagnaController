# RagnaController — Testing Checklist

Use this document before every release. Check off each item manually.

---

## 0. Build Verification

- [ ] `dotnet build` → zero errors, zero warnings
- [ ] EXE launches without crash
- [ ] Splash screen appears for ~3 seconds, then fades out
- [ ] MainWindow opens and shows "NO CONTROLLER" state correctly

---

## 1. Startup & Splash

- [ ] Double-click EXE → splash shows immediately (not after a delay)
- [ ] Progress bar animates left-to-right over 3 seconds
- [ ] Status text cycles: *Initializing → Loading profiles → Starting engines → Connecting controller → Ready*
- [ ] Logo pulses with neon glow throughout
- [ ] MainWindow appears as splash fades (no black flash)
- [ ] App icon visible in taskbar and title bar (icon.ico embedded)

---

## 2. Controller Detection

**Xbox controller:**
- [ ] Plug in → badge shows **XBOX** (green)
- [ ] Unplug → badge shows **NO CONTROLLER** (grey) within ~2 seconds
- [ ] Reconnect → badge shows **XBOX** again

**PS4/PS5 (via DS4Windows in XInput mode):**
- [ ] Badge shows **PS4** or **PS5** (blue)

**Other brands (if available):**
- [ ] 8BitDo in XInput mode → **8BITDO** (orange)
- [ ] Logitech F310/F710 in XInput mode → **LOGITECH** (cyan)

**Battery display:**
- [ ] Wired controller → 🔌 WIRED in header
- [ ] Wireless full → 🔋 FULL (green)
- [ ] Wireless low → ⚠ LOW (red)

---

## 3. Movement Engine (Left Stick)

- [ ] Pushing stick → character walks toward cursor position
- [ ] Release stick → character stops (with coast frames if configured)
- [ ] Deadzone: small stick movement does nothing until threshold
- [ ] Action RPG Mode ON: holding stick holds the click continuously
- [ ] Action RPG Mode OFF: each new direction fires a fresh click
- [ ] Saving Action RPG Mode toggle persists on profile reload

---

## 4. Cursor Engine (Right Stick)

- [ ] Right stick moves cursor smoothly
- [ ] Cursor stops when stick centers (no drift with default deadzone)
- [ ] **SELECT** button → PRECISION badge appears in header
- [ ] In Precision Mode: cursor noticeably slower (÷3)
- [ ] SELECT again → PRECISION badge disappears, speed restored

---

## 5. Layer System

For each layer, hold the modifier and verify all 4 face buttons fire correct keys:

| Hold | Press | Expected key |
|---|---|---|
| (none) | X | Hold Alt |
| L1 | A | F1 |
| L1 | B | F2 |
| L1 | X | F3 |
| L1 | Y | F4 |
| R1 | A | F5 |
| R1 | B | F6 |
| R1 | X | F7 |
| R1 | Y | F8 |
| L2 | A | F9 |
| L2 | B | F10 |
| L2 | X | F11 |
| L2 | Y | F12 |
| R2 | A | Ctrl+F1 |

- [ ] Layer badge in header updates when modifier held (orange for L1/R1, red for L2/R2)
- [ ] Releasing modifier → badge returns to BASE (cyan)
- [ ] R3 with no special engine active → double-click fires
- [ ] START + D-Pad ↑ → next profile selected
- [ ] START + D-Pad ↓ → previous profile selected (wraps around)

---

## 6. Turbo System

- [ ] Standard: hold button → key fires repeatedly at fixed rate
- [ ] Burst: first press fires 3 rapid shots, then settles to normal rate
- [ ] Rhythmic: interval visibly varies (organic tempo)
- [ ] Adaptive: first press instant, follow-ups slower
- [ ] Turbo stops immediately on button release

---

## 7. AutoTarget Engine (Melee)

- [ ] L3 press → engine activates (status badge shows "MELEE")
- [ ] Engine Tabs to nearest monster
- [ ] Right-clicks to engage (Seeking → Engaged)
- [ ] Fires attack key at configured interval
- [ ] Right stick + R1: directional aim moves cursor
- [ ] Target dies → re-tab cycle starts (if AutoRetarget enabled)
- [ ] L3 again → engine deactivates (Idle)

---

## 8. Kite Engine (Ranged)

- [ ] L3 press (KiteEnabled profile) → engine activates
- [ ] Cycle: Lock → Attack (N shots) → Retreat → Pivot → Relock
- [ ] Hold R2 during Attack → stays and continues attacking (no retreat)
- [ ] Press L2 during Attack → triggers immediate retreat
- [ ] Engine status shows current phase label in Mini Mode

---

## 9. Mage Engine

- [ ] L3 press (MageEnabled profile) → engine activates
- [ ] Right stick aims → R3 places ground AoE at cursor
- [ ] Hold R2 → bolt mode: R3 locks target and fires bolt key
- [ ] Hold L2 → fires defensive key (e.g. Safety Wall)
- [ ] Defensive cooldown prevents spam

---

## 10. Support Engine

- [ ] L3 press (SupportEnabled profile) → engine activates
- [ ] Right stick aims at ally → R3 snaps + heals
- [ ] L3 again (while active) → self-heal fires
- [ ] R1 → Tab to next party member
- [ ] Hold R2 → ground mode cursor
- [ ] Auto-cycle (if enabled) → heals party on interval

---

## 11. Macro System

**Record:**
- [ ] Open Macro window → click Record → press 3–4 keys → click Stop
- [ ] Steps list shows recorded keys with timings
- [ ] Click Save → file created in `%AppData%\RagnaController\Macros\`

**Playback:**
- [ ] Load saved macro → play once → all keys fire in correct order
- [ ] Loop count 3 → plays exactly 3 times
- [ ] Infinite loop → plays until Stop pressed

**Editor:**
- [ ] Open macro in Editor → can add/delete/reorder steps
- [ ] Delay values editable
- [ ] Optimize removes very short delays

---

## 12. Profile Library

- [ ] Open Profile Library → all 39 built-in profiles listed
- [ ] Search by name filters list in real time
- [ ] Load a profile → MainWindow profile combobox updates
- [ ] Export profile → JSON file saved
- [ ] Import JSON → profile appears in list
- [ ] Delete user profile → removed from list (built-ins cannot be deleted)

---

## 13. Profile Wizard

- [ ] Open Wizard → 4 steps progress correctly
- [ ] Step 1: name + class
- [ ] Step 2: engine selection
- [ ] Step 3: key bindings
- [ ] Step 4: review + create
- [ ] Created profile appears in combobox and can be loaded

---

## 14. Button Remapping

- [ ] Open Remap window with a profile loaded
- [ ] Change a base-layer button → fires new key in game
- [ ] Change a L1-layer button → fires new key when L1 held
- [ ] Enable turbo on a button → configurable interval and mode
- [ ] Clear a mapping → button fires nothing
- [ ] Save → changes persist after app restart

---

## 15. Global Hotkeys

- [ ] With 4+ profiles loaded: Ctrl+1 selects profile 1
- [ ] Ctrl+2 selects profile 2
- [ ] Ctrl+3 selects profile 3
- [ ] Ctrl+4 selects profile 4
- [ ] Minimize app → Ctrl+1 still switches profile (works in background)
- [ ] Hotkeys unregistered cleanly on close (no leftover Win32 handle)

---

## 16. System Tray

- [ ] Click minimize (—) → app disappears from screen, tray icon visible
- [ ] Double-click tray icon → app restored and focused
- [ ] Right-click tray icon → context menu shows "Show RagnaController" / "Exit"
- [ ] Exit from tray → app closes, tray icon disappears
- [ ] Tray icon uses app icon (not generic Windows icon)

---

## 17. Mini Mode

- [ ] Click 🪟 button → Mini Mode overlay appears (280×120 px), main window hides
- [ ] Overlay shows: profile name, engine state, status color (green=running, red=stopped)
- [ ] Drag overlay to any screen position
- [ ] Start/stop engine → overlay color updates in real time
- [ ] Click × on overlay → returns to main window

---

## 18. Settings Window

- [ ] Open Settings → all toggles show current state
- [ ] Toggle sound off → no audio feedback during skill fires
- [ ] Toggle rumble off → no haptic during skill fires
- [ ] Toggle ShowControllerViz off → stick dots hidden in header
- [ ] Change log level → logger respects new level
- [ ] Settings persist after app restart

---

## 19. Advanced Logger

- [ ] Switch to Log tab → messages visible
- [ ] Start/stop engine → log messages appear
- [ ] Click Clear → log display empties
- [ ] Click Export → SaveFileDialog opens → log file created at chosen path
- [ ] Session log file written to `%LocalAppData%\RagnaController\Logs\`

---

## 20. Update Checker

*(Requires GitHub repo URL set in UpdateChecker.cs)*
- [ ] On launch (after 3 s delay) → update check runs silently if up to date
- [ ] If newer version available → dialog offers download link
- [ ] Cancel → no crash, app continues normally

---

## 21. Unsaved Changes Guard

- [ ] Move a slider without saving → "SAVE" button appears
- [ ] Attempt to close → "Unsaved changes" dialog appears
- [ ] Choose Yes → profile saved, app closes
- [ ] Choose No → app closes without saving

---

## 22. Auto-Reconnect

- [ ] Start engine with controller → disconnect → "Controller disconnected – waiting…" shown
- [ ] Reconnect → "Running" state restored automatically (within ~2 s)

---

## 23. Window Position Memory

- [ ] Move window to corner → close app → reopen → window at same position
- [ ] Valid only within visible screen bounds (not restored off-screen)

---

## Performance Checklist

Open Task Manager while running:

- [ ] CPU usage < 5% with engine running
- [ ] Memory < 120 MB
- [ ] No memory growth over 5 minutes (no leak)
- [ ] Log tab shows avg tick < 2 ms, max tick < 8 ms

---

## Full Feature Checklist

- [ ] 39 profiles all load without error (check Library)
- [ ] All 4 combat engines activate/deactivate cleanly
- [ ] All 5 input layers map correctly
- [ ] All 4 turbo modes work
- [ ] Macro record + save + playback + edit works end-to-end
- [ ] Global hotkeys Ctrl+1–4 work minimized
- [ ] Tray icon: minimize / restore / exit
- [ ] Splash screen displays on every launch
- [ ] Zero German text visible anywhere in UI
- [ ] Zero crashes during 10-minute play session
