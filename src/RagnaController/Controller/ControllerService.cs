using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using SharpDX.XInput;

namespace RagnaController.Controller
{
    public class ControllerService : IDisposable
    {
        private SharpDX.XInput.Controller? _ctrl;
        private bool _isDetecting;
        public bool IsConnected { get; private set; }
        public string ControllerName { get; private set; } = "No Controller";
        public string ControllerType { get; private set; } = "Unknown";

        public ControllerService() => DetectController();

        public async void DetectController()
        {
            if (_isDetecting) return;
            _isDetecting = true;
            await Task.Run(() => {
                UserIndex[] ids = { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four };
                foreach (var id in ids) {
                    try {
                        var temp = new SharpDX.XInput.Controller(id);
                        if (temp.IsConnected) {
                            _ctrl = temp; IsConnected = true;
                            ControllerType = GetTypeViaWMI();
                            ControllerName = ControllerType + " (" + id + ")";
                            return;
                        }
                    } catch { }
                }
                IsConnected = false; ControllerName = "No Controller"; ControllerType = "Unknown";
            });
            _isDetecting = false;
        }

        public Gamepad? GetGamepad() {
            if (_ctrl == null || !IsConnected) return null;
            try { var s = _ctrl.GetState().Gamepad; IsConnected = true; return new Gamepad { LeftThumbX = s.LeftThumbX, LeftThumbY = s.LeftThumbY, RightThumbX = s.RightThumbX, RightThumbY = s.RightThumbY, LeftTrigger = s.LeftTrigger, RightTrigger = s.RightTrigger, Buttons = s.Buttons }; }
            catch { IsConnected = false; return null; }
        }

        public void SetRumble(float l, float r) {
            if (_ctrl == null || !IsConnected) return;
            try { _ctrl.SetVibration(new Vibration { LeftMotorSpeed = (ushort)(Math.Clamp(l, 0f, 1f) * 65535), RightMotorSpeed = (ushort)(Math.Clamp(r, 0f, 1f) * 65535) }); }
            catch { IsConnected = false; }
        }

        private string GetTypeViaWMI()
        {
            try
            {
                // Query only real gamepad/joystick devices, exclude mice and keyboards
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, HardwareID FROM Win32_PnPEntity " +
                    "WHERE PNPClass = 'HIDClass' OR PNPClass = 'XnaComposite' OR PNPClass = 'XboxComposite'");

                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                {
                    using (obj)
                    {
                        string name = obj["Name"]?.ToString() ?? "";
                        string hw   = obj["HardwareID"] is string[] ids
                            ? string.Join(" ", ids).ToUpper()
                            : "";

                        // Explicitly exclude mice and keyboards
                        if (name.IndexOf("mouse",    StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (name.IndexOf("keyboard", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (name.IndexOf("tastatur", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        // Xbox / Microsoft — VID_045E
                        if (hw.Contains("VID_045E") ||
                            name.IndexOf("Xbox",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("XInput",  StringComparison.OrdinalIgnoreCase) >= 0)
                            return "Xbox";

                        // PS5 DualSense — VID_054C PID_0CE6 / PID_0DF2
                        if (name.IndexOf("DualSense",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                            hw.Contains("PID_0CE6") || hw.Contains("PID_0DF2"))
                            return "PS5";

                        // PS4 DualShock — VID_054C
                        if (name.IndexOf("DualShock",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (hw.Contains("VID_054C") && !hw.Contains("PID_0CE6") && !hw.Contains("PID_0DF2")))
                            return "PS4";

                        // Nintendo Switch Pro
                        if (name.IndexOf("Switch",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                            hw.Contains("VID_057E"))
                            return "Switch";

                        // 8BitDo
                        if (hw.Contains("VID_2DC8"))
                            return "8BitDo";

                        // Logitech Gamepad (only if it is a gamepad, not a mouse/keyboard)
                        if (hw.Contains("VID_046D") &&
                            (name.IndexOf("gamepad",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("joystick",   StringComparison.OrdinalIgnoreCase) >= 0))
                            return "Logitech";
                    }
                }
            }
            catch { }
            // Fallback: SharpDX hat den Controller erkannt → muss Xbox-kompatibel sein
            return "Xbox";
        }

        public string GetBatteryLevel() {
            if (_ctrl == null || !IsConnected) return "Unknown";
            try { var i = _ctrl.GetBatteryInformation(BatteryDeviceType.Gamepad); return i.BatteryLevel.ToString(); }
            catch { return "Unknown"; }
        }

        public void Dispose() { try { SetRumble(0, 0); } catch { } _ctrl = null; IsConnected = false; GC.SuppressFinalize(this); }
    }
}