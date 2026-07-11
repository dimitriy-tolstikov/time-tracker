using System;
using System.IO;
using System.Text.Json;

namespace TimeTracker.Services
{
    public class AppConfig
    {
        public string TargetProcessName { get; set; } = string.Empty;
        public TimeSpan TotalTrackedTime { get; set; }
        public DateTime LastTrackedDate { get; set; }
    }

    public static class ConfigService
    {
        private static readonly string LocalConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json");

        private static readonly string AppDataConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTracker",
            "config.json");

        static ConfigService()
        {
            EnsureDirectory(AppDataConfigPath);
        }

        public static AppConfig Load()
        {
            var config = LoadFrom(AppDataConfigPath);
            if (config == null)
            {
                config = LoadFrom(LocalConfigPath);
            }
            return config ?? new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            SaveTo(AppDataConfigPath, config);
            SaveTo(LocalConfigPath, config);
        }

        private static AppConfig? LoadFrom(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveTo(string path, AppConfig config)
        {
            try
            {
                EnsureDirectory(path);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private static void EnsureDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
