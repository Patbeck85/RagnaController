# RagnaController — Release Testing Checklist v1.2.0

Run through every section before tagging a release. Check off each item manually.

---

## 1. Build & Startup

- [ ] `build.bat` completes without errors or warnings (target: option 1 — Framework-dependent)
- [ ] App launches and splash screen plays (gold pulse, fades into MainWindow)
- [ ] Admin warning banner appears if launched **without** admin rights
- [ ] Version number in title bar reads `1.2.0`
- [ ] No unhandled exception on cold start (fresh AppData)

---

## 2. Focus Lock

- [ ] Settings → Focus Lock checkbox is checked by default
- [ ] Browse button opens `.exe` file picker; selecting `ragexe.exe` sets field to `ragexe`
- [ ] Save settings; start engine; alt-tab to Notepad — status bar shows `⛔ FOCUS LOCK — switch to RO` in orange
- [ ] Controller input does **not** type or move mouse while locked
- [ ] Alt-tab back to RO — engine resumes within 500 ms, orange indicator clears
- [ ] Uncheck Focus Lock in settings → engine runs regardless of foreground window

---

## 3. Visual Deadzone Ring

- [ ] Both L-STICK and R-STICK visualisers show a red semi-transparent ring
- [ ] Moving the Deadzone slider → ring resizes live
- [ ] Double-clicking the `STICK DEADZONE` label resets slider **and** ring size
- [ ] Load a different profile → ring updates to that profile's deadzone value

---

## 4. Window Tracking & DPI

- [ ] Start engine with RO open → tick-latency field shows `X.Xms | RO 1.00x DPI` (or correct scale)
- [ ] Move RO window to a different monitor → within 500 ms DPI label updates
- [ ] Analog movement cursor lands on character, not offset from it
- [ ] With RO not running → tick field shows `X.Xms | RO: not found`

---

## 5. Multi-Client Window Switch

- [ ] Open two RO clients; map a button to `Switch Window`; press it → second client comes to front
- [ ] Press again → first client comes to front
- [ ] After each switch, within ~500 ms, cursor movement centres correctly on the active client
- [ ] Switching while on a 4K monitor does **not** cause cursor drift

---

## 6. Mini-Mode & Click-Through Trap

- [ ] Click `Mini` button → compact overlay appears, main window hides
- [ ] Right-click overlay → border turns blue, window becomes click-through
- [ ] Press `Start + Back` on controller → main window restores, click-through deactivated
- [ ] Tooltip on Mini overlay reads: *"Right-click: toggle click-through... Press Start+Back..."*

---

## 7. Analog Movement & Combat

- [ ] Left stick moves character smoothly; no stuttering at low deflection
- [ ] Deadzone ring visually matches the physical dead zone of the stick
- [ ] `L3` activates Melee engine; `L3` again deactivates
- [ ] With target locked, pressing a skill button snaps cursor to target and back (Smart Skill)
- [ ] `LB + RB` → Loot vacuum spirals cursor and clicks approximately every 50 ms
- [ ] `L3 + R3` → Panic heal fires F4 multiple times

---

## 8. Voice-to-Chat

- [ ] Press `Back + L1` → microphone activates
- [ ] Speak a short phrase → text is typed into RO chat and submitted
- [ ] Rapid double-press does **not** produce interleaved characters in chat

---

## 9. Daisy Wheel

- [ ] Press `Back + R1` → Daisy Wheel overlay opens
- [ ] Push stick **UP** → top sector highlights (not bottom — Y-axis inversion fix)
- [ ] Select a character; press Start → text submitted to RO chat
- [ ] Press B with no sector selected → wheel closes cleanly
- [ ] Rapidly press Start twice → no crash

---

## 10. Radial Emote Menu

- [ ] Hold `LT + RT` → radial menu opens
- [ ] Point right stick → correct sector highlights
- [ ] Release triggers → emote command sent to RO chat
- [ ] Rapidly hold/release triggers 10× in quick succession → no flicker, no crash
- [ ] Run `GetEmotes.ps1` → emote images appear in overlay

---

## 11. Profile System

- [ ] Import a `.json` profile with the same name as an existing one → replaces it, not duplicated
- [ ] Corrupt a profile `.json` (open in editor, delete half the content, save) → app loads the `.bak.json` silently
- [ ] Profile Wizard → create profile → appears in dropdown immediately
- [ ] Double-click slider label → value resets to profile default

---

## 12. Settings Persistence

- [ ] Change Focus Lock process name → save → restart app → correct value restored
- [ ] Change log level to Debug → save → restart app → Debug level active
- [ ] Window position remembered between restarts

---

## 13. Performance

- [ ] Run for 5 minutes in game; tick latency stays below 5 ms average (visible in Log tab)
- [ ] No perf warnings in log during normal play (threshold is 25 ms)
- [ ] CPU usage below 2% while engine is running

---

## 14. Cleanup on Exit

- [ ] Close app with engine running → no stuck keys, no ghost rumble
- [ ] Close during loot vacuum → no stuck left-click
- [ ] Close during Voice-to-Chat → microphone released
