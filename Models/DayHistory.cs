using System;

namespace TimeTracker.Models
{
    public class DayHistory
    {
        public DateTime Date { get; set; }

        public TimeSpan TotalWorkTime { get; set; }

        /// <summary>
        /// Отработанное время по часам [0..23].
        /// </summary>
        public TimeSpan[] WorkTimeByHour { get; set; } = new TimeSpan[24];

        /// <summary>
        /// Время простоя по часам [0..23].
        /// </summary>
        public TimeSpan[] IdleTimeByHour { get; set; } = new TimeSpan[24];
    }
}
