using System;
using System.Collections.Generic;
using System.Linq;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    /// <summary>
    /// Агрегаты по проектам/дням для UI. Данные — интервалы из SQLite;
    /// почасовая разбивка и таймлайн считаются в памяти по локальному времени.
    /// </summary>
    public class StatsService
    {
        public const string UnclassifiedColor = "#BBBBBB";
        public const string IdleColor = "#DDDDDD";

        /// <summary>Ширина таймлайна: 0.5 px на минуту = 720 px на сутки.</summary>
        public const double TimelinePixelsPerMinute = 0.5;

        private readonly IntervalRepository _intervals;
        private readonly ProjectRepository _projects;

        public StatsService(IntervalRepository intervals, ProjectRepository projects)
        {
            _intervals = intervals;
            _projects = projects;
        }

        public static (DateTime FromUtc, DateTime ToUtc) DayBoundsUtc(DateTime date)
            => (date.Date.ToUniversalTime(), date.Date.AddDays(1).ToUniversalTime());

        /// <summary>Проекты с суммой не-idle времени за день + строки «Неразобранное» и «Простой».</summary>
        public List<ProjectStatItem> GetDayProjectStats(DateTime date)
        {
            var (from, to) = DayBoundsUtc(date);
            var totals = _intervals.GetProjectTotals(from, to);
            var (unclassified, idle) = _intervals.GetUnclassifiedAndIdleTotals(from, to);

            var projects = _projects.GetProjects().ToDictionary(p => p.Id);
            var items = new List<ProjectStatItem>();

            foreach (var (projectId, seconds) in totals)
            {
                if (!projects.TryGetValue(projectId, out var project))
                    continue;
                items.Add(new ProjectStatItem
                {
                    ProjectId = projectId,
                    ProjectName = project.Name,
                    ColorHex = project.Color,
                    TotalSeconds = seconds
                });
            }

            if (unclassified > 0)
            {
                items.Add(new ProjectStatItem
                {
                    ProjectName = "Неразобранное",
                    ColorHex = UnclassifiedColor,
                    TotalSeconds = unclassified
                });
            }

            if (idle > 0)
            {
                items.Add(new ProjectStatItem
                {
                    ProjectName = "Простой (нет ввода)",
                    ColorHex = IdleColor,
                    TotalSeconds = idle
                });
            }

            var total = items.Sum(i => i.TotalSeconds);
            foreach (var item in items)
                item.Percent = total > 0 ? item.TotalSeconds / total * 100 : 0;

            return items.OrderByDescending(i => i.TotalSeconds).ToList();
        }

        /// <summary>24 столбца почасового графика со стеком сегментов по проектам.</summary>
        /// <param name="maxBarHeight">Высота столбца при полном часе (60 мин), в пикселях.</param>
        public List<HourStack> GetHourStacks(DateTime date, double maxBarHeight = 150)
        {
            var (from, to) = DayBoundsUtc(date);
            var intervals = _intervals.GetIntervals(from, to);
            var projects = _projects.GetProjects().ToDictionary(p => p.Id);

            // minutes[hour][key] — ключ: id проекта, -1 неразобранное, -2 простой.
            var minutes = new Dictionary<long, double>[24];
            for (int i = 0; i < 24; i++)
                minutes[i] = new Dictionary<long, double>();

            foreach (var interval in intervals)
            {
                var start = interval.StartLocal;
                var end = interval.EndLocal;
                if (end <= start)
                    continue;

                // Интервал может пересекать границу часа — разрезаем по часам.
                var key = interval.IsIdle ? -2 : interval.ProjectId ?? -1;
                var cursor = start;
                while (cursor < end)
                {
                    var nextHour = cursor.Date.AddHours(cursor.Hour + 1);
                    var sliceEnd = end < nextHour ? end : nextHour;
                    var sliceMinutes = (sliceEnd - cursor).TotalMinutes;
                    var bucket = minutes[cursor.Hour];
                    bucket[key] = bucket.GetValueOrDefault(key) + sliceMinutes;
                    cursor = sliceEnd;
                }
            }

            var stacks = new List<HourStack>();
            double ToHeight(double m) => Math.Min(m / 60.0, 1.0) * maxBarHeight;
            for (int h = 0; h < 24; h++)
            {
                var stack = new HourStack { Hour = h };
                // Порядок в стеке: проекты (по убыванию) сверху, затем неразобранное, внизу простой.
                foreach (var (key, value) in minutes[h].OrderByDescending(kv => kv.Key >= 0 ? kv.Value : double.MinValue))
                {
                    if (value <= 0 || key < 0)
                        continue;
                    var project = projects[key];
                    stack.Segments.Add(new HourStackSegment
                    {
                        Minutes = value,
                        Height = ToHeight(value),
                        ColorHex = project.Color,
                        ToolTipText = $"{project.Name}: {value:0} мин"
                    });
                }

                if (minutes[h].GetValueOrDefault(-1) > 0)
                {
                    stack.Segments.Add(new HourStackSegment
                    {
                        Minutes = minutes[h][-1],
                        Height = ToHeight(minutes[h][-1]),
                        ColorHex = UnclassifiedColor,
                        ToolTipText = $"Неразобранное: {minutes[h][-1]:0} мин"
                    });
                }

                if (minutes[h].GetValueOrDefault(-2) > 0)
                {
                    stack.Segments.Add(new HourStackSegment
                    {
                        Minutes = minutes[h][-2],
                        Height = ToHeight(minutes[h][-2]),
                        ColorHex = IdleColor,
                        ToolTipText = $"Простой: {minutes[h][-2]:0} мин"
                    });
                }

                stacks.Add(stack);
            }

            return stacks;
        }

        /// <summary>Сегменты таймлайна дня (00:00–24:00), координаты в пикселях.</summary>
        /// <param name="pixelsPerMinute">Масштаб; по умолчанию <see cref="TimelinePixelsPerMinute"/>.</param>
        public List<TimelineSegment> GetTimelineSegments(DateTime date, long? openIntervalId,
            double pixelsPerMinute = TimelinePixelsPerMinute)
        {
            var (from, to) = DayBoundsUtc(date);
            var intervals = _intervals.GetIntervals(from, to);
            var projects = _projects.GetProjects().ToDictionary(p => p.Id);
            var isToday = date.Date == DateTime.Now.Date;

            var dayStart = date.Date;
            var dayEnd = date.Date.AddDays(1);
            var segments = new List<TimelineSegment>();

            foreach (var interval in intervals)
            {
                var start = interval.StartLocal;
                var end = interval.EndLocal;

                // Открытый интервал рисуем до текущего момента.
                if (openIntervalId.HasValue && interval.Id == openIntervalId.Value && isToday)
                    end = DateTime.Now;

                if (start < dayStart) start = dayStart;
                if (end > dayEnd) end = dayEnd;
                if (end <= start)
                    continue;

                var project = interval.ProjectId.HasValue && projects.TryGetValue(interval.ProjectId.Value, out var p)
                    ? p
                    : null;

                var startMinutes = (start - dayStart).TotalMinutes;
                var durationMinutes = (end - start).TotalMinutes;

                var tooltip = $"{start:HH:mm}–{end:HH:mm} ({durationMinutes:0} мин)\n{interval.ProcessName}";
                if (!string.IsNullOrEmpty(interval.WindowTitle))
                    tooltip += $"\n{interval.WindowTitle}";
                if (!string.IsNullOrEmpty(interval.Url))
                    tooltip += $"\n{interval.Url}";
                tooltip += interval.IsIdle ? "\n(простой)" : project != null ? $"\nПроект: {project.Name}" : "\n(не классифицировано)";

                segments.Add(new TimelineSegment
                {
                    Left = startMinutes * pixelsPerMinute,
                    Width = Math.Max(durationMinutes * pixelsPerMinute, 1.5),
                    ColorHex = project?.Color ?? UnclassifiedColor,
                    Opacity = interval.IsIdle ? 0.35 : 1.0,
                    ProjectName = interval.IsIdle ? "Простой" : project?.Name ?? "Не классифицировано",
                    ToolTipText = tooltip
                });
            }

            return segments;
        }
    }
}
