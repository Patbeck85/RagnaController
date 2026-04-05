<div align="center">

# ⚔️ RagnaController

**Hybrid Action Controller Layer for Ragnarok Online**

[![Version](https://img.shields.io/badge/Version-1.2.0-D4A832.svg)](#changelog)
[![License: MIT](https://img.shields.io/badge/License-MIT-D4A832.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-D4A832.svg)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Steam%20Deck-D4A832.svg)](https://www.microsoft.com/windows)

*Play Ragnarok Online with any XInput controller — analog movement, 5 combat engines, 39 built-in class profiles, macro recorder, radial menus, and a crash-proof asynchronous input pipeline.*

</div>

---

## ✨ Features

| Feature | Details |
|---|---|
| 🛡️ **Anti-Freeze Pipeline** | Fully asynchronous input processing. Defeats anti-cheat (Gepard Shield) blocks and prevents UI freezes. |
| 🛞 **Radial Menu** | Hold L2+R2 for an instant radial menu. **30+ classic RO Emotes are built-in natively** (no downloads required). |
| 🕹️ **Analog Movement** | Left stick → click-to-move, Dual-Zone curve, Forward-Bias, Coast-Cancel. |
| ⚔️ **5 Combat Engines** | Melee · Ranged (Kite) · Mage · Support · **Combo Engine** — toggle with L3. |
| 🔀 **5-Layer Input** | Base · L1 · R1 · L2 · R2 — over 40 uniquely mappable actions per profile. |
| 💬 **Daisy Wheel Chat** | Press Select+R1 to open a circular on-screen keyboard for quick in-game chatting. |
| 🎙️ **Voice Chat** | Press Select+L1 to dictate text directly into the RO chat using local Speech Recognition. |
| 🌪️ **Smart Loot Vacuum** | Hold L1+R1 to automatically spiral the cursor and spam clicks to pick up massive loot drops. |
| 🆘 **Panic Heal** | Click L3+R3 to bypass all limits and instantly spam F4 (Potions) 10 times in 100ms. |
| 🎭 **4 Turbo Modes** | Standard · Burst · Rhythmic · Adaptive — per button configuration. |
| 📼 **Macro Recorder** | Record, edit, loop, and bind complex macros — saved locally as JSON. |
| 👥 **39 Built-in Profiles** | Pre-configured layouts for every RO class from Novice to Transcended. |

---

## 🚀 Quick Start

1. **Run** `START.bat` (It will automatically download .NET 8 if you don't have it).
2. **Select** a profile matching your class from the dropdown.
3. **Connect** your Xbox, PlayStation, or Switch controller.
4. Launch Ragnarok Online (Run RagnaController as Administrator if RO is elevated).

---

## 🎮 Default Controls

| Button | Action |
|--------|--------|
| **Left Stick** | Click-to-move / action RPG movement |
| **Right Stick** | Cursor control / Aim ground spells |
| **A / B / X / Y** | Action buttons (LMB, RMB, Items, Skills) |
| **L3 (Click Left Stick)** | Toggle Combat Engine (Melee / Mage / Support) |
| **R3 (Click Right Stick)** | Lock target / Place ground spell / Precision mode |
| **L1 / R1 / L2 / R2** | Modifier layers (+4 skill bars) |
| **L2 + R2 (Hold)** | Open Radial Emote / Item Menu |
| **L1 + R1 (Hold)** | Activate Smart Loot Vacuum |
| **L3 + R3 (Click both)** | Panic Heal (Spam F4) |
| **Select + R1** | Open Daisy Wheel On-Screen Keyboard |

---

## 🏗️ Building from Source

We provide an automated, colored, and interactive batch script to build the project effortlessly.

```text
Requirements: Windows 10/11 (The script will automatically install the .NET 8 SDK if missing)

1. Double-click START.bat
2. Select [4] Build All
3. Your ready-to-use ZIP files will appear in the publish/ folder.