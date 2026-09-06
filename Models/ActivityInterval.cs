using System;

namespace TimeTracker.Models
{
    /// <summary>
    /// Интервал времени, проведённый в одном активном окне.
    /// Времена хранятся в UTC; интервал никогда не пересекает локальную полночь
    /// (watcher разрезает его при смене дня), что упрощает дневную статистику.
    /// </summary>
    public class ActivityInterval
    {
        public long Id { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string? Url { get; set; }
        /// <summary>true — во время интервала не было ввода с клавиатуры/мыши.</summary>
        public bool IsIdle { get; set; }
        /// <summary>NULL — интервал не классифицирован ни на один проект.</summary>
        public long? ProjectId { get; set; }

        public DateTime StartLocal => DateTime.SpecifyKind(StartUtc, DateTimeKind.Utc).ToLocalTime();
        public DateTime EndLocal => DateTime.SpecifyKind(EndUtc, DateTimeKind.Utc).ToLocalTime();
        public TimeSpan Duration => EndUtc - StartUtc;
    }
}
