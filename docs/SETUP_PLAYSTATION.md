# PlayStation Controller Setup (PS4 / PS5)

RagnaController uses **XInput** as its core input API — the same native API used by Xbox controllers.  
Because Sony's PS4 (DualShock 4) and PS5 (DualSense) controllers use their own direct protocol, Windows does not natively translate their inputs to XInput out of the box.

To use a PlayStation controller with RagnaController, you need a translation layer. The best and most stable tool for this is **DS4Windows**.

---

## Why DS4Windows?

When you plug in a PlayStation controller, Windows recognizes it as a generic "HID (Human Interface Device)" gamepad, not as an XInput device.  
DS4Windows runs silently in the background, grabs the Sony protocol, and presents the controller to Windows as a perfectly functioning virtual Xbox pad.

*The magic part:* Even though DS4Windows disguises your controller as an Xbox pad, RagnaController has an advanced background hardware scanner (WMI query). It looks past the virtual Xbox pad, detects the real Sony hardware plugged into your USB port, and correctly displays the blue **PS4** or **PS5** badge and matching button prompts in the app!

---

## Step-by-Step Setup

### 1. Download DS4Windows

**Official site:** [https://ds4-windows.com](https://ds4-windows.com)  
Choose the latest release ZIP file.

> ⚠ **Warning:** Only download from the official site. Avoid unofficial mirrors or older discontinued forks (like the original Jays2Kings version).

### 2. Install and Run

1. Extract the downloaded ZIP anywhere on your PC (e.g., `C:\Tools\DS4Windows\`).  
2. DS4Windows is portable; there is no installer for the app itself. Just run `DS4Windows.exe`.
3. On the very first launch, it will prompt you to install the **ViGEmBus driver**.  
   👉 **Click Yes and install it.** This is the core driver that creates the virtual Xbox controller.

### 3. Connect your Controller

- **USB:** Plug it in using a USB-C (PS5) or Micro-USB (PS4) cable.  
- **Bluetooth:** Hold the **PS Button + Share Button** until the lightbar starts flashing rapidly. Then pair it in your Windows Bluetooth settings.

Once connected, the controller should appear in the "Controllers" tab of DS4Windows.

### 4. CRITICAL: Enable "Hide DS4 Controller"

This is the most important step for RagnaController to work without "Double Input" glitches:

1. In DS4Windows, go to the **Settings** tab.
2. Check the box for **"Hide DS4 Controller"**.
3. *Why?* If this is off, Windows sees *two* controllers: the real PS4 pad and the virtual Xbox pad. This can cause RagnaController to receive double inputs or behave erratically. Hiding the real pad forces Windows to only see the clean, virtual XInput pad.

*(Note: Depending on your Windows version, you might need to install a tool called "HidHide" if Windows refuses to hide the controller. DS4Windows will guide you if that happens).*

### 5. Launch RagnaController

Make sure DS4Windows is running and your controller is connected **before** starting RagnaController.

Look at the top right header of the RagnaController window:
- It should display a glowing blue badge saying **PS4** or **PS5**.
- If it says **Xbox**, RagnaController couldn't read the physical USB port name, but the inputs will still work perfectly 1:1.

---

## Auto-Start DS4Windows with Windows (Recommended)

To never worry about this again:
1. Open DS4Windows → Go to **Settings**
2. Check **"Run at Startup"**
3. Check **"Start Minimized"**

Now, whenever you turn on your PC and grab your controller, it will immediately work with RagnaController.

---

## Troubleshooting FAQ

| Problem | Solution |
|---|---|
| **Controller not detected in DS4Windows** | Re-plug the USB cable, or unpair/re-pair via Bluetooth. Try a different USB port. |
| **ViGEmBus not installed error** | Reinstall DS4Windows, make sure to accept the driver installation prompt at startup. |
| **Badge shows XBOX instead of PS4/PS5** | This is purely cosmetic. It means Windows hid the hardware ID so well that RagnaController couldn't read the Sony brand. The controls will still work 100%. |
| **RagnaController shows "NO CONTROLLER"** | Start DS4Windows *first*, wait for the controller to connect, and then start RagnaController. |
| **Buttons fire twice / Controller goes crazy** | You forgot Step 4. Enable **"Hide DS4 Controller"** in the DS4Windows settings! |
| **Battery always shows "🔌 WIRED"** | This is intentional. When connected via USB, the XInput API does not report a percentage, only that the device is receiving wired power. Connect via Bluetooth to see the percentage. |
| **DualSense adaptive triggers aren't working** | Expected. The XInput API (which Ragnarok Online relies on) does not support PS5 haptic triggers. They will function as standard analog triggers. |

---

## Alternative Software

If you do not want to use DS4Windows, here are alternative ways to get XInput from a PlayStation controller:

| Tool | Notes |
|---|---|
| **Steam (Big Picture)** | Add RagnaController as a "Non-Steam Game". Enable Steam Input for PlayStation controllers. Steam will translate the inputs. *Drawback: Steam must always run in the background.* |
| **DsHidMini** | A driver-level HID-to-XInput bridge. Much lighter on system resources than DS4Windows, but it has no graphical user interface. For advanced users. |

---

## Tested Configurations (v1.2.0)

We have verified 100% compatibility with the following hardware running through DS4Windows:

| Controller | Connection | DS4Windows | RagnaController Status |
|---|---|---|---|
| DualShock 4 v1 | USB | Latest | ✅ Full support |
| DualShock 4 v2 | Bluetooth | Latest | ✅ Full support |
| DualSense (PS5) | USB | Latest | ✅ Full support |
| DualSense Edge | USB | Latest | ✅ Full support |