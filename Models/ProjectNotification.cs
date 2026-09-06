using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models
{
    /// <summary>
    /// Одно уведомление проекта. Тип: "inactivity" — нет активности в проекте,
    /// "daily_limit" — превышен дневной лимит. Порог хранится в секундах.
    /// Уровень серьёзности (severity): "normal" — белый, "warning" — жёлтый,
    /// "critical" — красный; влияет только на оформление окна уведомления.
    /// У проекта может быть любое количество уведомлений.
    /// </summary>
    public class ProjectNotification : INotifyPropertyChanged
    {
        private string _type = "inactivity";
        private int _thresholdSeconds = 300;
        private string _severity = "normal";

        public long Id { get; set; }
        public long ProjectId { get; set; }

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeDisplay));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public int ThresholdSeconds
        {
            get => _thresholdSeconds;
            set
            {
                _thresholdSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThresholdMinutesPart));
                OnPropertyChanged(nameof(ThresholdSecondsPart));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string Severity
        {
            get => _severity;
            set
            {
                _severity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SeverityDisplay));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>Минутная часть порога (только для UI, в БД не хранится).</summary>
        public int ThresholdMinutesPart
        {
            get => ThresholdSeconds / 60;
            set => ThresholdSeconds = Math.Max(value, 0) * 60 + ThresholdSecondsPart;
        }

        /// <summary>Секундная часть порога (только для UI, в БД не хранится).</summary>
        public int ThresholdSecondsPart
        {
            get => ThresholdSeconds % 60;
            set => ThresholdSeconds = ThresholdMinutesPart * 60 + Math.Clamp(value, 0, 59);
        }

        public string TypeDisplay => Type switch
        {
            "inactivity" => "Неактивность",
            "daily_limit" => "Дневной лимит",
            _ => Type
        };

        public string SeverityDisplay => Severity switch
        {
            "warning" => "Жёлтый",
            "critical" => "Красный",
            _ => "Белый"
        };

        public string DisplayText => $"{TypeDisplay}: {FormatDuration(ThresholdSeconds)} · {SeverityDisplay}";

        public static string FormatDuration(int totalSeconds)
        {
            int minutes = totalSeconds / 60, seconds = totalSeconds % 60;
            if (minutes == 0)
                return $"{seconds} сек";
            return seconds > 0 ? $"{minutes} мин {seconds} сек" : $"{minutes} мин";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
