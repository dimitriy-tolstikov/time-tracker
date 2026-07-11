using System;

namespace TimeTracker.Models
{
    public class ActivityAction
    {
        public DateTime Time { get; set; }

        public string TimeFormatted => Time.ToString("HH:mm:ss");

        public TimeSpan? IntervalSincePrevious { get; set; }

        public string IntervalText => IntervalSincePrevious.HasValue
            ? IntervalSincePrevious.Value.ToString(@"mm\:ss")
            : "-";

        public bool IsCounted { get; set; }
    }
}
