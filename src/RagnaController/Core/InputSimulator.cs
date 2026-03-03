using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RagnaController.Core
{
    /// <summary>
    /// Highly optimized input simulator for Ragnarok Online.
    /// Combines hardware scan codes, absolute positioning and RO-compatible timing.
    /// </summary>
    public static class InputSimulator
    {
        // ── Windows API Imports ──────────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private static readonly int InputSize = Marshal.SizeOf<INPUT>();

        // ── Mouse functions ──────────────────────────────────────────────────────

        /// <summary>Sets the cursor to an exact screen coordinate (important for MovementEngine).</summary>
        public static void MoveMouseAbsolute(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>Moves the cursor relative to its current position.</summary>
        public static void MoveMouseRelative(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            var inputs = new INPUT[1];
            inputs[0].type = 0; // Mouse
            inputs[0].Data.mi.dx = dx;
            inputs[0].Data.mi.dy = dy;
            inputs[0].Data.mi.dwFlags = 0x0001; // MOUSEEVENTF_MOVE
            SendInput(1, inputs, InputSize);
        }

        public static void LeftClick()
        {
            LeftButtonDown();
            Thread.Sleep(8); // Minimum hold for RO to register (1 tick at 125fps)
            LeftButtonUp();
        }

        public static void RightClick()
        {
            var inputs = new INPUT[1];
            inputs[0].type = 0;
            inputs[0].Data.mi.dwFlags = 0x0008; // RightDown
            SendInput(1, inputs, InputSize);
            Thread.Sleep(8); // Minimum hold for RO to register
            inputs[0].Data.mi.dwFlags = 0x0010; // RightUp
            SendInput(1, inputs, InputSize);
        }

        public static void LeftButtonDown()
        {
            var inputs = new INPUT[1];
            inputs[0].type = 0;
            inputs[0].Data.mi.dwFlags = 0x0002; // LeftDown
            SendInput(1, inputs, InputSize);
        }

        public static void LeftButtonUp()
        {
            var inputs = new INPUT[1];
            inputs[0].type = 0;
            inputs[0].Data.mi.dwFlags = 0x0004; // LeftUp
            SendInput(1, inputs, InputSize);
        }

        public static void ScrollWheel(int delta)
        {
            var inputs = new INPUT[1];
            inputs[0].type = 0;
            inputs[0].Data.mi.dwFlags = 0x0800; // Wheel
            inputs[0].Data.mi.mouseData = (uint)delta;
            SendInput(1, inputs, InputSize);
        }

        // ── Keyboard functions ────────────────────────────────────────────────────

        public static void DoubleClick()
        {
            LeftClick();
            System.Threading.Thread.Sleep(16);
            LeftClick();
        }

        public static void TapKeyWithModifier(VirtualKey modifier, VirtualKey key)
        {
            KeyDown(modifier);
            System.Threading.Thread.Sleep(8);
            TapKey(key);
            System.Threading.Thread.Sleep(8);
            KeyUp(modifier);
        }

        public static void TapKey(VirtualKey key)
        {
            if (key == VirtualKey.None) return;
            KeyDown(key);
            Thread.Sleep(8);  // Minimum key hold for RO to register
            KeyUp(key);
        }

        public static void KeyDown(VirtualKey key)
        {
            if (key == VirtualKey.None) return;
            var inputs = new INPUT[1];
            inputs[0].type = 1; // Keyboard
            inputs[0].Data.ki.wVk = (ushort)key;
            inputs[0].Data.ki.wScan = (ushort)MapVirtualKey((uint)key, 0);
            
            // Flag logic: ScanCode required for RO, Extended for keys like Insert/Del
            uint flags = 0x0008; // KEYEVENTF_SCANCODE
            if (IsExtendedKey(key)) flags |= 0x0001; // KEYEVENTF_EXTENDEDKEY

            inputs[0].Data.ki.dwFlags = flags;
            SendInput(1, inputs, InputSize);
        }

        public static void KeyUp(VirtualKey key)
        {
            if (key == VirtualKey.None) return;
            var inputs = new INPUT[1];
            inputs[0].type = 1;
            inputs[0].Data.ki.wVk = (ushort)key;
            inputs[0].Data.ki.wScan = (ushort)MapVirtualKey((uint)key, 0);
            
            uint flags = 0x0008 | 0x0002; // SCANCODE + KEYUP
            if (IsExtendedKey(key)) flags |= 0x0001;

            inputs[0].Data.ki.dwFlags = flags;
            SendInput(1, inputs, InputSize);
        }

        private static bool IsExtendedKey(VirtualKey key)
        {
            ushort v = (ushort)key;
            // Insert, Delete, Home, End, PageUp, PageDown, arrow keys
            return v == 0x2D || v == 0x2E || (v >= 0x21 && v <= 0x28);
        }

        // ── Win32 API Structures ─────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion Data; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    }

    /// <summary>Complete enum of all required keys.</summary>
    public enum VirtualKey : ushort
    {
        None = 0, Tab = 0x09, Escape = 0x1B, Space = 0x20, Enter = 0x0D, Insert = 0x2D, Delete = 0x2E,
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47, H = 0x48,
        I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F, P = 0x50,
        Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77,
        F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,
        Num1 = 0x31, Num2 = 0x32, Num3 = 0x33, Num4 = 0x34, Num5 = 0x35, Num6 = 0x36, Num7 = 0x37, Num8 = 0x38, Num9 = 0x39, Num0 = 0x30,
        ControlLeft = 0xA2, AltLeft = 0xA4, ShiftLeft = 0xA0
    }
}