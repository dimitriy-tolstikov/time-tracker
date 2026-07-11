using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models
{
    public class DaySummary : INotifyPropertyChanged
    {
        private TimeSpan _totalWorkTime;

        public DateTime Date { get; set; }

        public TimeSpan TotalWorkTime
        {
            get => _totalWorkTime;
            set
            {
                if (_totalWorkTime != value)
                {
                    _totalWorkTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public string DisplayText
        {
            get
            {
                int hours = (int)TotalWorkTime.TotalHours;
                var t = TotalWorkTime;
                return $"{Date:dd.MM.yyyy} — {hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
