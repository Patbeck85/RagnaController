# RagnaController — Architecture

## Overview

RagnaController is a WPF/.NET 8 application that bridges gamepad input to keyboard/mouse output
using the Windows `SendInput` API. It runs a 60 Hz (16 ms) tick loop entirely on the WPF dispatcher thread.

```
┌─────────────────────────────────────────────────────────────┐
│                      MainWindow (WPF UI)                    │
│  Profile UI  │  Settings Sliders  │  Viz  │  Log  │ Tabs   │
└────────────────────────┬────────────────────────────────────┘
                         │ LoadProfile() / Start() / Stop()
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                       HybridEngine                          │
│  DispatcherTimer @ 16 ms                                    │
│   ┌────────────────┐  ┌─────────────────┐                  │
│   │ MovementEngine │  │  CombatEngine   │                  │
│   │  Left Stick    │  │ Button Mapping  │                  │
│   │  → Mouse Move  │  │ + Turbo + Layer │                  │
│   └───────┬────────┘  └────────┬────────┘                  │
│           └──────────┬─────────┘                           │
│                      ▼                                      │
│              InputSimulator (P/Invoke SendInput)            │
└───────────────────────────────────────────────────────────  ┘
                         ▲
         ┌───────────────┘
┌────────┴────────┐
│ ControllerService│   (SharpDX XInput)
│  Xbox / HID      │
└──────────────────┘
```

## Tick Loop

Every 16 ms:
1. `ControllerService.GetGamepad()` → raw XInput state
2. `MovementEngine.Update(lx, ly)` → computes pixel delta → `InputSimulator.MoveMouseRelative()`
3. `CombatEngine.UpdateLayers(l2, r2)` → sets active button layer
4. For each changed/held button → `CombatEngine.ProcessButton()` → `InputSimulator.TapKey()` or `Click()`
5. Right stick → small mouse nudge (camera assist)
6. Fire `SnapshotUpdated` event → UI updates visualizer

## Layer System

```
Button "A" pressed:
  ├── Neither L2 nor R2 held? → lookup "A"       → Base Layer
  ├── L2 held?                → lookup "L2+A"    → L2 Layer
  └── R2 held?                → lookup "R2+A"    → R2 Layer
```

## Profile Storage

Profiles are JSON files stored at:
`%AppData%\RagnaController\Profiles\<name>.json`

Built-in profiles (Melee, Ranged, Mage) are code-defined and never written to disk.

## Input Flow (SendInput)

```
Controller Axis/Button
        │
    HybridEngine (16ms tick)
        │
    MovementEngine / CombatEngine
        │
    InputSimulator.MoveMouseRelative()
    InputSimulator.TapKey()
    InputSimulator.LeftClick() / RightClick()
        │
    Windows SendInput API (user32.dll)
        │
    Operating System input queue
        │
    Ragnarok Online (receives as normal keyboard/mouse input)
```

No game process is opened, read, or written at any point.
