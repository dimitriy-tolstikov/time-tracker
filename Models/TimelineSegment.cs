namespace TimeTracker.Models
{
    /// <summary>Сегмент таймлайна «Хронология»: координаты уже в пикселях для Canvas.</summary>
    public class TimelineSegment
    {
        public double Left { get; set; }
        public double Width { get; set; }
        public string ColorHex { get; set; } = "#888888";
        /// <summary>Idle-сегменты рисуются бледными.</summary>
        public double Opacity { get; set; } = 1.0;
        /// <summary>Название проекта (или «Простой»/«Не классифицировано») — показывается в подсказке.</summary>
        public string ProjectName { get; set; } = string.Empty;
        public string ToolTipText { get; set; } = string.Empty;
    }
}
