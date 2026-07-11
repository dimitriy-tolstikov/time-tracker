using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    public class TrackerService
    {
        private readonly DispatcherTimer _timer;
        private readonly List<ActivityAction> _actions = new();
        private readonly object _lock = new();
        private readonly AppConfig _config;
        private DateTime _lastSaveTime;
        private DateTime _lastTickTime;
        private bool _hadInput;
        private bool _inactivityWarningShown;
        private DateTime _currentDate;
        private TimeSpan[] _workTimeByHour = new TimeSpan[24];
        private TimeSpan[] _idleTimeByHour = new TimeSpan[24];
        private TimeSpan _advanceTime;

        public string TargetProcessName { get; set; } = string.Empty;
        public TimeSpan TrackedTime { get; private set; }
        public bool IsTrackingActive { get; private set; }
        public DateTime LastSavedTime { get; private set; }
        public DateTime CurrentDate => _currentDate;

        public IReadOnlyList<ActivityAction> Actions
        {
            get
            {
                lock (_lock) return _actions.ToList();
            }
        }

        public event EventHandler? Updated;
        public event EventHandler? InactivityWarning;
        public event EventHandler? DayRolledOver;

        public TrackerService(AppConfig config)
        {
            _config = config;
            _currentDate = DateTime.Now.Date;

            if (config.LastTrackedDate == default)
            {
                TrackedTime = config.TotalTrackedTime;
            }
            else if (config.LastTrackedDate == _currentDate)
            {
                // Восстанавливаем сегодняшнюю почасовую статистику из файла истории.
                TrackedTime = config.TotalTrackedTime;
                var todayHistory = HistoryService.LoadAll().FirstOrDefault(d => d.Date == _currentDate);
                if (todayHistory != null)
                {
                    _workTimeByHour = todayHistory.WorkTimeByHour.ToArray();
                    _idleTimeByHour = todayHistory.IdleTimeByHour.ToArray();
                }
            }
            else
            {
                // Программа запущена в новый день — сохраняем вчерашний день, если его ещё нет, и начинаем с нуля.
                var history = HistoryService.LoadAll();
                var previousDay = history.FirstOrDefault(d => d.Date == config.LastTrackedDate);
                if (previousDay == null)
                {
                    previousDay = new DayHistory
                    {
                        Date = config.LastTrackedDate,
                        TotalWorkTime = config.TotalTrackedTime,
                        WorkTimeByHour = new TimeSpan[24],
                        IdleTimeByHour = new TimeSpan[24]
                    };
                    HistoryService.SaveDay(previousDay);
                }

                TrackedTime = TimeSpan.Zero;
                config.TotalTrackedTime = TimeSpan.Zero;
                config.LastTrackedDate = _currentDate;
                ConfigService.Save(config);
            }

            _lastSaveTime = DateTime.Now;
            LastSavedTime = DateTime.Now;
            _lastTickTime = DateTime.Now;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
            InputHooks.InputDetected += OnInputDetected;
        }

        private void OnInputDetected()
        {
            lock (_lock)
            {
                _hadInput = true;
            }
        }

        public void Start()
        {
            InputHooks.Install();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            SaveNow();
            SaveCurrentDayHistory();
            InputHooks.Uninstall();
        }

        public void Reset()
        {
            lock (_lock)
            {
                _actions.Clear();
                TrackedTime = TimeSpan.Zero;
                IsTrackingActive = false;
                _workTimeByHour = new TimeSpan[24];
                _idleTimeByHour = new TimeSpan[24];
                _advanceTime = TimeSpan.Zero;
            }
            _config.TotalTrackedTime = TimeSpan.Zero;
            _config.LastTrackedDate = DateTime.Now.Date;
            ConfigService.Save(_config);
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public void SaveNow()
        {
            lock (_lock)
            {
                _config.TotalTrackedTime = TrackedTime;
            }
            _config.LastTrackedDate = _currentDate;
            ConfigService.Save(_config);
            LastSavedTime = DateTime.Now;
            _lastSaveTime = DateTime.Now;
        }

        public DayHistory GetCurrentDayHistory()
        {
            lock (_lock)
            {
                return new DayHistory
                {
                    Date = _currentDate,
                    TotalWorkTime = TrackedTime,
                    WorkTimeByHour = _workTimeByHour.ToArray(),
                    IdleTimeByHour = _idleTimeByHour.ToArray()
                };
            }
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var activeProcessName = ProcessHelper.GetActiveProcessName();
            var isTargetActive = !string.IsNullOrEmpty(TargetProcessName)
                && string.Equals(activeProcessName, TargetProcessName, StringComparison.OrdinalIgnoreCase);

            bool hadInput;
            lock (_lock)
            {
                hadInput = _hadInput;
                _hadInput = false;
            }

            if (isTargetActive && hadInput)
            {
                lock (_lock)
                {
                    _actions.Add(new ActivityAction { Time = now });
                    if (_actions.Count > 5000)
                        _actions.RemoveAt(0);
                    _advanceTime = TimeSpan.Zero;
                }
            }

            CheckDayRollover(now);
            RecalculateTime(now, isTargetActive, hadInput, activeProcessName);
            CheckInactivityWarning(now, isTargetActive);
            TryAutoSave(now);
            Updated?.Invoke(this, EventArgs.Empty);

            _lastTickTime = now;
        }

        private void CheckDayRollover(DateTime now)
        {
            if (now.Date == _currentDate)
                return;

            lock (_lock)
            {
                SaveCurrentDayHistoryInternal();

                _currentDate = now.Date;
                TrackedTime = TimeSpan.Zero;
                _workTimeByHour = new TimeSpan[24];
                _idleTimeByHour = new TimeSpan[24];
                _actions.Clear();
                _advanceTime = TimeSpan.Zero;
                _inactivityWarningShown = false;

                _config.TotalTrackedTime = TimeSpan.Zero;
                _config.LastTrackedDate = _currentDate;
                ConfigService.Save(_config);
            }

            DayRolledOver?.Invoke(this, EventArgs.Empty);
        }

        private void TryAutoSave(DateTime now)
        {
            if (now - _lastSaveTime >= TimeSpan.FromMinutes(1))
            {
                SaveNow();
                SaveCurrentDayHistory();
            }
        }

        private void SaveCurrentDayHistory()
        {
            lock (_lock)
            {
                SaveCurrentDayHistoryInternal();
            }
        }

        private void SaveCurrentDayHistoryInternal()
        {
            var day = new DayHistory
            {
                Date = _currentDate,
                TotalWorkTime = TrackedTime,
                WorkTimeByHour = _workTimeByHour.ToArray(),
                IdleTimeByHour = _idleTimeByHour.ToArray()
            };
            HistoryService.SaveDay(day);
        }

        private void CheckInactivityWarning(DateTime now, bool isTargetActive)
        {
            bool shouldWarn = false;

            lock (_lock)
            {
                if (isTargetActive || _actions.Count == 0)
                {
                    _inactivityWarningShown = false;
                    return;
                }

                var lastActionTime = _actions[^1].Time;
                var inactiveDuration = now - lastActionTime;

                if (inactiveDuration >= TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(30))
                    && inactiveDuration < TimeSpan.FromMinutes(5)
                    && !_inactivityWarningShown)
                {
                    _inactivityWarningShown = true;
                    shouldWarn = true;
                }
            }

            if (shouldWarn)
            {
                InactivityWarning?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RecalculateTime(DateTime now, bool isTargetActive, bool hadInput, string activeProcessName)
        {
            lock (_lock)
            {
                var sorted = _actions.OrderBy(a => a.Time).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    if (i > 0)
                    {
                        var diff = sorted[i].Time - sorted[i - 1].Time;
                        sorted[i].IntervalSincePrevious = diff;
                        sorted[i].IsCounted = diff <= TimeSpan.FromMinutes(5);
                    }
                    else
                    {
                        sorted[i].IntervalSincePrevious = null;
                        sorted[i].IsCounted = false;
                    }
                }

                IsTrackingActive = false;
                DateTime? lastActionTime = null;
                var sinceLast = TimeSpan.Zero;
                if (sorted.Count > 0)
                {
                    var last = sorted[^1];
                    lastActionTime = last.Time;
                    sinceLast = now - last.Time;
                    if (sinceLast <= TimeSpan.FromMinutes(5))
                    {
                        IsTrackingActive = true;
                    }
                }

                var elapsed = now - _lastTickTime;
                if (elapsed > TimeSpan.FromSeconds(2))
                    elapsed = TimeSpan.FromSeconds(1);

                var currentHour = now.Hour;
                var advanceCorrected = false;

                if (IsTrackingActive)
                {
                    TrackedTime += elapsed;
                    _workTimeByHour[currentHour] += elapsed;
                    _advanceTime += elapsed;
                }
                else
                {
                    _idleTimeByHour[currentHour] += elapsed;
                    if (_advanceTime > TimeSpan.Zero)
                    {
                        TrackedTime -= _advanceTime;
                        if (TrackedTime < TimeSpan.Zero)
                            TrackedTime = TimeSpan.Zero;
                        _advanceTime = TimeSpan.Zero;
                        advanceCorrected = true;
                    }
                }

                LoggerService.LogTick(new TickLogEntry
                {
                    Timestamp = now,
                    ActiveProcess = activeProcessName,
                    TargetProcess = TargetProcessName,
                    IsTargetActive = isTargetActive,
                    HadInput = hadInput,
                    LastActionTime = lastActionTime,
                    SinceLastSeconds = sinceLast.TotalSeconds,
                    IsTrackingActive = IsTrackingActive,
                    ElapsedSeconds = elapsed.TotalSeconds,
                    TrackedTimeSeconds = TrackedTime.TotalSeconds,
                    AdvanceTimeSeconds = _advanceTime.TotalSeconds,
                    AdvanceCorrected = advanceCorrected,
                    WorkHourSeconds = _workTimeByHour[currentHour].TotalSeconds,
                    IdleHourSeconds = _idleTimeByHour[currentHour].TotalSeconds,
                    CurrentHour = currentHour,
                    ActionCount = sorted.Count
                });
            }
        }
    }
}
