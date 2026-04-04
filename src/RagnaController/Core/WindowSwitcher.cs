using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Brings a target RO client window to the foreground and remembers the previous
    /// window so pressing the same button again returns focus to the main client.
    /// Uses AttachThreadInput for reliable SetForegroundWindow across processes.
    /// </summary>
    public static class WindowSwitcher
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        // Last window we switched AWAY from — pressing the button again returns here
        private static IntPtr _lastOrigin = IntPtr.Zero;

        /// <summary>
        /// Toggle focus between the current foreground window and the first visible
        /// window whose process name contains <paramref name="targetProcessName"/>.
        /// If the target is already in the foreground, return to the saved origin.
        /// </summary>
        public static void Toggle(string targetProcessName)
        {
            if (string.IsNullOrWhiteSpace(targetProcessName)) return;

            IntPtr current = GetForegroundWindow();
            IntPtr target  = FindWindow(targetProcessName);

            if (target == IntPtr.Zero) return; // target not running

            if (current == target)
            {
                // Already on the slave — go back to saved origin (or find main client)
                IntPtr returnTo = (_lastOrigin != IntPtr.Zero && IsWindow(_lastOrigin))
                    ? _lastOrigin
                    : IntPtr.Zero;

                if (returnTo != IntPtr.Zero)
                    BringToFront(returnTo);
            }
            else
            {
                // Save where we are, then switch to target
                _lastOrigin = current;
                BringToFront(target);
            }
        }

        /// <summary>
        /// Find the first visible window of processes whose name contains the given string (case-insensitive).
        /// Supports partial names: "ragexe" matches "ragexe.exe", "ragexe_patched.exe", etc.
        /// </summary>
        private static IntPtr FindWindow(string processNameFragment)
        {
            string fragment = processNameFragment.ToLowerInvariant()
                                                 .Replace(".exe", "");

            var candidates = new List<(IntPtr handle, DateTime startTime)>();

            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (!p.ProcessName.ToLowerInvariant().Contains(fragment)) continue;
                    var mainWindow = p.MainWindowHandle;
                    if (mainWindow == IntPtr.Zero || !IsWindowVisible(mainWindow)) continue;
                    candidates.Add((mainWindow, p.StartTime));
                }
                catch { /* access denied on some system processes */ }
            }

            if (candidates.Count == 0) return IntPtr.Zero;

            // If multiple RO clients are open: skip the one that is currently in focus,
            // otherwise return the oldest one (most likely the main / slave depending on usage).
            IntPtr current = GetForegroundWindow();
            var others = candidates.Where(c => c.handle != current).ToList();

            return others.Count > 0
                ? others.OrderBy(c => c.startTime).First().handle
                : candidates.OrderBy(c => c.startTime).First().handle;
        }

        /// <summary>
        /// Reliably brings hWnd to foreground using the AttachThreadInput trick.
        /// Plain SetForegroundWindow is blocked by Windows when the caller doesn't own focus.
        /// </summary>
        private static void BringToFront(IntPtr hWnd)
        {
            if (!IsWindow(hWnd)) return;

            // Restore if minimized
            ShowWindow(hWnd, SW_RESTORE);

            uint targetThread  = GetWindowThreadProcessId(hWnd, out _);
            uint currentThread = GetCurrentThreadId();

            bool attached = false;
            if (targetThread != currentThread)
            {
                attached = AttachThreadInput(currentThread, targetThread, true);
            }

            try
            {
                SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }
}
