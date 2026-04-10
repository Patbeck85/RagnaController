using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Tracks the RO client window position and size across monitors with correct DPI scaling.
    /// Resolves the "cursor drift" problem on multi-monitor / 4K / 150% DPI setups.
    ///
    /// How it works:
    ///  1. FindWindowByProcess()  — scans running processes for the configured exe name
    ///  2. GetClientRect()        — gives the inner drawable area (excludes title-bar / borders)
    ///  3. ClientToScreen()       — converts client (0,0) to real screen coordinates
    ///  4. MonitorFromWindow()    — finds which monitor the window lives on
    ///  5. GetDpiForMonitor()     — reads that monitor's actual DPI (e.g. 192 on 4K @ 200%)
    ///  6. Scale factor           — physicalDpi / 96 corrects all pixel distances
    ///
    /// The resulting CenterX / CenterY values are in physical (raw) screen pixels, exactly
    /// what SendInput expects — no more drift at any DPI or window position.
    /// </summary>
    public class WindowTracker
    {
        // ── Win32 ──────────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern bool   GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool   GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool   ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("shcore.dll")] private static extern int    GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")] private static extern bool   IsWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint   GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int  MDT_EFFECTIVE_DPI        = 0;

        // ── State ──────────────────────────────────────────────────────────────────
        private IntPtr _hwnd = IntPtr.Zero;
        private string _processName = "ragexe";

        /// <summary>Physical screen X of the RO client centre pixel.</summary>
        public int CenterX { get; private set; }
        /// <summary>Physical screen Y of the RO client centre pixel.</summary>
        public int CenterY { get; private set; }
        /// <summary>Width of the RO client area in physical pixels.</summary>
        public int ClientW { get; private set; }
        /// <summary>Height of the RO client area in physical pixels.</summary>
        public int ClientH { get; private set; }
        /// <summary>DPI scale factor of the monitor the window is on (1.0 = 96 dpi / 100%).</summary>
        public float DpiScale { get; private set; } = 1.0f;
        /// <summary>True if a valid window handle was found on the last refresh.</summary>
        public bool IsTracking { get; private set; }

        // ── Public API ─────────────────────────────────────────────────────────────
        public void SetProcessName(string name) => _processName = name;

        /// <summary>
        /// Call every ~500 ms (or after profile/settings change).
        /// Finds the window, computes the DPI-corrected client centre, and caches the result.
        /// Hot-path callers (MovementEngine.Update, 125 Hz) just read CenterX/CenterY.
        /// </summary>
        public void Refresh()
        {
            IsTracking = false;

            try
            {
                // Priority 1: foreground window — if it's already our cached HWND and still valid,
                // skip the expensive process scan entirely (fast path, no GC allocation).
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero)
                {
                    if (fgHwnd == _hwnd && IsWindow(_hwnd))
                    {
                        // Same window still in front — just refresh geometry
                        UpdateGeometry();
                        return;
                    }

                    // Different foreground window — check if it's an RO client
                    GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                    try
                    {
                        using var fgProc = Process.GetProcessById((int)fgPid);
                        if (fgProc.ProcessName.Contains(_processName, StringComparison.OrdinalIgnoreCase))
                        {
                            _hwnd = fgHwnd;
                            UpdateGeometry();
                            return;
                        }
                    }
                    catch { }
                }

                // Priority 2: cached HWND still valid — RO is backgrounded, reuse it
                if (_hwnd != IntPtr.Zero && IsWindow(_hwnd))
                {
                    UpdateGeometry();
                    return;
                }

                // Priority 3: full process scan (expensive — only when handle is lost)
                _hwnd = IntPtr.Zero;
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.ProcessName.Contains(_processName, StringComparison.OrdinalIgnoreCase)
                            && p.MainWindowHandle != IntPtr.Zero)
                        {
                            _hwnd = p.MainWindowHandle;
                            break;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[WindowTracker] Process check failed: {ex.Message}"); }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            if (_hwnd != IntPtr.Zero) UpdateGeometry();
        }

        // Note: HybridEngine controls refresh cadence via its own _focusCheckCounter.
        // ForceRefreshOnNextTick() removed — _focusCheckCounter in HybridEngine is reset directly.
        // Tick() removed — it was dead code.



        // ── Private helpers ────────────────────────────────────────────────────────
        private void UpdateGeometry()
        {
            if (!GetClientRect(_hwnd, out RECT client)) return;

            // Convert client origin (0,0) to screen coordinates
            var origin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(_hwnd, ref origin)) return;

            // Get the DPI of the monitor this window is on
            IntPtr monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
            float scale = 1.0f;
            if (monitor != IntPtr.Zero)
            {
                if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
                    scale = dpiX / 96.0f;
            }

            int w = client.Right  - client.Left;
            int h = client.Bottom - client.Top;

            // With PerMonitorV2 DPI awareness (app.manifest), all Win32 API calls
            // (GetClientRect, ClientToScreen, GetCursorPos, SendInput) already operate in
            // physical pixels. DpiScale is retained for informational display only.
            ClientW  = w;
            ClientH  = h;
            CenterX  = origin.X + ClientW  / 2;
            CenterY  = origin.Y + ClientH  / 2;
            DpiScale = scale;   // informational — shown in status bar
            IsTracking = (w > 0 && h > 0);
        }

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);
    }
}
