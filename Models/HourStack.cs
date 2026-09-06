using System.Collections.ObjectModel;

namespace TimeTracker.Models
{
    /// <summary>Один столбец почасового графика «По проектам»: стек сегментов.</summary>
    public class HourStack
    {
        public int Hour { get; set; }
        public ObservableCollection<HourStackSegment> Segments { get; } = new();
        public string HourLabel => $"{Hour:00}:00";
    }

    /// <summary>Сегмент столбца: минуты одного проекта (или простоя/неразобранного) в этом часе.</summary>
    public class HourStackSegment
    {
        public double Minutes { get; set; }
        /// <summary>Высота в пикселях: 60 мин = вся высота области; считает StatsService.</summary>
        public double Height { get; set; }
        public string ColorHex { get; set; } = "#888888";
        public string ToolTipText { get; set; } = string.Empty;
    }
}
