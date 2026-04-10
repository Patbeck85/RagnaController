# PlayStation Controller Setup

RagnaController uses **XInput** — the same API as Xbox controllers.  
PS4 and PS5 controllers speak their own protocol and need a translation layer.

---

## Why DS4Windows?

Sony controllers connect as a generic HID device, not as XInput.  
DS4Windows runs in the background and presents the controller to Windows as a virtual Xbox pad.  
RagnaController then receives full XInput data (sticks, triggers, buttons, battery level) and
shows the correct **PS4** or **PS5** badge in the header.

---

## Step-by-Step Setup

### 1. Download DS4Windows

**Official site:** https://ds4-windows.com  
Choose the latest release ZIP.

> ⚠ Avoid unofficial mirrors. The linked site is the current maintained fork.

### 2. Install

Extract the ZIP anywhere (e.g. `C:\Tools\DS4Windows\`).  
No installer needed — it's portable.  
On first launch it will prompt to install the **ViGEmBus driver** — click Yes. This is required.

### 3. Connect your controller

**USB:** Plug in with a USB-C (DualSense) or Micro-USB (DS4) cable.  
**Bluetooth:** Hold PS + Share until the lightbar flashes, then pair in Windows Bluetooth settings.

### 4. Enable XInput mode

In DS4Windows:
1. Go to **Settings** tab
2. Enable **"Hide DS4 Controller"** — this hides the raw Sony device and only exposes the virtual Xbox pad
3. The controller should show a green checkmark in the Controllers tab

### 5. Launch RagnaController

Start RagnaController **after** DS4Windows is running.  
The badge in the header should show **PS4** (blue) or **PS5** (blue).

---

## Auto-Start DS4Windows with Windows

1. Open DS4Windows → Settings → **"Start with Windows"**
2. Or create a shortcut in `shell:startup` (`Win+R` → type `shell:startup`)

With Auto-Start enabled, DS4Windows is ready before you launch RagnaController.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Controller not detected in DS4Windows | Replug USB / re-pair Bluetooth · Try a different USB port |
| ViGEmBus not installed | Reinstall DS4Windows, accept driver prompt |
| Badge shows XBOX instead of PS4/PS5 | "Hide DS4 Controller" not enabled in DS4Windows |
| Two controllers appear in Windows | "Hide DS4 Controller" is off — enable it |
| Bluetooth keeps disconnecting | System power plan may suspend USB — set to High Performance |
| DS4Windows crashes on start | Install [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0) |
| RagnaController shows NO CONTROLLER | Start DS4Windows before RagnaController |
| Button mapping feels wrong | DS4Windows may be remapping buttons — use default profile in DS4Windows |
| Battery not showing | Only visible over Bluetooth. USB always shows 🔌 WIRED |
| DualSense adaptive triggers not working | Expected — XInput does not support DualSense haptic triggers |

---

## Alternative Software

| Tool | Notes |
|---|---|
| **Steam (Big Picture)** | Enable Steam Input for the controller, set to Xbox layout. Works but Steam must be running. |
| **DsHidMini** | Driver-level HID-to-XInput bridge. Lighter than DS4Windows but no GUI. |
| **Ryochan7 DS4Windows** | Older maintained fork — use official ds4-windows.com instead. |

---

## Tested Configurations

| Controller | Connection | DS4Windows version | Result |
|---|---|---|---|
| DualShock 4 v1 | USB | Latest | ✅ Full support |
| DualShock 4 v2 | Bluetooth | Latest | ✅ Full support |
| DualSense (PS5) | USB | Latest | ✅ Full support |
| DualSense Edge | USB | Latest | ✅ Full support |

---

## Technical Notes

RagnaController detects PS controllers via WMI (`Win32_PnPEntity`) even when DS4Windows hides the raw device.  
WMI lists both the hidden physical device (VID_054C) and the virtual Xbox device simultaneously,
so brand detection works correctly.

Sony VIDs and PIDs checked:

| Device | VID | PID |
|---|---|---|
| DualSense | 054C | 0CE6 |
| DualSense Edge | 054C | 0DF2 |
| DualShock 4 v1 | 054C | 05C4 |
| DualShock 4 v2 | 054C | 09CC |
| DS4 Back Button | 054C | 0BA0 |
