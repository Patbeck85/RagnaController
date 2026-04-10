using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RagnaController.Core
{
    public static class InputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint n, INPUT[] p, int s);
        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint c, uint t);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("ntdll.dll", EntryPoint = "wine_get_version", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr wine_get_version();

        private static readonly int Size = Marshal.SizeOf<INPUT>();
        private static readonly bool IsWine;
        private static readonly int KeyDelay;
        private static readonly int ClickDelay;

        static InputSimulator()
        {
            try { IsWine = wine_get_version() != IntPtr.Zero; } catch { IsWine = false; }
            KeyDelay = IsWine ? 25 : 12;
            ClickDelay = IsWine ? 18 : 10;
        }

        private static volatile bool _leftClickInFlight = false;
        private static volatile bool _rightClickInFlight = false;
        private static volatile bool _isChatting = false;
        private static volatile bool _shutdownRequested = false;

        /// <summary>Call once on app shutdown to abort any in-flight chat operations.</summary>
        public static void RequestShutdown() => _shutdownRequested = true;

        public static void MoveMouseAbsolute(int x, int y) => SetCursorPos(x, y);
        public static void MoveMouseRelative(int dx, int dy) { if (dx == 0 && dy == 0) return; var i = new INPUT[1]; i[0].type = 0; i[0].Data.mi.dx = dx; i[0].Data.mi.dy = dy; i[0].Data.mi.dwFlags = 0x0001 | 0x2000; SendInput(1, i, Size); } // MOVE | MOVE_NOCOALESCE

        /// <summary>Opens the RO chat, types the text, and submits it with RO-optimised timings.</summary>
        public static async System.Threading.Tasks.Task SendChatString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Serialise concurrent callers — prevents key interleaving when two emotes fire in rapid succession
            while (_isChatting && !_shutdownRequested) await System.Threading.Tasks.Task.Delay(50);
            if (_shutdownRequested) return;
            _isChatting = true;
            try
            {
                // 1. Open chat
                TapKey(VirtualKey.Enter);

                // 2. Wait for RO chat UI to become ready (~2-3 frames at 60fps = ~50ms, 150ms gives safe margin)
                await System.Threading.Tasks.Task.Delay(150);

                // 3. Type text as Unicode key events
                var inputs = new INPUT[text.Length * 2];
                for (int i = 0; i < text.Length; i++)
                {
                    inputs[i * 2].type = 1;
                    inputs[i * 2].Data.ki.wVk    = 0;
                    inputs[i * 2].Data.ki.wScan  = (ushort)text[i];
                    inputs[i * 2].Data.ki.dwFlags = 0x0004; // KEYEVENTF_UNICODE
                    inputs[i * 2 + 1].type = 1;
                    inputs[i * 2 + 1].Data.ki.wVk    = 0;
                    inputs[i * 2 + 1].Data.ki.wScan  = (ushort)text[i];
                    inputs[i * 2 + 1].Data.ki.dwFlags = 0x0004 | 0x0002; // UNICODE | KEYUP
                }
                SendInput((uint)inputs.Length, inputs, Size);

                // 4. Wait for RO chat buffer to fully process (100ms prevents text getting stuck)
                await System.Threading.Tasks.Task.Delay(100);

                // 5. Submit
                TapKey(VirtualKey.Enter);
            }
            finally
            {
                _isChatting = false;
            }
        }
        public static void LeftClick() {
            if (_leftClickInFlight) return;
            _leftClickInFlight = true;
            Task.Run(async () => { try { Mouse(2); await Task.Delay(ClickDelay); Mouse(4); } finally { _leftClickInFlight = false; } });
        }
        public static void RightClick() {
            if (_rightClickInFlight) return;
            _rightClickInFlight = true;
            Task.Run(async () => { try { Mouse(8); await Task.Delay(ClickDelay); Mouse(16); } finally { _rightClickInFlight = false; } });
        }
        public static void DoubleClick() => Task.Run(async () => { Mouse(2); await Task.Delay(ClickDelay); Mouse(4); await Task.Delay(IsWine ? 50 : 25); Mouse(2); await Task.Delay(ClickDelay); Mouse(4); });
        public static void KeyDown(VirtualKey k) => Key(k, 8);
        public static void KeyUp(VirtualKey k) => Key(k, 10);
        public static void TapKey(VirtualKey k) => Task.Run(async () => { Key(k, 8); await Task.Delay(KeyDelay); Key(k, 10); });
        public static void TapKeyWithModifier(VirtualKey m, VirtualKey k) => Task.Run(async () => { Key(m, 8); await Task.Delay(ClickDelay); Key(k, 8); await Task.Delay(KeyDelay); Key(k, 10); await Task.Delay(ClickDelay); Key(m, 10); });
        public static void PanicHeal(VirtualKey k) => Task.Run(async () => { for (int i = 0; i < 10; i++) { Key(k, 8); await Task.Delay(IsWine ? 15 : 8); Key(k, 10); await Task.Delay(IsWine ? 15 : 8); } });
        public static void ScrollWheel(int d) { var i = new INPUT[1]; i[0].type = 0; i[0].Data.mi.dwFlags = 2048; i[0].Data.mi.mouseData = (uint)d; SendInput(1, i, Size); }
        private static void Mouse(uint f) { var i = new INPUT[1]; i[0].type = 0; i[0].Data.mi.dwFlags = f; SendInput(1, i, Size); }
        private static void Key(VirtualKey k, uint f) { var i = new INPUT[1]; i[0].type = 1; i[0].Data.ki.wVk = (ushort)k; i[0].Data.ki.wScan = (ushort)MapVirtualKey((uint)k, 0); if ((ushort)k >= 33 && (ushort)k <= 46) f |= 1; i[0].Data.ki.dwFlags = f; SendInput(1, i, Size); }

        [StructLayout(LayoutKind.Explicit)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    }

    public enum VirtualKey : ushort { None = 0, Tab = 9, Escape = 27, Space = 32, Enter = 13, Insert = 45, Delete = 46, A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90, F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117, F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123, Num1 = 49, Num2 = 50, Num3 = 51, Num4 = 52, Num5 = 53, Num6 = 54, Num7 = 55, Num8 = 56, Num9 = 57, Num0 = 48, ControlLeft = 162, AltLeft = 164, ShiftLeft = 160 }
}