using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RagnaController.Core
{
    /// <summary>
    /// Global hotkey manager for profile switching (Ctrl+1-4).
    /// Uses Windows RegisterHotKey API.
    /// </summary>
    public class HotkeyManager : IDisposable
    {
        // ── Win32 API ─────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_CONTROL = 0x0002;

        // Hotkey IDs
        private const int HOTKEY_PROFILE_1 = 9001;
        private const int HOTKEY_PROFILE_2 = 9002;
        private const int HOTKEY_PROFILE_3 = 9003;
        private const int HOTKEY_PROFILE_4 = 9004;

        // ── State ─────────────────────────────────────────────────────────────────
        private IntPtr _windowHandle;
        private HwndSource? _source;
        public bool IsRegistered { get; private set; }

        // Events
        public event Action<int>? ProfileHotkeyPressed; // 1-4

        // ── Registration ──────────────────────────────────────────────────────────

        public bool Register(Window window)
        {
            if (IsRegistered) return true;

            _windowHandle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            
            if (_source == null) return false;

            _source.AddHook(HwndHook);

            // Register Ctrl+1 through Ctrl+4
            bool success = true;
            success &= RegisterHotKey(_windowHandle, HOTKEY_PROFILE_1, MOD_CONTROL, 0x31); // VK_1
            success &= RegisterHotKey(_windowHandle, HOTKEY_PROFILE_2, MOD_CONTROL, 0x32); // VK_2
            success &= RegisterHotKey(_windowHandle, HOTKEY_PROFILE_3, MOD_CONTROL, 0x33); // VK_3
            success &= RegisterHotKey(_windowHandle, HOTKEY_PROFILE_4, MOD_CONTROL, 0x34); // VK_4

            IsRegistered = success;
            return success;
        }

        public void Unregister()
        {
            if (!IsRegistered || _windowHandle == IntPtr.Zero) return;

            UnregisterHotKey(_windowHandle, HOTKEY_PROFILE_1);
            UnregisterHotKey(_windowHandle, HOTKEY_PROFILE_2);
            UnregisterHotKey(_windowHandle, HOTKEY_PROFILE_3);
            UnregisterHotKey(_windowHandle, HOTKEY_PROFILE_4);

            if (_source != null)
                _source.RemoveHook(HwndHook);

            IsRegistered = false;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                int profileIndex = id switch
                {
                    HOTKEY_PROFILE_1 => 1,
                    HOTKEY_PROFILE_2 => 2,
                    HOTKEY_PROFILE_3 => 3,
                    HOTKEY_PROFILE_4 => 4,
                    _ => 0
                };

                if (profileIndex > 0)
                {
                    ProfileHotkeyPressed?.Invoke(profileIndex);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            GC.SuppressFinalize(this);
        }
    }
}
