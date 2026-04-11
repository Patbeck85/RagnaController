<div align="center">

# ⚔️ RagnaController

**Hybrid Action Controller Layer for Ragnarok Online**

[![Version](https://img.shields.io/badge/Version-1.2.0-E5B842.svg)](#changelog)
[![License: MIT](https://img.shields.io/badge/License-MIT-E5B842.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-E5B842.svg)](https://dotnet.microsoft.com)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-E5B842.svg)](https://www.microsoft.com/windows)

*Play Ragnarok Online with any XInput controller — analog movement, 5 combat engines, 39 built-in class profiles, cloud profile sharing, voice-to-chat, daisy wheel keyboard, macro recorder, and an Obsidian & Gold UI.*

</div>

---

## ✨ Features

| Feature | Details |
|---|---|
| 🛡️ **Focus Lock** | Pauses all input when RO loses focus — prevents accidental clicks in Discord or browsers. |
| 🎯 **Visual Deadzone Ring** | Live red ring on stick visualiser scales with your deadzone slider — fix stick drift in 3 seconds. |
| 🎮 **Smart Skill Auto-Aim** | Cursor juggling: snaps to locked target, fires skill, snaps back in ~12 ms. No manual aiming. |
| 🔄 **Window Switcher** | Instantly switches between multiple RO clients. WindowTracker re-centres automatically on the active foreground window. |
| 🗺️ **DPI-Aware Window Tracking** | Reads actual RO client bounds via Win32 (PerMonitorV2) — perfectly accurate on any 4K monitor, DPI, or window mode. |
| 🎤 **Voice-to-Chat** | `Back + L1` — speak into microphone, text is typed into RO chat automatically. |
| 🎡 **Daisy Wheel Keyboard** | `Back + R1` — circular on-screen keyboard for typing without leaving the controller. |
| 🕹️ **Analog Movement** | Left stick → click-to-move with dual-zone curve, coast-cancel, and Action RPG leash radius. |
| ⚔️ **Mob-Sweep Mode** | R2 + Left stick → auto TAB-cycle + attack while moving. |
| 🥊 **5 Combat Engines** | Melee · Ranged (Kite) · Mage · Support · Combo Sequence — automatic FSM combat tracking. |
| 🔀 **5-Layer Input** | Base · L1 · R1 · L2 · R2 — 20+ uniquely mappable combos. |
| 🎭 **4 Turbo Modes** | Standard · Burst · Rhythmic · Adaptive — configurable per button. |
| 📼 **Macro Recorder** | Record, edit, loop, bind — saved as JSON in AppData. |
| 🎯 **Radial Emote Menu** | Hold LT+RT — point right stick to fire emote commands. Window reused, no flicker. |
| 👥 **39 Built-in Profiles** | Every RO class from Novice to Transcended. Includes pre-configured combo sequences (e.g. Monk, Taekwon). |
| ☁️ **Community Profile Sharing**| Upload to GitHub Gist with 1 click. Share short codes (e.g. `GX-A3F9`) with other players to download instantly. |
| 🔄 **Auto-Update Check** | Checks GitHub releases on startup. |

---

## 🚀 Quick Start

1. **Run** `RagnaController.exe` (Run as Admin recommended — avoids UIPI input block from anticheats like Gepard).
2. **Select** your game client `.exe` in Settings → the Browse button opens a file picker.
3. **Select** a profile matching your class from the dropdown.
4. **Click START** or wait for the controller to auto-connect.
5. Launch Ragnarok Online.

> **Focus Lock** is enabled by default. If the engine seems inactive, make sure RO is the foreground window. The status bar shows `⛔ FOCUS LOCK` when blocked.

---

## 🎮 Default Controls

| Button | Action |
|--------|--------|
| **Left Stick** | Click-to-move / Action RPG movement |
| **Right Stick** | Cursor control |
| **L3** | Toggle combat engine (Seeking/Engaged modes) |
| **L3 + L1** | Toggle Mage Engine (Ground spell / Bolt aim) |
| **L3 + R1** | Toggle Kite Engine (Ranged hit & run) |
| **L3 + L2** | Toggle Support Engine (Party target & heal) |
| **L3 + R3** | Panic heal (F4 × 10) |
| **LB + RB** | Loot vacuum (cursor spiral + click) |
| **LT + RT** | Radial emote menu |
| **Back + L1** | Voice-to-Chat |
| **Back + R1** | Daisy Wheel keyboard |
| **Start + Back** | Emergency: restore main window from Mini-Mode |
| **Start + D-Pad ↑/↓** | Quick profile switch |
| **L1 / R1 / L2 / R2** | Modifier layers |

---

## 🏛️ Class Profiles

| Category | Profiles |
|----------|----------|
| **Novice** | Novice, Super Novice |
| **Swordsman** | Swordsman, Knight, Lord Knight, Crusader, Paladin |
| **Mage** | Mage, Wizard, High Wizard, Sage, Professor |
| **Archer** | Archer, Hunter, Sniper, Dancer, Gypsy, Bard, Clown |
| **Acolyte** | Acolyte, Priest, High Priest, Monk, Champion |
| **Merchant** | Merchant, Blacksmith, Whitesmith, Alchemist, Creator |
| **Thief** | Thief, Assassin, Assassin Cross, Rogue, Stalker |
| **Taekwon** | Taekwon, Star Gladiator, Soul Linker |
| **Ninja / Gunslinger** | Ninja, Gunslinger |

---

## ⚙️ Settings

| Setting | Default | Notes |
|---|---|---|
| **Game client** | `ragexe` | Browse for `.exe` — only the filename is stored. Used by Focus Lock and Window Tracker. |
| **Focus Lock** | On | Pause engine when RO is not the foreground window |
| Auto-start engine | Off | Start engine immediately on launch |
| Start in Mini-Mode | Off | Launch directly to compact overlay |
| Sound feedback | On | Windows system sounds |
| Rumble feedback | On | Haptic patterns |
| Log level | Info | Debug · Info · Warning · Error |

---

## 🏗️ Building from Source

```text
Requirements: .NET 8 SDK, Windows 10/11

build.bat   → Builds framework-dependent, self-contained, and Steam Deck targets.
```

*See `docs/BUILD-INSTRUCTIONS.md` for details.*

---

## 📋 Changelog

### v1.2.0 — April 2026

**New Features:**
- ☁️ **Community Profile Sharing** — upload profiles to GitHub Gists anonymously. Share 6-character codes (e.g. `GX-A3F9`) so others can download your layout directly inside the app.
- 🥊 **Combo Engine** — 5th dedicated combat engine. Hold a button to fire an automatic, server-delay-adjusted skill sequence (e.g. Monk Triple Attack → Chain Combo → Combo Finish → Asura Strike).
- 🛡️ **Focus Lock** — engine suspends input when RO is not the foreground window. `GetForegroundWindow` polled every 500 ms. Status bar shows orange `⛔ FOCUS LOCK` indicator.
- 🎯 **Visual Deadzone Ring** — red semi-transparent ellipse on both stick visualisers scales live with the Deadzone slider. Intuitive stick-drift calibration.
- 🗺️ **DPI-Aware Window Tracker** (`WindowTracker.cs`) — uses `GetClientRect` + `GetDpiForMonitor`. Application marked as `PerMonitorV2` DPI-aware. Perfectly accurate on any monitor, DPI, or window mode. 
- 🔄 **Window Switcher** — `ActionType.SwitchWindow` on background thread. Allows instantly flipping between a main and slave client. `WindowTracker` forces a re-centre immediately after the switch.
- 🎤 **Voice-to-Chat** — `Back + L1` activates Windows Speech Recognition, types spoken text into RO chat.
- 🎡 **Daisy Wheel Keyboard** — `Back + R1` opens circular on-screen keyboard.
- 🎯 **Smart Skill Auto-Aim** — cursor snaps to locked target, fires skill, returns in ~12 ms.
- 🎭 **Radial Menu reuse** — window is hidden/shown (`Visibility`) instead of destroyed/recreated. Eliminates WPF transparency flicker on rapid trigger presses.

**Bug Fixes:**
- Ghost rumble after engine pause — async patterns now check `_rumbleEnabled` after `await Delay`.
- `ProfileManager.AddAndSave` duplicated profiles on same-name import — now correctly replaces.
- Loot Vacuum spawned ~125 `Task.Run` per second — throttled to one click per 50 ms.
- Profile `.json` corruption recovery — `Load()` now falls back to `.bak.json`.
- `SendChatString` race condition — serialised with `_isChatting` flag to prevent emote chat interleaving.

**Performance:**
- `timeBeginPeriod(1)` on startup — Windows scheduler locked to 1 ms precision, DispatcherTimer jitter reduced from ±5 ms to ±0.5 ms. Analog input is noticeably smoother.

### v1.1.0 — März 2026
> Gold-Theme · Splash · 7 Build-Fixes

### v1.0.x — Feb 2026
> Core engine, combat systems, macro recorder, profile library

---

## 📁 Project Structure

```text
RagnaController/
├── src/RagnaController/
│   ├── Assets/              ← SVG icons, splash images, emote PNGs
│   ├── Controller/          ← ControllerService (XInput + WMI brand detection)
│   ├── Core/                ← HybridEngine, Combat, Mage, Support, Kite, Combo,
│   │                           Voice, WindowSwitcher, WindowTracker, ProfileShare
│   ├── Models/              ← Settings, VirtualKey
│   ├── Profiles/            ← Profile, ProfileManager
│   └── App.xaml(.cs)        ← Startup, global Obsidian/Gold styles, PerMonitorV2
├── docs/
│   ├── ADVANCED_FEATURES.md
│   ├── BUILD-INSTRUCTIONS.md
│   ├── CONTROLS_GUIDE.md
│   ├── FEATURES.md
│   ├── SETUP_PLAYSTATION.md
│   ├── TESTING.md
│   └── architecture.md
├── build.bat                ← CLI publishing script for Win and Steam Deck
└── GetEmotes.ps1            ← Downloads RO emote images from iROWiki
```

---

## 📄 License

MIT © 2026 RagnaController Contributors