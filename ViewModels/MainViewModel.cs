using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly TrackerService _tracker;
        private readonly AppConfig _config;

        public ObservableCollection<ActivityAction> Actions { get; } = new();
        public ObservableCollection<HourlyStat> SelectedDayHourlyStats { get; } = new();
        public ObservableCollection<DaySummary> AvailableDays { get; } = new();

        private DaySummary? _selectedDay;
        public DaySummary? SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (_selectedDay != value)
                {
                    _selectedDay = value;
                    OnPropertyChanged();
                    LoadSelectedDayStats();
                }
            }
        }

        private string _setWindowButtonText = "Указать окно (5 сек)";
        public string SetWindowButtonText
        {
            get => _setWindowButtonText;
            set { _setWindowButtonText = value; OnPropertyChanged(); }
        }

        public string TrackedTimeText
        {
            get
            {
                var t = _tracker.TrackedTime;
                int hours = (int)t.TotalHours;
                return $"{hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
        }

        public string CurrentTimeText => DateTime.Now.ToString("HH:mm:ss");
        public string LastSavedText => $"Сохранено: {_tracker.LastSavedTime:HH:mm:ss}";

        public string SelectedDayHeaderText
        {
            get
            {
                if (_selectedDay == null || _selectedDay.Date == _tracker.CurrentDate)
                    return "Распределение времени по часам (сегодня)";
                return $"Распределение времени по часам ({_selectedDay.Date:dd.MM.yyyy})";
            }
        }

        public string TargetProcessName
        {
            get => _config.TargetProcessName;
            set
            {
                if (_config.TargetProcessName != value)
                {
                    _config.TargetProcessName = value;
                    _tracker.TargetProcessName = value;
                    ConfigService.Save(_config);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public bool IsTrackingActive => _tracker.IsTrackingActive;

        public string StatusText
        {
            get
            {
                if (string.IsNullOrEmpty(TargetProcessName))
                    return "Целевое окно не выбрано. Нажмите 'Указать окно'.";
                return IsTrackingActive
                    ? $"Отслеживание активно: {TargetProcessName}"
                    : $"Ожидание активности в: {TargetProcessName}";
            }
        }

        public ICommand SetWindowCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ShowTodayCommand { get; }

        public event EventHandler? ShowInactivityWarning;

        public MainViewModel()
        {
            _config = ConfigService.Load();
            _tracker = new TrackerService(_config);
            _tracker.TargetProcessName = _config.TargetProcessName;
            _tracker.Updated += OnTrackerUpdated;
            _tracker.InactivityWarning += (s, e) => ShowInactivityWarning?.Invoke(this, e);
            _tracker.DayRolledOver += (s, e) =>
            {
                RefreshAvailableDays();
                SelectToday();
            };

            SetWindowCommand = new RelayCommand(async () => await SetWindowAsync());
            ResetCommand = new RelayCommand(() =>
            {
                _tracker.Reset();
                UpdateTodaySummary();
            });
            ShowTodayCommand = new RelayCommand(SelectToday);

            var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) => OnPropertyChanged(nameof(CurrentTimeText));
            clockTimer.Start();

            RefreshAvailableDays();
            SelectToday();
            _tracker.Start();
        }

        private bool _isSettingWindow;

        private async Task SetWindowAsync()
        {
            if (_isSettingWindow) return;
            _isSettingWindow = true;

            for (int i = 5; i > 0; i--)
            {
                SetWindowButtonText = $"Выберите окно... {i}";
                await Task.Delay(1000);
            }

            var name = ProcessHelper.GetActiveProcessName();
            if (!string.IsNullOrEmpty(name))
            {
                TargetProcessName = name;
            }

            SetWindowButtonText = "Указать окно (5 сек)";
            _isSettingWindow = false;
        }

        private void OnTrackerUpdated(object? sender, EventArgs e)
        {
            var actions = _tracker.Actions;

            int i = 0;
            while (i < Actions.Count && i < actions.Count)
            {
                Actions[i].IntervalSincePrevious = actions[i].IntervalSincePrevious;
                Actions[i].IsCounted = actions[i].IsCounted;
                i++;
            }

            while (Actions.Count > actions.Count)
                Actions.RemoveAt(Actions.Count - 1);

            while (i < actions.Count)
            {
                Actions.Add(new ActivityAction
                {
                    Time = actions[i].Time,
                    IntervalSincePrevious = actions[i].IntervalSincePrevious,
                    IsCounted = actions[i].IsCounted
                });
                i++;
            }

            RefreshSelectedDayStats();
            UpdateTodaySummary();

            OnPropertyChanged(nameof(TrackedTimeText));
            OnPropertyChanged(nameof(IsTrackingActive));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(LastSavedText));
        }

        private void UpdateTodaySummary()
        {
            var today = AvailableDays.FirstOrDefault(d => d.Date == _tracker.CurrentDate);
            if (today != null)
            {
                today.TotalWorkTime = _tracker.TrackedTime;
            }
            else
            {
                AvailableDays.Insert(0, new DaySummary { Date = _tracker.CurrentDate, TotalWorkTime = _tracker.TrackedTime });
            }
        }

        private void RefreshAvailableDays()
        {
            var history = HistoryService.LoadAll();
            var today = _tracker.CurrentDate;
            var todayHistory = _tracker.GetCurrentDayHistory();

            var days = new List<DaySummary>();

            foreach (var day in history.Where(d => d.Date != today).OrderByDescending(d => d.Date))
            {
                days.Add(new DaySummary { Date = day.Date, TotalWorkTime = day.TotalWorkTime });
            }

            // Сегодняшний день всегда первым.
            days.Insert(0, new DaySummary { Date = today, TotalWorkTime = todayHistory.TotalWorkTime });

            AvailableDays.Clear();
            foreach (var day in days)
            {
                AvailableDays.Add(day);
            }

            OnPropertyChanged(nameof(AvailableDays));
        }

        private void SelectToday()
        {
            SelectedDay = AvailableDays.FirstOrDefault(d => d.Date == _tracker.CurrentDate);
        }

        private void LoadSelectedDayStats()
        {
            RefreshSelectedDayStats();
            OnPropertyChanged(nameof(SelectedDayHeaderText));
        }

        private void RefreshSelectedDayStats()
        {
            DayHistory day;

            if (_selectedDay == null || _selectedDay.Date == _tracker.CurrentDate)
            {
                day = _tracker.GetCurrentDayHistory();
            }
            else
            {
                var history = HistoryService.LoadAll();
                day = history.FirstOrDefault(d => d.Date == _selectedDay.Date) ?? new DayHistory { Date = _selectedDay.Date };
            }

            SelectedDayHourlyStats.Clear();
            for (int h = 0; h < 24; h++)
            {
                SelectedDayHourlyStats.Add(new HourlyStat
                {
                    Hour = h,
                    WorkMinutes = day.WorkTimeByHour[h].TotalMinutes,
                    IdleMinutes = day.IdleTimeByHour[h].TotalMinutes
                });
            }

            OnPropertyChanged(nameof(SelectedDayHourlyStats));
            OnPropertyChanged(nameof(SelectedDayHeaderText));
        }

        public void Dispose()
        {
            _tracker.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
