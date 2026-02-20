using System;
using SharpDX.XInput;
using SDL2;

namespace RagnaController.Controller
{
    /// <summary>
    /// Universal controller service supporting Xbox (XInput) and PlayStation (SDL2) controllers.
    /// Auto-detects available controller and provides unified Gamepad interface.
    /// </summary>
    public class ControllerService : IDisposable
    {
        // ── Backend selection ─────────────────────────────────────────────────────
        private ControllerBackend _backend = ControllerBackend.None;
        private SharpDX.XInput.Controller? _xinputController;
        private IntPtr _sdlController = IntPtr.Zero;
        private IntPtr _sdlJoystick  = IntPtr.Zero;

        // ── State ─────────────────────────────────────────────────────────────────
        public bool   IsConnected    { get; private set; }
        public string ControllerName { get; private set; } = "No Controller";
        public string ControllerType { get; private set; } = "Unknown";

        // ── Constructor ───────────────────────────────────────────────────────────
        public ControllerService()
        {
            DetectController();
        }

        // ── Detection ─────────────────────────────────────────────────────────────
        private void DetectController()
        {
            // Try XInput first (Xbox controllers)
            if (TryInitXInput())
            {
                _backend       = ControllerBackend.XInput;
                IsConnected    = true;
                ControllerName = "Xbox Controller";
                ControllerType = "Xbox";
                return;
            }

            // Try SDL2 (PlayStation, generic, etc.)
            if (TryInitSDL())
            {
                _backend       = ControllerBackend.SDL2;
                IsConnected    = true;
                // Controller name set in TryInitSDL
                return;
            }

            // No controller found
            _backend       = ControllerBackend.None;
            IsConnected    = false;
            ControllerName = "No Controller";
            ControllerType = "Unknown";
        }

        private bool TryInitXInput()
        {
            try
            {
                _xinputController = new SharpDX.XInput.Controller(UserIndex.One);
                return _xinputController.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        private bool TryInitSDL()
        {
            try
            {
                // Initialize SDL2 GameController subsystem
                if (SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER) < 0)
                    return false;

                // Check for any connected game controllers
                int numJoysticks = SDL.SDL_NumJoysticks();
                if (numJoysticks == 0)
                {
                    SDL.SDL_Quit();
                    return false;
                }

                // Open first available game controller
                for (int i = 0; i < numJoysticks; i++)
                {
                    if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
                    {
                        _sdlController = SDL.SDL_GameControllerOpen(i);
                        if (_sdlController != IntPtr.Zero)
                        {
                            _sdlJoystick = SDL.SDL_GameControllerGetJoystick(_sdlController);
                            
                            // Get controller name
                            IntPtr namePtr = SDL.SDL_GameControllerName(_sdlController);
                            ControllerName = SDL.UTF8_ToManaged(namePtr) ?? "SDL Controller";

                            // Detect type
                            if (ControllerName.Contains("PlayStation") || ControllerName.Contains("DualShock") || 
                                ControllerName.Contains("PS4") || ControllerName.Contains("PS5") || 
                                ControllerName.Contains("DualSense"))
                            {
                                ControllerType = ControllerName.Contains("PS5") || ControllerName.Contains("DualSense") 
                                    ? "PlayStation 5" 
                                    : "PlayStation 4";
                            }
                            else
                            {
                                ControllerType = "Generic";
                            }

                            return true;
                        }
                    }
                }

                SDL.SDL_Quit();
                return false;
            }
            catch
            {
                return false;
            }
        }

        // ── Read State ────────────────────────────────────────────────────────────

        /// <summary>
        /// Get current gamepad state in unified format.
        /// Returns null if no controller connected.
        /// </summary>
        public Gamepad? GetGamepad()
        {
            if (!IsConnected)
                return null;

            return _backend switch
            {
                ControllerBackend.XInput => GetXInputGamepad(),
                ControllerBackend.SDL2   => GetSDLGamepad(),
                _                        => null
            };
        }

        private Gamepad GetXInputGamepad()
        {
            if (_xinputController == null || !_xinputController.IsConnected)
            {
                IsConnected = false;
                return default;
            }

            var state = _xinputController.GetState().Gamepad;

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

        private Gamepad GetSDLGamepad()
        {
            if (_sdlController == IntPtr.Zero)
            {
                IsConnected = false;
                return default;
            }

            // Poll events to update state
            SDL.SDL_GameControllerUpdate();

            // Read axes (16-bit signed: -32768 to 32767)
            short leftX  = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
            short leftY  = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
            short rightX = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
            short rightY = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);

            // Read triggers (0-32767 in SDL, we map to 0-255 for consistency)
            short leftTriggerRaw  = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT);
            short rightTriggerRaw = SDL.SDL_GameControllerGetAxis(_sdlController, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT);
            byte leftTrigger  = (byte)((leftTriggerRaw  + 32768) / 257);
            byte rightTrigger = (byte)((rightTriggerRaw + 32768) / 257);

            // Read buttons and map to XInput GamepadButtonFlags
            GamepadButtonFlags buttons = 0;

            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A) == 1)
                buttons |= GamepadButtonFlags.A;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B) == 1)
                buttons |= GamepadButtonFlags.B;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X) == 1)
                buttons |= GamepadButtonFlags.X;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y) == 1)
                buttons |= GamepadButtonFlags.Y;

            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER) == 1)
                buttons |= GamepadButtonFlags.LeftShoulder;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER) == 1)
                buttons |= GamepadButtonFlags.RightShoulder;

            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK) == 1)
                buttons |= GamepadButtonFlags.Back;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START) == 1)
                buttons |= GamepadButtonFlags.Start;

            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK) == 1)
                buttons |= GamepadButtonFlags.LeftThumb;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK) == 1)
                buttons |= GamepadButtonFlags.RightThumb;

            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP) == 1)
                buttons |= GamepadButtonFlags.DPadUp;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN) == 1)
                buttons |= GamepadButtonFlags.DPadDown;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT) == 1)
                buttons |= GamepadButtonFlags.DPadLeft;
            if (SDL.SDL_GameControllerGetButton(_sdlController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT) == 1)
                buttons |= GamepadButtonFlags.DPadRight;

            return new Gamepad
            {
                LeftThumbX   = leftX,
                LeftThumbY   = leftY,
                RightThumbX  = rightX,
                RightThumbY  = rightY,
                LeftTrigger  = leftTrigger,
                RightTrigger = rightTrigger,
                Buttons      = buttons
            };
        }

        // ── Rumble (optional) ─────────────────────────────────────────────────────

        public void SetRumble(float leftMotor, float rightMotor, int durationMs)
        {
            if (!IsConnected) return;

            switch (_backend)
            {
                case ControllerBackend.XInput:
                    if (_xinputController != null)
                    {
                        var vibration = new Vibration
                        {
                            LeftMotorSpeed  = (ushort)(leftMotor * 65535f),
                            RightMotorSpeed = (ushort)(rightMotor * 65535f)
                        };
                        _xinputController.SetVibration(vibration);
                    }
                    break;

                case ControllerBackend.SDL2:
                    if (_sdlController != IntPtr.Zero)
                    {
                        // SDL2 rumble: low frequency (left) and high frequency (right)
                        ushort lowFreq  = (ushort)(leftMotor  * 65535f);
                        ushort highFreq = (ushort)(rightMotor * 65535f);
                        SDL.SDL_GameControllerRumble(_sdlController, lowFreq, highFreq, (uint)durationMs);
                    }
                    break;
            }
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_sdlController != IntPtr.Zero)
            {
                SDL.SDL_GameControllerClose(_sdlController);
                SDL.SDL_Quit();
                _sdlController = IntPtr.Zero;
            }

            _xinputController = null;
            GC.SuppressFinalize(this);
        }
    }

    // ── Supporting types ──────────────────────────────────────────────────────────

    internal enum ControllerBackend
    {
        None,
        XInput,  // Xbox controllers
        SDL2     // PlayStation, generic, etc.
    }
}
