using System;
using System.Runtime.InteropServices;
using System.IO;

namespace LoneEftDmaRadar.UI.Misc
{
    public static class DebugLogger
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();

        private static bool _isConsoleAllocated = false;

        public static void Toggle()
        {
            if (_isConsoleAllocated)
            {
                FreeConsole();
                _isConsoleAllocated = false;
            }
            else
            {
                AllocConsole();
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.WriteLine("Debug Console Initialized");
                _isConsoleAllocated = true;
            }
        }

        public static void Close()
        {
            if (_isConsoleAllocated)
            {
                FreeConsole();
                _isConsoleAllocated = false;
            }
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (!_isConsoleAllocated) return;

            var timestamp = $"[{DateTime.Now:HH:mm:ss.fff}] ";
            var levelStr = level switch
            {
                LogLevel.Debug => "[DEBUG] ",
                LogLevel.Info => "[INFO] ",
                LogLevel.Warning => "[WARN] ",
                LogLevel.Error => "[ERROR] ",
                LogLevel.Critical => "[CRITICAL] ",
                _ => ""
            };

            Console.WriteLine($"{timestamp}{levelStr}{message}");
        }

        public static void LogDebug(string message) => Log(message, LogLevel.Debug);
        public static void LogInfo(string message) => Log(message, LogLevel.Info);
        public static void LogWarning(string message) => Log(message, LogLevel.Warning);
        public static void LogError(string message) => Log(message, LogLevel.Error);
        public static void LogCritical(string message) => Log(message, LogLevel.Critical);

        public static void LogException(Exception ex, string context = "")
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Exception: {ex.Message}\n{ex.StackTrace}"
                : $"Exception in {context}: {ex.Message}\n{ex.StackTrace}";

            Log(message, LogLevel.Error);
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}
