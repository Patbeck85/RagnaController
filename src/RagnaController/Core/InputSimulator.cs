using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Wraps the Windows SendInput API to simulate mouse and keyboard events.
    /// This does NOT read game memory, inject code or modify packets.
    /// </summary>
    public static class InputSimulator
    {
        // ── P/Invoke ────────────────────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static readonly int InputSize = Marshal.SizeOf<INPUT>();

        // ── Mouse ───────────────────────────────────────────────────────────────

        /// <summary>Moves the mouse cursor by a relative offset.</summary>
        public static void MoveMouseRelative(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;

            var inputs = new INPUT[1];
            inputs[0].type = InputType.Mouse;
            inputs[0].mi.dx = dx;
            inputs[0].mi.dy = dy;
            inputs[0].mi.dwFlags = MouseEventFlags.Move;

            SendInput(1, inputs, InputSize);
        }

        /// <summary>Sends a left mouse button down + up click.</summary>
        public static void LeftClick()
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputType.Mouse;
            inputs[0].mi.dwFlags = MouseEventFlags.LeftDown;
            inputs[1].type = InputType.Mouse;
            inputs[1].mi.dwFlags = MouseEventFlags.LeftUp;
            SendInput(2, inputs, InputSize);
        }

        /// <summary>Sends a right mouse button click.</summary>
        public static void RightClick()
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputType.Mouse;
            inputs[0].mi.dwFlags = MouseEventFlags.RightDown;
            inputs[1].type = InputType.Mouse;
            inputs[1].mi.dwFlags = MouseEventFlags.RightUp;
            SendInput(2, inputs, InputSize);
        }

        /// <summary>Presses or releases the left mouse button.</summary>
        public static void LeftButtonDown() => SendMouseFlag(MouseEventFlags.LeftDown);
        public static void LeftButtonUp()   => SendMouseFlag(MouseEventFlags.LeftUp);

        /// <summary>Simulates a mouse wheel scroll.</summary>
        public static void ScrollWheel(int delta)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputType.Mouse;
            inputs[0].mi.dwFlags = MouseEventFlags.Wheel;
            inputs[0].mi.mouseData = delta;
            SendInput(1, inputs, InputSize);
        }

        // ── Keyboard ────────────────────────────────────────────────────────────

        /// <summary>Taps a virtual key (down + up).</summary>
        public static void TapKey(VirtualKey key)
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputType.Keyboard;
            inputs[0].ki.wVk = (ushort)key;
            inputs[1].type = InputType.Keyboard;
            inputs[1].ki.wVk = (ushort)key;
            inputs[1].ki.dwFlags = KeyEventFlags.KeyUp;
            SendInput(2, inputs, InputSize);
        }

        /// <summary>Holds a virtual key down.</summary>
        public static void KeyDown(VirtualKey key)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputType.Keyboard;
            inputs[0].ki.wVk = (ushort)key;
            SendInput(1, inputs, InputSize);
        }

        /// <summary>Releases a virtual key.</summary>
        public static void KeyUp(VirtualKey key)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputType.Keyboard;
            inputs[0].ki.wVk = (ushort)key;
            inputs[0].ki.dwFlags = KeyEventFlags.KeyUp;
            SendInput(1, inputs, InputSize);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static void SendMouseFlag(MouseEventFlags flag)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputType.Mouse;
            inputs[0].mi.dwFlags = flag;
            SendInput(1, inputs, InputSize);
        }

        // ── Structs & Enums ─────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public InputType type;
            public InputUnion _; // union: mi / ki / hi
            public MOUSEINPUT mi => _.mi;
            public KEYBDINPUT ki => _.ki;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public MouseEventFlags dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public KeyEventFlags dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private enum InputType : uint { Mouse = 0, Keyboard = 1 }

        [Flags]
        private enum MouseEventFlags : uint
        {
            Move      = 0x0001,
            LeftDown  = 0x0002,
            LeftUp    = 0x0004,
            RightDown = 0x0008,
            RightUp   = 0x0010,
            Wheel     = 0x0800,
            Absolute  = 0x8000
        }

        [Flags]
        private enum KeyEventFlags : uint
        {
            None  = 0x0000,
            KeyUp = 0x0002
        }
    }

    /// <summary>Commonly used Windows virtual key codes.</summary>
    public enum VirtualKey : ushort
    {
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73,
        F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77,
        F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

        Tab    = 0x09,
        Escape = 0x1B,
        Space  = 0x20,
        Enter  = 0x0D,

        Num1 = 0x31, Num2 = 0x32, Num3 = 0x33, Num4 = 0x34,
        Num5 = 0x35, Num6 = 0x36, Num7 = 0x37, Num8 = 0x38,
        Num9 = 0x39, Num0 = 0x30,

        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
        G = 0x47, H = 0x48, I = 0x49, J = 0x4A, K = 0x4B,
        L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F, P = 0x50,
        Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
        V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

        CtrlLeft  = 0xA2,
        AltLeft   = 0xA4,
        ShiftLeft = 0xA0,
    }
}
