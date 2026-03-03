<div align="center">

# ⚔️ RagnaController

**Hybrid Action Controller Layer for Ragnarok Online**

[![Version](https://img.shields.io/badge/Version-1.1.0-D4A832.svg)](#changelog)
[![License: MIT](https://img.shields.io/badge/License-MIT-D4A832.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-D4A832.svg)](https://dotnet.microsoft.com)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-D4A832.svg)](https://www.microsoft.com/windows)

*Play Ragnarok Online with any XInput controller — analog movement, 4 combat engines, 39 built-in class profiles, macro recorder, animated splash screen and system tray.*

</div>

---

## ✨ Features

| Feature | Details |
|---|---|
| 🎬 **Splash Screen** | Animated startup with freigestelltem Logo, gold glow effects, particles |
| 🔔 **System Tray** | Minimize to tray, double-click restore, right-click context menu |
| 🕹️ **Analog Movement** | Left stick → click-to-move, Dual-Zone curve, Forward-Bias, Coast-Cancel |
| ⚔️ **Mob-Sweep Mode** | R2 + Left stick → auto TAB-cycle + attack while moving |
| 🖱️ **Cursor Engine** | Right stick → smooth cursor, SELECT toggles Precision Mode (speed ÷3) |
| ⌨️ **Global Hotkeys** | Ctrl+1–4 switch profiles even when minimized |
| 🥊 **4 Combat Engines** | Melee · Ranged (Kite) · Mage · Support — toggle with L3 |
| 🔀 **5-Layer Input** | Base · L1 · R1 · L2 · R2 — 20+ uniquely mappable combos |
| 🎭 **4 Turbo Modes** | Standard · Burst · Rhythmic · Adaptive — per button |
| 📼 **Macro Recorder** | Record, edit, loop, bind — saved as JSON in AppData |
| 👥 **39 Built-in Profiles** | Every RO class from Novice to Transcended |
| 🧙 **Profile Wizard** | 4-step guided profile creation with class templates |
| 📚 **Profile Library** | Load, import, export, delete profiles |
| 🔁 **Button Remapper** | Full per-button remapping with turbo and macro support |
| 📊 **Live Info Tab** | Class tips, skill recommendations, session stats |
| 🖥️ **Mini Mode** | Compact overlay, Ctrl+F to toggle |
| 🔄 **Auto-Update Check** | Checks GitHub on startup for new releases |

---

## 🚀 Quick Start

1. **Run** `RagnaController.exe`  
2. **Select** a profile matching your class from the dropdown  
3. **Click START** — controller is now active  
4. Launch Ragnarok Online  

> **Admin rights recommended** when RO runs elevated (avoids SendInput UIPI block)

---

## 🎮 Default Controls

| Button | Action |
|--------|--------|
| **Left Stick** | Click-to-move / action RPG movement |
| **Right Stick** | Cursor control |
| **A** | LMB / Attack / Select |
| **B** | Enter / Confirm dialog |
| **L3** | Combat engine toggle |
| **R3 (SELECT)** | Precision mode toggle |
| **Start** | Escape / Close window |
| **L1 / R1 / L2 / R2** | Modifier layers (+4 buttons each) |

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

## 🏗️ Building from Source

```
Requirements: .NET 8 SDK, Windows 10/11

START.bat          → Build + Run (debug)
BUILD.bat          → Publish → RagnaController_v1.1.0.zip
```

Output: `RagnaController_v1.1.0.zip` neben der `.sln`-Datei

---

## 📋 Changelog

### v1.1.0 — März 2026
> **7 Bugs behoben · Gold-Theme · Splash-Logo freigestellt**

- **[B-47] HOCH** — `XamlParseException` beim Start: `IsSelected` auf `ComboBoxItem` ist in WPF nicht erlaubt → auf alle 3 betroffenen Fenster gefixt
- **[B-48] HOCH** — `XamlParseException`: `BasedOn`-Kette im impliziten `ComboBox`-Style crasht Template-Auflösung → direkte Properties, kein `BasedOn`
- **[B-49] HOCH** — `NullReferenceException` in `ClassCombo_SelectionChanged`: `SelectionChanged` feuert vor `InitializeComponent()` → Null-Guard ergänzt
- **[B-50] MITTEL** — `NullReferenceException` in `ApplyFilters()` → Null-Guard für `SearchBox`, `ProfilesList`, `FilterCombo`
- **[B-51] MITTEL** — `ProfileCombo` zeigte keinen Text: `DisplayMemberPath="Name"` fehlte → ergänzt
- **[B-52] NIEDRIG** — Splash Screen zeigte alten Stand: Logo-PNG freigestellt (schwarzer Hintergrund entfernt), Animationseffekte auf freigestelltes Bild angepasst
- **[B-53] NIEDRIG** — `CharacterSpacing` in WPF nicht verfügbar (WinUI-only) → Build-Fehler behoben

**UI-Änderungen:**
- 🎨 Komplettes **Gold-Theme** — alle Cyan/Türkis-Farben durch Logo-Gold (`#D4A832`) ersetzt
- 🖼️ **Splash Screen** neu: freigestelltes Logo, transparenter Hintergrund, 15 Storyboard-Animationen, Partikel, Flügel-Glows
- 📦 **Version 1.1.0** — `csproj` und Titelleiste aktualisiert, ZIP-Build erzeugt `RagnaController_v1.1.0.zip`

### v1.0.8 — Feb 2026
> **5 Bugs behoben · Macro System · Profile Library**

- [B-37]–[B-41] Macro Recorder Fixes, Profile Import/Export, Turbo-Modus Stabilität

### v1.0.1–v1.0.7
> Frühere Versionen: Controller-Erkennung, InputSimulator, WMI-Freeze, Kite-Engine, Combat-Engines, Button-Remapper

---

## 📁 Projektstruktur

```
RagnaController/
├── src/RagnaController/
│   ├── Assets/              ← Icons, Splash-Bilder
│   ├── Controller/          ← ControllerService (XInput)
│   ├── Core/                ← HybridEngine, CombatEngine, Mage, Support, Kite
│   ├── Models/              ← Settings, VirtualKey
│   ├── Profiles/            ← Profile, ProfileManager
│   ├── App.xaml(.cs)        ← Startup, Splash
│   ├── MainWindow.xaml(.cs) ← Hauptfenster
│   └── *.xaml.cs            ← Remap, Library, Wizard, Macro, Settings
├── docs/
│   ├── BUILD-INSTRUCTIONS.md
│   ├── FEATURES.md
│   ├── SETUP_PLAYSTATION.md
│   └── TESTING.md
└── changelog/
    ├── bugs.json            ← Bug-Datenbank
    └── CHANGELOG.docx       ← Auto-generiertes Changelog
```

---

## 📄 License

MIT © 2026 RagnaController Contributors
