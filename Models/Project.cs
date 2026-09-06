using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models
{
    /// <summary>Проект, на который классифицируются интервалы активности.</summary>
    public class Project : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _color = "#888888";

        public long Id { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
