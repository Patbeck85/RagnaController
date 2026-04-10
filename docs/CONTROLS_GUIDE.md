# Controls Guide (v1.2.0)

---

## Layer System

Hold a shoulder button or trigger to access additional button mappings.

| Modifier held | Layer | Typical use |
|---|---|---|
| None | BASE | Movement, basic attack, menu navigation |
| Hold LB | L1 | Primary skills (F1–F4) |
| Hold RB | R1 | Secondary skills (F5–F8) |
| Hold LT | L2 | Utility / buff skills (F9–F12) |
| Hold RT | R2 | Renewal skills / alt actions |

---

## Fixed Global Shortcuts

These are always active regardless of profile or remapping.

| Input | Action |
|---|---|
| **Left Stick** | Click-to-move (character follows cursor) |
| **Right Stick** | Free cursor / engine aim |
| **L3** (press left stick) | Toggle combat engine |
| **R3** (press right stick) | Double-click / engine special action |
| **X** (hold, no modifier) | Hold Alt (show item names on ground) |
| **Start + D-Pad ↑** | Next profile |
| **Start + D-Pad ↓** | Previous profile |
| **Start + Back** | Restore main window (Mini-Mode escape) |
| **Back + L1** | Voice-to-Chat (speak → RO chat) |
| **Back + R1** | Daisy Wheel on-screen keyboard |
| **L3 + R3** | Panic heal (F4 × 10, instant) |
| **LB + RB** | Loot Vacuum (cursor spiral + clicks) |
| **LT + RT** | Radial emote menu |

---

## All Action Types (Button Remapper)

| Type | Description |
|---|---|
| `Key Press` | Tap a keyboard key |
| `Key Hold` | Hold a key while button is held |
| `Left Click` | Single left mouse click |
| `Right Click` | Single right mouse click |
| `Double Click` | Double left click |
| `Macro Playback` | Play a recorded macro |
| `Class Combo (Auto)` | Fire the class combo chain |
| `Switch Window` | Bring the configured RO client to front |

---

## Turbo Modes

Configure per-button in the Remap window.

| Mode | Behaviour |
|---|---|
| **Standard** | Repeats at fixed interval while held |
| **Burst** | 3 rapid presses on first hold, then standard rate |
| **Rhythmic** | Interval oscillates — organic feel, harder to detect |
| **Adaptive** | Instant on press, follow-ups adapt to skill animation length |

---

## Combo Engine

The Combo engine fires a pre-configured skill sequence automatically. Activate by holding the assigned button.

- Timing uses Pre-Renewal or Renewal delay sets (matches your server type selected in the main window).
- Edit sequences in the **Combo** window — supports up to 8 steps per class.

---

## Voice-to-Chat

1. Press `Back + L1` — engine opens microphone.
2. Speak naturally.
3. Recognised text is typed into RO chat and submitted automatically.
4. Works best with a headset microphone in a quiet environment.

---

## Daisy Wheel Keyboard

1. Press `Back + R1` — circular keyboard opens.
2. Push left stick toward a letter cluster.
3. Press **A**, **B**, **X**, or **Y** to type the matching character.
4. **L3** = Backspace · **R3** = Space · **Start** = submit to chat · **B** (no sector) = cancel.

---

## Radial Emote Menu

1. Hold **LT + RT** simultaneously.
2. Point right stick at the desired emote slot.
3. Release both triggers — emote command fires into RO chat.

Configure emote slots in the **Radial** setup window. Run `GetEmotes.ps1` to download official RO emote images.
