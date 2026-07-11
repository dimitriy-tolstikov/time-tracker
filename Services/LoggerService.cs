using System;
using System.IO;
using System.Text.Json;

namespace TimeTracker.Services
{
    public static class LoggerService
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTracker",
            "Logs");

        private static readonly object Lock = new();

        static LoggerService()
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
        }

        public static void LogTick(TickLogEntry entry)
        {
            var fileName = $"ticks_{DateTime.Now:yyyy-MM-dd}.jsonl";
            var path = Path.Combine(LogDirectory, fileName);
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;

            lock (Lock)
            {
                File.AppendAllText(path, line);
            }
        }

        public static void LogInfo(string message)
        {
            var fileName = $"info_{DateTime.Now:yyyy-MM-dd}.jsonl";
            var path = Path.Combine(LogDirectory, fileName);
            var line = JsonSerializer.Serialize(new { Timestamp = DateTime.Now, Message = message }) + Environment.NewLine;

            lock (Lock)
            {
                File.AppendAllText(path, line);
            }
        }
    }

    public class TickLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string? ActiveProcess { get; set; }
        public string? TargetProcess { get; set; }
        public bool IsTargetActive { get; set; }
        public bool HadInput { get; set; }
        public DateTime? LastActionTime { get; set; }
        public double SinceLastSeconds { get; set; }
        public bool IsTrackingActive { get; set; }
        public double ElapsedSeconds { get; set; }
        public double TrackedTimeSeconds { get; set; }
        public double AdvanceTimeSeconds { get; set; }
        public bool AdvanceCorrected { get; set; }
        public double WorkHourSeconds { get; set; }
        public double IdleHourSeconds { get; set; }
        public int CurrentHour { get; set; }
        public int ActionCount { get; set; }
    }
}
