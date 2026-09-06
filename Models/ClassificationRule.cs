using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models
{
    /// <summary>
    /// Правило классификации интервала на проект.
    /// RuleType: "process" — точное совпадение имени процесса, "title"/"url" — подстрока или regex.
    /// Конфликты правил разрешаются по Priority (больше = важнее).
    /// </summary>
    public class ClassificationRule : INotifyPropertyChanged
    {
        private string _ruleType = "process";
        private string _pattern = string.Empty;
        private bool _isRegex;
        private int _priority;

        public long Id { get; set; }
        public long ProjectId { get; set; }

        public string RuleType
        {
            get => _ruleType;
            set { _ruleType = value; OnPropertyChanged(); OnPropertyChanged(nameof(RuleTypeDisplay)); }
        }

        public string Pattern
        {
            get => _pattern;
            set { _pattern = value; OnPropertyChanged(); }
        }

        public bool IsRegex
        {
            get => _isRegex;
            set { _isRegex = value; OnPropertyChanged(); }
        }

        public int Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); }
        }

        public string RuleTypeDisplay => RuleType switch
        {
            "process" => "Процесс",
            "title" => "Заголовок",
            "url" => "URL",
            _ => RuleType
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
