using System;
using System.Diagnostics;
using System.Linq;

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

        /// <summary>
        /// Активирует главное окно указанного процесса: восстанавливает его, если свёрнуто,
        /// и выводит на передний план. Возвращает true, если окно найдено и активировано.
        /// </summary>
        public static bool SwitchToWindow(string processName)
        {
            try
            {
                if (string.IsNullOrEmpty(processName))
                    return false;

                var process = Process.GetProcessesByName(processName)
                    .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
                if (process == null)
                    return false;

                var hWnd = process.MainWindowHandle;
                if (NativeMethods.IsIconic(hWnd))
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                return NativeMethods.SetForegroundWindow(hWnd);
            }
            catch
            {
                return false;
            }
        }
    }
}
