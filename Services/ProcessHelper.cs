using System;
using System.Diagnostics;

namespace TimeTracker.Services
{
    public static class ProcessHelper
    {
        public static string GetActiveProcessName()
        {
            try
            {
                var hWnd = NativeMethods.GetForegroundWindow();
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetActiveWindowTitle()
        {
            var hWnd = NativeMethods.GetForegroundWindow();
            return NativeMethods.GetWindowTitle(hWnd);
        }
    }
}
