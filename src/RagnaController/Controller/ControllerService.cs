using System;
using System.Linq;
using System.Management;
using SharpDX.XInput;

namespace RagnaController.Controller
{
    public class ControllerService : IDisposable
    {
        private SharpDX.XInput.Controller? _xinputController;
        private UserIndex _userIndex = UserIndex.Any;

        public bool   IsConnected    { get; private set; }
        public string ControllerName { get; private set; } = "No Controller";
        public string ControllerType { get; private set; } = "Unknown";

        public ControllerService()
        {
            DetectController();
        }

        public void DetectController()
        {
            UserIndex[] indexes = { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four };

            foreach (var index in indexes)
            {
                try
                {
                    var tempController = new SharpDX.XInput.Controller(index);
                    if (tempController.IsConnected)
                    {
                        _xinputController = tempController;
                        _userIndex = index;
                        IsConnected    = true;
                        // Detect actual controller brand via WMI HID device names
                        string detectedType = DetectControllerType();
                        ControllerType = detectedType;
                        ControllerName = detectedType switch
                        {
                            "PS5"         => $"DualSense (Slot {(int)index + 1})",
                            "PS4"         => $"DualShock 4 (Slot {(int)index + 1})",
                            "Switch"      => $"Switch Pro (Slot {(int)index + 1})",
                            "8BitDo"      => $"8BitDo Controller (Slot {(int)index + 1})",
                            "Logitech"    => $"Logitech Gamepad (Slot {(int)index + 1})",
                            "Razer"       => $"Razer Controller (Slot {(int)index + 1})",
                            "Thrustmaster"=> $"Thrustmaster (Slot {(int)index + 1})",
                            _             => $"Xbox Controller (Slot {(int)index + 1})"
                        };
                        return;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerService] Slot error: {ex.Message}"); }
            }

            IsConnected = false;
            ControllerName = "No Controller";
            ControllerType = "Unknown";
        }

        public Gamepad? GetGamepad()
        {
            // Do NOT call DetectController() here — that triggers a slow WMI query
            // on every tick while disconnected, freezing the UI thread for 300+ ms.
            // Reconnection is driven exclusively by HybridEngine._reconnectCounter (every ~2 s).
            if (_xinputController == null || !IsConnected) return null;

            try
            {
                var state = _xinputController.GetState().Gamepad;
                IsConnected = true; // still alive
                return new Gamepad
                {
                    LeftThumbX     = state.LeftThumbX,
                    LeftThumbY     = state.LeftThumbY,
                    RightThumbX    = state.RightThumbX,
                    RightThumbY    = state.RightThumbY,
                    LeftTrigger    = state.LeftTrigger,
                    RightTrigger   = state.RightTrigger,
                    Buttons        = state.Buttons
                };
            }
            catch
            {
                IsConnected = false;
                return null;
            }
        }

        /// <summary>
        /// Sets vibration intensity. FeedbackSystem is responsible for stopping it.
        /// </summary>
        public void SetRumble(float leftMotor, float rightMotor)
        {
            if (_xinputController == null || !IsConnected) return;

            try
            {
                _xinputController.SetVibration(new Vibration
                {
                    LeftMotorSpeed  = (ushort)(leftMotor * 65535f),
                    RightMotorSpeed = (ushort)(rightMotor * 65535f)
                });
            }
            catch (Exception ex) { IsConnected = false; System.Diagnostics.Debug.WriteLine($"[ControllerService] SetRumble error: {ex.Message}"); }
        }

        private static string DetectControllerType()
        {
            try
            {
                // Query all HIDClass devices — covers USB and Bluetooth gamepads
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, HardwareID FROM Win32_PnPEntity WHERE PNPClass = 'HIDClass'");

                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>().ToList())
                using (obj) // ManagementObject implements IDisposable — must be disposed to release COM reference
                {
                    string name  = obj["Name"]?.ToString() ?? "";
                    string hwIds = "";
                    if (obj["HardwareID"] is string[] ids)
                        hwIds = string.Join(" ", ids).ToUpperInvariant();

                    // ── Sony PlayStation 5 (DualSense) ─────────────────────────────────
                    // VID 054C | DualSense PID 0CE6 | DualSense Edge PID 0DF2
                    bool ps5ByName = name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                                  || name.Contains("PS5",       StringComparison.OrdinalIgnoreCase);
                    bool ps5ByVid  = hwIds.Contains("VID_054C")
                                  && (hwIds.Contains("PID_0CE6") || hwIds.Contains("PID_0DF2"));
                    if (ps5ByName || ps5ByVid) return "PS5";

                    // ── Sony PlayStation 4 (DualShock 4) ──────────────────────────────
                    // VID 054C | v1 PID 05C4 | v2 PID 09CC | Back Button PID 0BA0
                    bool ps4ByName = name.Contains("DualShock",          StringComparison.OrdinalIgnoreCase)
                                  || name.Contains("PS4",                StringComparison.OrdinalIgnoreCase)
                                  || name.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase);
                    bool ps4ByVid  = hwIds.Contains("VID_054C")
                                  && (hwIds.Contains("PID_05C4") || hwIds.Contains("PID_09CC") || hwIds.Contains("PID_0BA0"));
                    if (ps4ByName || ps4ByVid) return "PS4";

                    // ── Generic Sony fallback ──────────────────────────────────────────
                    if (hwIds.Contains("VID_054C") && name.Contains("Gamepad", StringComparison.OrdinalIgnoreCase))
                        return "PS4";

                    // ── Nintendo Switch Pro Controller ─────────────────────────────────
                    // VID 057E | Switch Pro PID 2009 | Switch Pro USB also 2009
                    bool switchByName = name.Contains("Nintendo",    StringComparison.OrdinalIgnoreCase)
                                     || name.Contains("Switch",      StringComparison.OrdinalIgnoreCase)
                                     || name.Contains("Pro Control", StringComparison.OrdinalIgnoreCase);
                    bool switchByVid  = hwIds.Contains("VID_057E")
                                     && (hwIds.Contains("PID_2009") || hwIds.Contains("PID_2007"));
                    if (switchByName || switchByVid) return "Switch";

                    // ── 8BitDo ─────────────────────────────────────────────────────────
                    // VID 2DC8 covers all 8BitDo models (SN30 Pro, Pro 2, Ultimate, etc.)
                    if (hwIds.Contains("VID_2DC8") || name.Contains("8BitDo", StringComparison.OrdinalIgnoreCase))
                        return "8BitDo";

                    // ── Logitech F310 / F710 ───────────────────────────────────────────
                    // VID 046D | F310 PID C21D | F710 PID C21F | F310 XInput PID C218
                    bool logitechByVid = hwIds.Contains("VID_046D")
                                      && (hwIds.Contains("PID_C21D") || hwIds.Contains("PID_C21F") || hwIds.Contains("PID_C218"));
                    bool logitechByName = name.Contains("Logitech", StringComparison.OrdinalIgnoreCase)
                                       && name.Contains("Gamepad",  StringComparison.OrdinalIgnoreCase);
                    if (logitechByVid || logitechByName) return "Logitech";

                    // ── Razer ──────────────────────────────────────────────────────────
                    // VID 1532 covers Razer Wolverine, Raiju, Sabertooth, etc.
                    if (hwIds.Contains("VID_1532") || name.Contains("Razer", StringComparison.OrdinalIgnoreCase))
                        return "Razer";

                    // ── Thrustmaster ───────────────────────────────────────────────────
                    // VID 044F
                    if (hwIds.Contains("VID_044F") || name.Contains("Thrustmaster", StringComparison.OrdinalIgnoreCase))
                        return "Thrustmaster";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerService] HID detection failed: {ex.Message}"); }
            return "Xbox";
        }

        /// <summary>Returns battery level as a string: "Full", "Medium", "Low", "Empty", or "Wired".</summary>
        public string GetBatteryLevel()
        {
            if (_xinputController == null || !IsConnected) return "Unknown";
            try
            {
                var info = _xinputController.GetBatteryInformation(BatteryDeviceType.Gamepad);
                if (info.BatteryType == BatteryType.Wired) return "Wired";
                return info.BatteryLevel switch
                {
                    BatteryLevel.Full   => "Full",
                    BatteryLevel.Medium => "Medium",
                    BatteryLevel.Low    => "Low",
                    BatteryLevel.Empty  => "Empty",
                    _                   => "Unknown"
                };
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerService] Battery error: {ex.Message}"); return "Unknown"; }
        }

        public void Dispose()
        {
            // Stop any active rumble before releasing the controller handle
            try { SetRumble(0f, 0f); } catch { /* ignore — controller may already be gone */ }
            _xinputController = null;
            IsConnected = false;
            GC.SuppressFinalize(this);
        }
    }

    internal enum ControllerBackend { None, XInput }
}