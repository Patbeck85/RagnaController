# RagnaController — Feature Testing Guide

## 🧪 Testing Overview

This document covers all newly implemented features and how to test them.

---

## ✅ Feature 1: Global Hotkey Profile Switching

### What It Does
Switch profiles instantly without touching the UI using Ctrl+1-4.

### How to Test
1. Start RagnaController
2. Note current profile (e.g., "Melee")
3. Press **Ctrl+2** → Should switch to Ranged profile
4. Press **Ctrl+3** → Should switch to Mage profile
5. Press **Ctrl+4** → Should switch to Support profile
6. Press **Ctrl+1** → Should switch back to Melee profile

### Expected Behavior
- Profile ComboBox updates immediately
- Engine reloads with new profile settings
- Sound + Rumble feedback plays (if enabled)
- Event log shows: `[Hotkey] Ctrl+N → ProfileName`

### Known Limitations
- Hotkeys only work when RagnaController window has focus OR is minimized (Windows limitation)
- If another app has registered Ctrl+1-4, registration will silently fail

---

## ✅ Feature 2: Sound & Rumble Feedback

### What It Does
Haptic and audio feedback for state changes.

### How to Test

**Sound Feedback:**
1. Enable "Sound Enabled" in settings (default: ON)
2. Start engine → Hear system sound
3. Stop engine → Hear different sound
4. Switch profile (Ctrl+1) → Hear asterisk sound

**Rumble Feedback:**
1. Connect controller (Xbox/PlayStation)
2. Enable "Rumble Enabled" in settings (default: ON)
3. Start engine → Controller vibrates briefly (0.3s)
4. Enter combat mode (L3 for Melee) → Short vibration
5. Lock target (R3) → Light vibration pulse

**Rhythmic Patterns (Kite Engine):**
1. Load Ranged profile
2. Start engine + enter kite mode (L3)
3. Let kite cycle run → Feel rhythmic vibration pattern during retreat

### Expected Behavior
- Sound: System sounds play (Asterisk, Beep, Exclamation)
- Rumble: Controller vibrates with varying intensity/duration based on event
- Both can be toggled independently in settings

### Supported Controllers
- ✅ Xbox One/Series X|S (XInput)
- ✅ PlayStation 5 (SDL2)
- ✅ PlayStation 4 (SDL2)
- ⚠️ Generic controllers (rumble may not work)

---

## ✅ Feature 3: Advanced Logging System

### What It Does
- File logging to disk
- Performance metrics (average tick time, FPS)
- Session statistics (event counts)
- Export full session report

### How to Test

**File Logging:**
1. Start RagnaController
2. Perform actions (start engine, switch profile, etc.)
3. Close app
4. Navigate to: `%LocalAppData%\RagnaController\Logs\`
5. Open `session_YYYY-MM-DD_HH-MM-SS.log`
6. Verify events are logged with timestamps

**Performance Metrics:**
1. Start engine with controller
2. Let run for 30+ seconds
3. Click "Export Session Log" in View menu
4. Open exported file
5. Check "Performance Metrics" section:
   - Average Tick: Should be ~1-2ms
   - Max Tick: Should be <16ms (60 FPS threshold)
   - Frame Rate: Should show ~60 FPS

**Session Statistics:**
1. Start engine
2. Use various features (combat mode, profile switch, etc.)
3. Export session log
4. Check "Event Counts" section
5. Verify categories match your actions

### Expected Output (Example)
```
═══════════════════════════════════════════════════════════
  RAGNA CONTROLLER — SESSION EXPORT
═══════════════════════════════════════════════════════════
Session Start:    2026-02-18 15:30:00
Session Duration: 00:05:23
Total Events:     847

─── Performance Metrics ───────────────────────────────────
Average Tick:     1.23ms
Max Tick:         8ms
Frame Rate:       62.5 FPS

─── Event Counts ──────────────────────────────────────────
Engine          124
Controller       98
Combat           67
...
```

### File Locations
- Session logs: `%LocalAppData%\RagnaController\Logs\session_*.log`
- Exports: `%LocalAppData%\RagnaController\Logs\export_*.txt`

---

## ✅ Feature 4: Mini Mode UI

### What It Does
Compact 280x120px overlay window for gaming.

### How to Test

**Enter Mini Mode:**
- Method 1: Press **Ctrl+F** in main window
- Method 2: Right-click menu → "Mini Mode"

**Mini Mode Features:**
1. Shows current profile name
2. Live engine status (color-coded dot)
3. Combat state (icon + label)
4. Draggable window (click + drag anywhere)
5. Always on top
6. No taskbar icon

**Exit Mini Mode:**
- Click ✕ button in mini window
- Restores full main window

### Expected Behavior
- Mini window appears centered above main window
- Main window hides (but app keeps running)
- Mini window updates in real-time with engine state
- Ctrl+1-4 hotkeys still work in mini mode

### Visual States (Color Coding)
- 🟢 Green dot = Engine running
- 🔴 Red dot = Engine stopped
- State icon changes:
  - ■ = Idle
  - ⟳ = Seeking
  - ● = Engaged
  - ▶ = Attacking

---

## 🔧 Troubleshooting

### Hotkeys Not Working
**Symptom:** Pressing Ctrl+1-4 does nothing  
**Causes:**
1. Another app registered the same hotkey
2. RagnaController doesn't have focus
3. Registration failed on startup

**Fix:**
1. Check event log for "Global hotkeys registered"
2. Close apps that might use Ctrl+1-4
3. Restart RagnaController

### No Rumble
**Symptom:** Controller doesn't vibrate  
**Causes:**
1. Rumble disabled in settings
2. Controller doesn't support rumble
3. SDL2.dll missing (PlayStation controllers)

**Fix:**
1. Check "Rumble Enabled" in settings
2. Test with Xbox controller (guaranteed rumble support)
3. For PS4/PS5: Ensure SDL2.dll is in app folder

### Performance Issues
**Symptom:** Stuttering, lag, high tick times  
**Causes:**
1. File logging overhead
2. Background apps
3. Slow disk I/O

**Fix:**
1. Disable file logging: Set LogLevel = Error
2. Close background apps
3. Run from SSD (not HDD)

### Mini Mode Window Lost
**Symptom:** Mini mode active but can't find window  
**Fix:**
- Alt+Tab to switch to it
- Or restart app (exits mini mode on close)

---

## 📊 Performance Benchmarks

### Expected Performance Targets
- **Engine Tick Time:** <2ms average, <16ms max
- **Input Latency:** <5ms (controller → action)
- **Memory Usage:** ~50-80 MB
- **CPU Usage:** <1% idle, <5% active
- **Startup Time:** <2 seconds

### Stress Test
1. Start engine
2. Switch profiles rapidly (Ctrl+1-4 spam)
3. Enter/exit combat modes repeatedly
4. Check for:
   - No crashes
   - No memory leaks
   - Tick time stays <16ms

---

## 🐛 Known Issues & Limitations

### Hotkeys
- Only work when app has focus or is minimized
- Can't override system hotkeys (Windows limitation)
- Max 4 profiles supported (Ctrl+1-4)

### Rumble
- Generic controllers may not support rumble
- PlayStation via Bluetooth: Rumble may be delayed
- Continuous rumble can drain battery quickly

### Mini Mode
- Can't drag to second monitor (always on primary)
- No resize option (fixed 280x120px)
- Topmost window can cover other apps

### Logging
- Large logs (>50MB) can slow down export
- File I/O can cause brief stutters on slow disks
- Old logs are not auto-deleted (manual cleanup needed)

---

## ✨ Future Improvements

### Planned Features (Not Yet Implemented)
- ⏳ Button Remapping UI
- ⏳ Macro Recorder
- ⏳ Advanced Turbo Modes (Burst, Rhythmic)
- ⏳ Community Profile Hub
- ⏳ Auto-Update System
- ⏳ Conditional Actions

### Why Not Included Yet
These features require:
- Additional UI complexity
- Backend infrastructure (cloud for profile hub)
- More extensive testing

They may be added in future releases based on user demand.

---

## 📝 Testing Checklist

Use this checklist to verify all features:

- [ ] Ctrl+1 switches to Melee profile
- [ ] Ctrl+2 switches to Ranged profile  
- [ ] Ctrl+3 switches to Mage profile
- [ ] Ctrl+4 switches to Support profile
- [ ] Sound plays on engine start/stop
- [ ] Controller rumbles on state changes
- [ ] Event log shows timestamped events
- [ ] Session log file created in %LocalAppData%
- [ ] Export session log works and shows metrics
- [ ] Ctrl+F enters mini mode
- [ ] Mini mode shows live engine state
- [ ] ✕ button exits mini mode
- [ ] Settings persist after restart
- [ ] No crashes during 5+ minute session
- [ ] Performance metrics show <16ms avg tick

---

## 📧 Feedback & Bug Reports

If you encounter issues:
1. Export session log (View → Export Session Log)
2. Attach log file to bug report
3. Include:
   - Windows version
   - Controller type
   - Steps to reproduce
   - Expected vs actual behavior

Session logs contain full diagnostic info including:
- Event timeline
- Performance metrics
- Error messages
- Configuration state

---

**Happy Testing!** 🎮
