using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    /// <summary>
    /// Одноразовый импорт истории старой версии (history.json в LocalApplicationData)
    /// в SQLite. Старый файл только читается и не изменяется — параллельно работающая
    /// старая версия продолжает им владеть. Факт импорта фиксируется в settings,
    /// повторно миграция не запускается.
    /// Точность старых данных — час (WorkTimeByHour), поэтому каждый ненулевой час
    /// импортируется как один интервал от начала часа.
    /// </summary>
    public static class LegacyMigrationService
    {
        private const string DoneKey = "LegacyImportDone";
        private const string LegacyProjectName = "Старые данные";
        private const string LegacyProcessName = "legacy";

        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTracker",
            "history.json");

        public static void ImportIfNeeded()
        {
            try
            {
                var settings = new SettingsRepository();
                if (settings.GetString(DoneKey) == "1")
                    return;

                if (File.Exists(HistoryPath))
                    Import(settings);

                // Файла нет или импорт завершён — больше не проверяем.
                settings.SetString(DoneKey, "1");
            }
            catch
            {
                // Миграция не должна мешать запуску приложения.
            }
        }

        private static void Import(SettingsRepository settings)
        {
            List<LegacyDayHistory>? days;
            try
            {
                var json = File.ReadAllText(HistoryPath);
                days = JsonSerializer.Deserialize<List<LegacyDayHistory>>(json);
            }
            catch
            {
                return;
            }

            if (days == null || days.Count == 0)
                return;

            var projects = new ProjectRepository();
            var legacyProject = projects.GetProjects()
                .FirstOrDefault(p => string.Equals(p.Name, LegacyProjectName, StringComparison.OrdinalIgnoreCase));
            long projectId;
            if (legacyProject != null)
            {
                projectId = legacyProject.Id;
            }
            else
            {
                projectId = projects.AddProject(new Project
                {
                    Name = LegacyProjectName,
                    Color = "#9E9E9E"
                });
            }

            var intervals = new IntervalRepository();
            foreach (var day in days)
            {
                var date = day.Date.Date;
                for (int hour = 0; hour < 24 && hour < day.WorkTimeByHour.Length; hour++)
                {
                    var work = day.WorkTimeByHour[hour];
                    if (work <= TimeSpan.Zero)
                        continue;

                    // Старые даты — локальные; в БД храним UTC.
                    var startLocal = DateTime.SpecifyKind(date.AddHours(hour), DateTimeKind.Local);
                    var startUtc = startLocal.ToUniversalTime();
                    intervals.InsertInterval(new ActivityInterval
                    {
                        StartUtc = startUtc,
                        EndUtc = startUtc + work,
                        ProcessName = LegacyProcessName,
                        WindowTitle = "Импортировано из history.json",
                        IsIdle = false,
                        ProjectId = projectId
                    });
                }
            }
        }

        /// <summary>Формат DayHistory старой версии (JSON, System.Text.Json).</summary>
        private class LegacyDayHistory
        {
            public DateTime Date { get; set; }
            public TimeSpan TotalWorkTime { get; set; }
            public TimeSpan[] WorkTimeByHour { get; set; } = new TimeSpan[24];
            public TimeSpan[] IdleTimeByHour { get; set; } = new TimeSpan[24];
        }
    }
}
