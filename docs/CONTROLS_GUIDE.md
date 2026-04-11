# Controls Guide (v1.2.0)

RagnaController uses a powerful 5-layer input system and multiple background FSMs (Finite State Machines) to translate controller input into optimal Ragnarok Online actions. 

This guide covers the default layer logic, fixed global shortcuts, and how to map your own abilities.

---

## 🔀 Layer System

By holding one of the shoulder buttons or triggers, you shift the entire controller into a new "Layer". This allows you to map over 20 unique actions to the 4 face buttons and D-Pad.

| Modifier Held | Layer | Typical Use Case (Recommendation) |
|---|---|---|
| **None** | `BASE` | Movement, basic attack, NPC interaction, looting |
| **Hold LB** | `L1` | Primary offensive skills (F1–F4) |
| **Hold RB** | `R1` | Secondary / AoE skills (F5–F8) |
| **Hold LT** | `L2` | Utility / buffs / teleport (F9–F12) |
| **Hold RT** | `R2` | Renewal skills / Combo sequences / Alt actions |

*Example:* Pressing `A` normally might trigger a basic attack. Holding `LB` and pressing `A` triggers your `L1+A` mapping (e.g., Sonic Blow).

---

## 🔒 Fixed Global Shortcuts

These shortcuts are hardcoded into the engine and are **always active** regardless of your current profile.

### Core Movement & Aiming
| Input | Action | Details |
|---|---|---|
| **Left Stick** | Click-to-Move | Analog movement. Uses dual-zone curve and coast-cancel. |
| **Right Stick** | Engine Aim | Controls the cursor. Used to aim skills, lock targets, or select emotes. |
| **X** (Hold) | Hold Alt | Highlights dropped items and NPC names while held. (Requires no layer modifiers to be held). |
| **R3** (Click RS) | Double-Click / Lock | Double-clicks the mouse. If a combat engine is active, locks the target. |

### Combat Engine Toggles
| Input | Action | Details |
|---|---|---|
| **L3** (Click LS) | Toggle Melee | Activates Auto-Target FSM (Seeking / Engaged / Attacking). |
| **L3 + L1** | Toggle Mage | Activates Mage Engine (Ground spell aiming / Bolt auto-cast). |
| **L3 + R1** | Toggle Kite | Activates Kite Engine (Ranged Hit & Run automation). |
| **L3 + L2** | Toggle Support | Activates Support Engine (Party tab-cycling / Smart heal). |
| **L3 + R3** | Panic Heal | Bypasses all delays, triggers warning rumble, fires `F4` 10 times instantly. |

### Overlays & Tools
| Input | Action | Details |
|---|---|---|
| **LB + RB** | Loot Vacuum | Spirals the cursor around the character and clicks rapidly (50ms interval). |
| **LT + RT** | Radial Emote Menu | Opens the Emote wheel. Point right stick, release triggers to fire. |
| **Back + L1** | Voice-to-Chat | Opens microphone. Speak to automatically type into RO chat. |
| **Back + R1** | Daisy Wheel | Opens circular on-screen keyboard. Use left stick + face buttons to type. |
| **Start + D-Pad ↑/↓**| Quick Profile Switch | Instantly cycles through your saved profiles. |
| **Start + Back** | Restore Window | Emergency escape: brings RagnaController to the front (useful to exit Mini-Mode click-through). |

---

## 🛠️ Action Types (Remapper)

When configuring a button in the **Remap** window, you can choose from the following Action Types:

| Type | Description |
|---|---|
| `Key Press` | Standard keyboard keystroke (e.g., F1, Enter, Space). |
| `Left Click` | Simulates a single left mouse click. |
| `Right Click` | Simulates a single right mouse click. |
| `Macro Playback` | Plays a saved JSON macro sequence recorded via the Macro Recorder. |
| `Class Combo (Auto)`| Triggers the **Combo Engine**. Hold the button to automatically step through your configured skill sequence with perfect server timing. |
| `Switch Window` | Multi-client support: instantly brings the background RO client to the front. Press again to return. |

---

## ⚡ Turbo Modes

Any `Key Press` or `Click` action can have **Turbo** enabled. You can choose the firing interval (e.g., 100ms) and one of 4 specific behaviors:

1. **Standard:** Fires repeatedly at the exact specified interval while the button is held.
2. **Burst:** Fires 3 rapid presses immediately upon holding, then falls back to the standard interval. Perfect for ensuring a skill registers in heavy lag.
3. **Rhythmic:** Oscillates the interval slightly (±15%). Creates a more organic, human-like input pattern.
4. **Adaptive:** Fires instantly on press, then delays subsequent inputs based on typical RO skill animation locks.

---

## 🥊 The 5 Combat Engines

RagnaController features 5 distinct state machines (FSMs). You usually only need one active depending on your class.

1. **Melee (AutoTarget):** Point the right stick toward a mob. Press `R3` to lock on. The engine auto-attacks and handles **Smart Skill Auto-Aim** (snapping the cursor to the locked target when you press a skill button, returning it in 12ms).
2. **Mage:** Use the right stick to freely aim a ground AoE spell. Press `R3` to place it instantly without losing your walking rhythm. Hold `R2` for targeted Bolt-spam mode.
3. **Kite (Ranged):** Lock a target. The engine will fire X arrows, automatically click behind you to retreat, pivot back, and resume firing. Hold `R2` to stand your ground; hold `L2` to force an early retreat.
4. **Support:** Aim right stick toward a party member on screen. Press `R3` to instantly snap the cursor to them and cast Heal. Or enable Auto-Cycle to automatically `TAB` through party members.
5. **Combo Engine:** Bound to a specific button (e.g., `R2 + A`). Hold the button to chain skills automatically (like *Monk: Triple Attack → Chain Combo → Combo Finish → Asura Strike*). 

---

## 🎤 Voice & Text Input

### Voice-to-Chat
- Press `Back + L1`. 
- The engine plays an activation sound.
- Speak naturally into your default Windows microphone.
- Once you stop speaking, the engine opens the RO chat (`Enter`), types the recognized text, and submits it.

### Daisy Wheel Keyboard
- Press `Back + R1` to open the overlay.
- Push the **Left Stick** into one of the 8 directions to select a character cluster.
- Press **A, B, X, or Y** to type the specific letter in that cluster.
- **L3:** Backspace
- **R3:** Spacebar
- **Start:** Submit to chat and close
- **B (without pointing stick):** Cancel and close