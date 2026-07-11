using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    public static class HistoryService
    {
        private static readonly string LocalHistoryPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "history.json");

        private static readonly string AppDataHistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTracker",
            "history.json");

        static HistoryService()
        {
            EnsureDirectory(AppDataHistoryPath);
        }

        public static List<DayHistory> LoadAll()
        {
            // Приоритет у AppData, но если там пусто — пытаемся загрузить рядом с программой.
            var history = LoadFrom(AppDataHistoryPath);
            if (history.Count == 0)
            {
                history = LoadFrom(LocalHistoryPath);
            }
            return history;
        }

        public static void SaveAll(List<DayHistory> history)
        {
            SaveTo(AppDataHistoryPath, history);
            SaveTo(LocalHistoryPath, history);
        }

        public static void SaveDay(DayHistory day)
        {
            var history = LoadAll();

            var existing = history.FirstOrDefault(d => d.Date.Date == day.Date.Date);
            if (existing != null)
            {
                history.Remove(existing);
            }

            history.Add(day);
            SaveAll(history);
        }

        private static List<DayHistory> LoadFrom(string path)
        {
            if (!File.Exists(path))
                return new List<DayHistory>();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<DayHistory>>(json) ?? new List<DayHistory>();
            }
            catch
            {
                return new List<DayHistory>();
            }
        }

        private static void SaveTo(string path, List<DayHistory> history)
        {
            try
            {
                EnsureDirectory(path);
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
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
