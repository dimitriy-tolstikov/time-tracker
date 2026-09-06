using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models
{
    /// <summary>
    /// Строка списка «проект → время за день». ProjectId = null у служебных строк
    /// («Неразобранное», «Простой»).
    /// </summary>
    public class ProjectStatItem : INotifyPropertyChanged
    {
        public long? ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#888888";
        public double TotalSeconds { get; set; }
        public double Percent { get; set; }

        public string DisplayText
        {
            get
            {
                var t = TimeSpan.FromSeconds(TotalSeconds);
                return $"{ProjectName} — {(int)t.TotalHours:D2}:{t.Minutes:D2} ({Percent:0.#}%)";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
