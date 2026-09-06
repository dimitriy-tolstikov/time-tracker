using System;

namespace TimeTracker.Models
{
    /// <summary>Строка списка «Неразобранное» — неклассифицированный интервал.</summary>
    public class UnclassifiedIntervalItem
    {
        public long Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsIdle { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }

        public string TimeText => $"{StartLocal:HH:mm}–{EndLocal:HH:mm}";
        public string DurationText
        {
            get
            {
                var d = EndLocal - StartLocal;
                return d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes} мин" : $"{(int)d.TotalSeconds} сек";
            }
        }
        public string IdleMarker => IsIdle ? " (простой)" : string.Empty;

        public static UnclassifiedIntervalItem FromInterval(ActivityInterval interval) => new()
        {
            Id = interval.Id,
            ProcessName = interval.ProcessName,
            WindowTitle = interval.WindowTitle,
            Url = interval.Url,
            IsIdle = interval.IsIdle,
            StartLocal = interval.StartLocal,
            EndLocal = interval.EndLocal
        };
    }
}
