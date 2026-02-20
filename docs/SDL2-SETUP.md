# SDL2 Controller Support — Deployment Guide

RagnaController now supports **Xbox, PlayStation 4, PlayStation 5 and generic controllers** via SDL2.

## 📦 What's needed

The app requires **SDL2.dll** (native library) to support PlayStation controllers.

### Automatic Download (Recommended)

The installer (`Setup.ps1`) will automatically download SDL2.dll from the official SDL GitHub releases.

### Manual Setup

1. Download SDL2 from: https://github.com/libsdl-org/SDL/releases
2. Get `SDL2-2.x.x-win32-x64.zip`
3. Extract `SDL2.dll` to the same folder as `RagnaController.exe`

## 🎮 Supported Controllers

| Controller | Backend | Auto-Detected |
|---|---|---|
| **Xbox One / Series X/S** | XInput | ✅ Yes |
| **Xbox 360** | XInput | ✅ Yes |
| **PlayStation 5 (DualSense)** | SDL2 | ✅ Yes |
| **PlayStation 4 (DualShock 4)** | SDL2 | ✅ Yes |
| **Generic USB/Bluetooth** | SDL2 | ✅ Yes |

## 🔧 How Detection Works

The app tries in this order:

1. **XInput** → Checks for Xbox controllers (native Windows support)
2. **SDL2** → Checks for PlayStation and generic controllers
3. **None** → Shows "No Controller" message

When a controller connects:
- The title bar shows the exact controller type (e.g., "PLAYSTATION 5")
- The controller icon in the visualizer adapts
- All button mappings work identically across all controllers

## 🐛 Troubleshooting

**"No Controller" even though controller is connected:**
- Check if SDL2.dll exists in the app folder
- For PS4/PS5: Make sure it's connected via **USB** or **Bluetooth** (not DS4Windows)
- Windows may need **XInput wrapper removal** if you used tools like DS4Windows before

**PS4/PS5 shows as "Generic":**
- This is fine — all buttons work the same
- SDL2 reads the controller name from the driver

**Buttons don't match:**
- PlayStation uses: Cross (A), Circle (B), Square (X), Triangle (Y)
- The app auto-maps these to Xbox layout internally
- D-Pad, triggers, sticks work identically

## 📝 Build Notes

If building from source:

```powershell
dotnet restore  # Downloads SDL2-CS NuGet package
dotnet build
```

SDL2.dll must be in the output directory (`bin/Release/net8.0-windows/`).

The NuGet package `SDL2-CS` includes the DLL automatically on Windows x64.
