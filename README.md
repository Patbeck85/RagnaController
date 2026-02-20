<div align="center">

# 🎮 RagnaController

**Hybrid Action Controller Layer for Ragnarok Online**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue.svg)](https://www.microsoft.com/windows)
[![GitHub Stars](https://img.shields.io/github/stars/yourusername/RagnaController?style=social)](https://github.com/yourusername/RagnaController)

*Play Ragnarok Online with an Xbox or PlayStation controller — analog movement, layered skill mapping, turbo support.*

![Screenshot](docs/screenshot.png)

</div>

---

## ✨ Features

| Feature | Description |
|---|---|
| 🕹️ **Analog Movement** | Left stick → smooth mouse cursor movement with deadzone, sensitivity and response curve settings |
| 🥊 **Combat Engine** | Full button-to-key/click mapping with per-button turbo (auto-repeat) |
| 🔀 **Layer System** | Hold **L2** or **R2** to access 2 extra button layers — triple your mappable buttons |
| 👤 **3 Built-in Profiles** | Melee, Ranged, Mage — ready to use out of the box |
| 💾 **Profile Management** | Create, save, delete, import & export custom JSON profiles |
| 🎮 **Universal Controller** | Xbox (XInput) + PlayStation 4/5 (SDL2) — auto-detected, no tools needed |
| 🪟 **Modern Dark UI** | Clean WPF interface with controller visualizer and live event log |
| 🔒 **Safe by Design** | Zero memory reading, no code injection, no packet manipulation |

---

## 🛡️ Safety

RagnaController **only** simulates standard Windows input via the `SendInput` API.

- ❌ Does **not** read or write game memory
- ❌ Does **not** inject code into any process
- ❌ Does **not** modify or sniff network traffic
- ✅ Works like a keyboard and mouse — nothing more

---

## 📦 Requirements

- **Windows 10 / 11** (x64)
- [**.NET 8 Runtime**](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Controller (auto-detected):**
  - ✅ Xbox One / Series X|S / 360 (XInput — native)
  - ✅ PlayStation 5 (DualSense) via SDL2
  - ✅ PlayStation 4 (DualShock 4) via SDL2
  - ✅ Generic USB/Bluetooth controllers via SDL2

The app detects your controller type and displays it in the UI. PlayStation buttons auto-map to Xbox layout. **No DS4Windows needed!**

---

## 🚀 Getting Started

### Option A — Windows Installer (recommended for most users)

1. Go to [**Releases**](https://github.com/yourusername/RagnaController/releases)
2. Download `RagnaController-Setup-v1.0.0.exe`
3. Run the installer — it **automatically** checks and installs:
   - .NET 8 Windows Desktop Runtime (if missing)
   - Visual C++ Redistributable (if missing)
4. Launch from the Desktop shortcut or Start Menu

> The installer handles everything. No manual setup needed.

### Option B — Portable ZIP (no install required)

1. Download `RagnaController-v1.0.0-portable.zip`
2. Extract anywhere
3. Double-click `RagnaController.bat`
   - Auto-checks for .NET 8
   - Downloads and installs it if missing
   - Then launches the app

### Option C — Build from source

```powershell
git clone https://github.com/yourusername/RagnaController.git
cd RagnaController

# Build portable ZIP
.\scripts\build.ps1

# Build + Inno Setup installer (requires Inno Setup 6 installed)
.\scripts\build.ps1 -BuildInstaller

# Self-contained build (bundles .NET — no runtime needed on target PC)
.\scripts\build.ps1 -SelfContained
```

---

## 🎮 Default Controls

### Base Layer

| Button | Melee | Ranged | Mage |
|---|---|---|---|
| **A** | Basic Attack ⚡ | Double Strafe ⚡ | Storm Gust |
| **B** | Skill 2 | Arrow Shower | Meteor Storm |
| **X** | Skill 3 | Falcon Assault | Lord of Vermillion |
| **Y** | Skill 4 | Blitz Beat | Fire Bolt ⚡ |
| **RB** | Potion (F1) | Potion (F1) | Potion (F1) |
| **LB** | Lock Target (Right Click) | Lock Target | Place AoE |
| **D-Pad** | Skill Bars F5–F8 | Skill Bars F5–F8 | Skill Bars F5–F8 |
| **Right Stick** | Camera / Cursor | Camera / Cursor | Camera / Cursor |

> ⚡ = Turbo (auto-repeat while held)

### L2 Layer (hold L2)

| Button | Action |
|---|---|
| **A / B / X / Y** | Skills 5–8 (F1–F4) |
| **D-Pad** | Items 1–4 (Num 1–4) |

### R2 Layer (hold R2)

| Button | Action |
|---|---|
| **A** | Click Move |
| **B** | Target Next (Tab) |
| **X** | Sit / Stand (Alt) |

---

## 🗂️ Project Structure

```
RagnaController/
│
├── src/RagnaController/
│   ├── Core/
│   │   ├── HybridEngine.cs       ← Main 60Hz tick loop
│   │   ├── MovementEngine.cs     ← Analog stick → mouse
│   │   ├── CombatEngine.cs       ← Button → key/click + turbo
│   │   └── InputSimulator.cs     ← Windows SendInput wrapper
│   │
│   ├── Controller/
│   │   └── ControllerService.cs  ← XInput wrapper (SharpDX)
│   │
│   ├── Profiles/
│   │   ├── Profile.cs            ← Profile data model
│   │   └── ProfileManager.cs     ← JSON load/save + built-in presets
│   │
│   ├── Models/
│   │   └── Settings.cs           ← App settings (JSON)
│   │
│   ├── MainWindow.xaml/.cs       ← Main UI
│   └── NewProfileDialog.xaml/.cs ← New profile dialog
│
├── docs/
│   ├── architecture.md
│   └── screenshot.png
│
├── README.md
├── LICENSE
└── .gitignore
```

---

## 🔧 Customization

Profiles are stored as JSON files in `%AppData%\RagnaController\Profiles\`.
You can edit them manually or use the built-in Import/Export buttons.

Example profile snippet:
```json
{
  "Name": "My Wizard",
  "Class": "Mage",
  "MouseSensitivity": 1.0,
  "Deadzone": 0.15,
  "ButtonMappings": {
    "A": {
      "Type": "Key",
      "Key": "Z",
      "Label": "Storm Gust",
      "TurboEnabled": false
    },
    "L2+A": {
      "Type": "Key",
      "Key": "F1",
      "Label": "Ice Wall",
      "TurboEnabled": false
    }
  }
}
```

---

## 🗺️ Roadmap

- [ ] Controller button visualizer (live highlight)
- [ ] Macro sequences (multi-key combos)
- [ ] Per-skill cooldown timer display
- [ ] System tray minimization
- [ ] Multi-controller support (Player 2+)
- [ ] Auto-detect Ragnarok Online window

---

## 🤝 Contributing

Pull requests are welcome!

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m "Add my feature"`
4. Push to the branch: `git push origin feature/my-feature`
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

<div align="center">

Made with ❤️ for the Ragnarok Online community

*Not affiliated with Gravity Co., Ltd. or any RO server operators.*

</div>
