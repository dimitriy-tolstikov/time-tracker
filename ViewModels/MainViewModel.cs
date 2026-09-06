using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly TimeSpan RefreshThrottle = TimeSpan.FromSeconds(5);

        private readonly WindowWatcherService _watcher;
        private readonly SettingsRepository _settings;
        private readonly IntervalRepository _intervals;
        private readonly ProjectRepository _projects;
        private readonly ClassificationService _classifier;
        private readonly StatsService _stats;
        private readonly NotificationService _notifications;

        private DateTime _lastRefresh = DateTime.MinValue;

        public ObservableCollection<DaySummary> AvailableDays { get; } = new();
        public ObservableCollection<ProjectStatItem> ProjectStats { get; } = new();
        public ObservableCollection<HourStack> HourStacks { get; } = new();
        public ObservableCollection<TimelineSegment> TimelineSegments { get; } = new();
        public ObservableCollection<UnclassifiedIntervalItem> Unclassified { get; } = new();
        public ObservableCollection<Project> Projects { get; } = new();

        public RuleTypeItem[] RuleTypes => RuleTypeItem.All;

        /// <summary>Репозиторий проектов для вкладки управления проектами (ProjectsView).</summary>
        public ProjectRepository ProjectsRepository => _projects;

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
                    OnPropertyChanged(nameof(SelectedDayHeaderText));
                    RefreshStats();
                }
            }
        }

        private bool _isTimelineMode;
        public bool IsTimelineMode
        {
            get => _isTimelineMode;
            set
            {
                _isTimelineMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProjectsMode));
            }
        }

        public bool IsProjectsMode
        {
            get => !_isTimelineMode;
            set => IsTimelineMode = !value;
        }

        // --- Масштаб таймлайна ---

        public const double MinTimelineZoom = 1.0;
        public const double MaxTimelineZoom = 16.0;

        private double _timelineZoom = 1.0;
        /// <summary>Масштаб таймлайна: 1x — сутки во всю ширину окна, дальше ×2 за шаг.</summary>
        public double TimelineZoom
        {
            get => _timelineZoom;
            set
            {
                var clamped = Math.Clamp(value, MinTimelineZoom, MaxTimelineZoom);
                if (_timelineZoom == clamped)
                    return;
                _timelineZoom = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimelineCanvasWidth));
                RefreshTimeline();
            }
        }

        private double _timelineBaseWidth = 720;
        /// <summary>Базовая ширина таймлайна (при зуме 1x) — равна видимой ширине, ставит MainWindow.</summary>
        public double TimelineBaseWidth
        {
            get => _timelineBaseWidth;
            set
            {
                if (Math.Abs(_timelineBaseWidth - value) < 0.5)
                    return;
                _timelineBaseWidth = value;
                OnPropertyChanged(nameof(TimelineCanvasWidth));
                RefreshTimeline();
            }
        }

        public double TimelineCanvasWidth => _timelineBaseWidth * _timelineZoom;

        private double TimelinePixelsPerMinute => TimelineCanvasWidth / 1440.0;

        private string _hoveredTimelineText = string.Empty;
        /// <summary>Подпись под таймлайном: проект сегмента под курсором.</summary>
        public string HoveredTimelineText
        {
            get => _hoveredTimelineText;
            set { _hoveredTimelineText = value; OnPropertyChanged(); }
        }

        private double _projectsChartMaxHeight = 150;
        /// <summary>Высота области почасового графика «По проектам» (фактическая, ставит MainWindow).</summary>
        public double ProjectsChartMaxHeight
        {
            get => _projectsChartMaxHeight;
            set
            {
                if (Math.Abs(_projectsChartMaxHeight - value) < 0.5)
                    return;
                _projectsChartMaxHeight = value;
                OnPropertyChanged();
                RefreshHourStacks();
            }
        }

        /// <summary>Пересчитать только почасовые столбцы (при изменении высоты области).</summary>
        private void RefreshHourStacks()
        {
            if (_selectedDay == null)
                return;

            HourStacks.Clear();
            foreach (var stack in _stats.GetHourStacks(_selectedDay.Date, ProjectsChartMaxHeight))
                HourStacks.Add(stack);
        }

        // --- Панель «Неразобранное» ---

        private UnclassifiedIntervalItem? _selectedUnclassified;
        public UnclassifiedIntervalItem? SelectedUnclassified
        {
            get => _selectedUnclassified;
            set
            {
                _selectedUnclassified = value;
                OnPropertyChanged();
                PrefillRulePattern();
            }
        }

        private Project? _assignProject;
        public Project? AssignProject
        {
            get => _assignProject;
            set { _assignProject = value; OnPropertyChanged(); }
        }

        private bool _createRule;
        public bool CreateRule
        {
            get => _createRule;
            set { _createRule = value; OnPropertyChanged(); }
        }

        private RuleTypeItem _selectedRuleType = RuleTypeItem.All[0];
        public RuleTypeItem SelectedRuleType
        {
            get => _selectedRuleType;
            set
            {
                _selectedRuleType = value;
                OnPropertyChanged();
                PrefillRulePattern();
            }
        }

        private string _rulePattern = string.Empty;
        public string RulePattern
        {
            get => _rulePattern;
            set { _rulePattern = value; OnPropertyChanged(); }
        }

        private bool _ruleIsRegex;
        public bool RuleIsRegex
        {
            get => _ruleIsRegex;
            set { _ruleIsRegex = value; OnPropertyChanged(); }
        }

        // --- Настройки ---

        public int InactivityTimeoutMinutes
        {
            get => GetInactivityTimeoutSeconds() / 60;
            set => SetInactivityTimeout(Math.Max(value, 0), InactivityTimeoutSeconds);
        }

        public int InactivityTimeoutSeconds
        {
            get => GetInactivityTimeoutSeconds() % 60;
            set => SetInactivityTimeout(InactivityTimeoutMinutes, Math.Clamp(value, 0, 59));
        }

        private int GetInactivityTimeoutSeconds()
            => _settings.GetInt(SettingsRepository.InactivityTimeoutSecondsKey,
                SettingsRepository.DefaultInactivityTimeoutSeconds);

        private void SetInactivityTimeout(int minutes, int seconds)
        {
            var total = Math.Max(minutes * 60 + seconds, 1);
            if (GetInactivityTimeoutSeconds() != total)
            {
                _settings.SetInt(SettingsRepository.InactivityTimeoutSecondsKey, total);
                _watcher.InactivityTimeout = TimeSpan.FromSeconds(total);
            }

            OnPropertyChanged(nameof(InactivityTimeoutMinutes));
            OnPropertyChanged(nameof(InactivityTimeoutSeconds));
        }

        // --- Тексты ---

        public string CurrentTimeText => DateTime.Now.ToString("HH:mm:ss");

        public string StatusText => string.IsNullOrEmpty(_watcher.CurrentWindowText)
            ? "Ожидание активного окна..."
            : $"Активное окно: {_watcher.CurrentWindowText}";

        public string SelectedDayHeaderText
        {
            get
            {
                if (_selectedDay == null || _selectedDay.Date == _watcher.CurrentDate)
                    return "График дня (сегодня)";
                return $"График дня ({_selectedDay.Date:dd.MM.yyyy})";
            }
        }

        // --- Команды и события ---

        public ICommand ShowTodayCommand { get; }
        public ICommand AssignCommand { get; }
        public ICommand OpenProjectsCommand { get; }
        public ICommand TimelineZoomInCommand { get; }
        public ICommand TimelineZoomOutCommand { get; }

        public event EventHandler<ProjectNotificationEventArgs>? ShowProjectNotification;
        /// <summary>Запрос переключиться на вкладку управления проектами (обрабатывает MainWindow).</summary>
        public event EventHandler? OpenProjectsRequested;

        public MainViewModel()
        {
            _settings = new SettingsRepository();
            _intervals = new IntervalRepository();
            _projects = new ProjectRepository();
            _classifier = new ClassificationService(_projects, _intervals);
            _stats = new StatsService(_intervals, _projects);
            _notifications = new NotificationService(_projects, _intervals);

            _watcher = new WindowWatcherService(_intervals, _classifier)
            {
                InactivityTimeout = TimeSpan.FromSeconds(GetInactivityTimeoutSeconds())
            };
            _watcher.Updated += (s, e) => ThrottledRefresh();
            _watcher.DayRolledOver += (s, e) =>
            {
                RefreshAvailableDays();
                SelectToday();
            };

            _notifications.NotificationRequested += (s, e) => ShowProjectNotification?.Invoke(this, e);

            ShowTodayCommand = new RelayCommand(SelectToday);
            AssignCommand = new RelayCommand(AssignSelectedInterval, () => SelectedUnclassified != null && AssignProject != null);
            OpenProjectsCommand = new RelayCommand(() => OpenProjectsRequested?.Invoke(this, EventArgs.Empty));
            TimelineZoomInCommand = new RelayCommand(() => TimelineZoom *= 2, () => TimelineZoom < MaxTimelineZoom);
            TimelineZoomOutCommand = new RelayCommand(() => TimelineZoom /= 2, () => TimelineZoom > MinTimelineZoom);

            var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) => OnPropertyChanged(nameof(CurrentTimeText));
            clockTimer.Start();

            // Периодическое обновление статистики (открытый интервал растёт).
            var refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            refreshTimer.Tick += (s, e) => RefreshAll();
            refreshTimer.Start();

            RefreshProjects();
            RefreshAvailableDays();
            SelectToday();
            _watcher.Start();
            _notifications.Start();
        }

        /// <summary>Вызывается после закрытия окна проектов: перечитать правила и переклассифицировать.</summary>
        public void OnProjectsChanged()
        {
            _classifier.Reload();
            _notifications.WakeUp();
            var (from, to) = StatsService.DayBoundsUtc(_watcher.CurrentDate);
            _classifier.ReclassifyUnclassified(from, to);
            RefreshProjects();
            RefreshAll();
        }

        public void SnoozeNotification(long projectId, TimeSpan duration)
            => _notifications.Snooze(projectId, duration);

        private void AssignSelectedInterval()
        {
            if (SelectedUnclassified == null || AssignProject == null)
                return;

            _intervals.SetIntervalProject(SelectedUnclassified.Id, AssignProject.Id);

            if (CreateRule && !string.IsNullOrWhiteSpace(RulePattern))
            {
                _projects.AddRule(new ClassificationRule
                {
                    ProjectId = AssignProject.Id,
                    RuleType = SelectedRuleType.Value,
                    Pattern = RulePattern.Trim(),
                    IsRegex = RuleIsRegex,
                    Priority = 0
                });
                _classifier.Reload();
            }

            // Переклассифицируем остальные неклассифицированные интервалы дня —
            // в т.ч. по только что созданному правилу.
            var (from, to) = StatsService.DayBoundsUtc(_watcher.CurrentDate);
            _classifier.ReclassifyUnclassified(from, to);

            CreateRule = false;
            RefreshStats();
        }

        private void PrefillRulePattern()
        {
            if (SelectedUnclassified == null)
            {
                RulePattern = string.Empty;
                return;
            }

            RulePattern = SelectedRuleType.Value switch
            {
                "process" => SelectedUnclassified.ProcessName,
                "title" => SelectedUnclassified.WindowTitle,
                "url" => ExtractHost(SelectedUnclassified.Url) ?? SelectedUnclassified.Url ?? string.Empty,
                _ => string.Empty
            };
        }

        private static string? ExtractHost(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            // У адресной строки Edge схема может отсутствовать — дописываем для Uri.
            var candidate = url.Contains("://") ? url : "https://" + url;
            return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ? uri.Host : null;
        }

        private void ThrottledRefresh()
        {
            OnPropertyChanged(nameof(StatusText));
            if (DateTime.Now - _lastRefresh < RefreshThrottle)
                return;
            RefreshAll();
        }

        private void RefreshAll()
        {
            _lastRefresh = DateTime.Now;
            UpdateTodaySummary();
            RefreshStats();
        }

        private void RefreshStats()
        {
            if (_selectedDay == null)
                return;

            var date = _selectedDay.Date;

            ProjectStats.Clear();
            foreach (var item in _stats.GetDayProjectStats(date))
                ProjectStats.Add(item);

            HourStacks.Clear();
            foreach (var stack in _stats.GetHourStacks(date, ProjectsChartMaxHeight))
                HourStacks.Add(stack);

            TimelineSegments.Clear();
            foreach (var segment in _stats.GetTimelineSegments(date, _watcher.CurrentOpenInterval?.Id, TimelinePixelsPerMinute))
                TimelineSegments.Add(segment);

            var (from, to) = StatsService.DayBoundsUtc(date);
            var selectedId = SelectedUnclassified?.Id;
            Unclassified.Clear();
            foreach (var interval in _intervals.GetUnclassifiedIntervals(from, to))
                Unclassified.Add(UnclassifiedIntervalItem.FromInterval(interval));

            if (selectedId.HasValue)
                SelectedUnclassified = Unclassified.FirstOrDefault(i => i.Id == selectedId.Value);

            OnPropertyChanged(nameof(SelectedDayHeaderText));
        }

        /// <summary>Пересчитать только сегменты таймлайна (при смене масштаба/ширины).</summary>
        private void RefreshTimeline()
        {
            if (_selectedDay == null)
                return;

            TimelineSegments.Clear();
            foreach (var segment in _stats.GetTimelineSegments(_selectedDay.Date, _watcher.CurrentOpenInterval?.Id, TimelinePixelsPerMinute))
                TimelineSegments.Add(segment);
        }

        private void RefreshProjects()
        {
            Projects.Clear();
            foreach (var project in _projects.GetProjects())
                Projects.Add(project);
        }

        private void UpdateTodaySummary()
        {
            var today = _watcher.CurrentDate;
            var summary = AvailableDays.FirstOrDefault(d => d.Date == today);
            if (summary == null)
            {
                summary = new DaySummary { Date = today };
                AvailableDays.Insert(0, summary);
            }

            var seconds = _intervals.GetDaySummaries()
                .Where(d => d.Date == today)
                .Sum(d => d.TotalSeconds);

            // Открытый интервал ещё не посчитан в БД — добавляем его длительность.
            var open = _watcher.CurrentOpenInterval;
            if (open != null && !open.IsIdle)
                seconds += (DateTime.UtcNow - open.StartUtc).TotalSeconds;

            summary.TotalWorkTime = TimeSpan.FromSeconds(seconds);
        }

        private void RefreshAvailableDays()
        {
            AvailableDays.Clear();
            foreach (var (date, seconds) in _intervals.GetDaySummaries())
            {
                AvailableDays.Add(new DaySummary { Date = date, TotalWorkTime = TimeSpan.FromSeconds(seconds) });
            }

            // Сегодняшний день всегда есть в списке, даже если интервалов ещё нет.
            if (AvailableDays.All(d => d.Date != _watcher.CurrentDate))
                AvailableDays.Insert(0, new DaySummary { Date = _watcher.CurrentDate });
        }

        private void SelectToday()
        {
            SelectedDay = AvailableDays.FirstOrDefault(d => d.Date == _watcher.CurrentDate);
        }

        public void Dispose()
        {
            _watcher.Stop();
            _notifications.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
