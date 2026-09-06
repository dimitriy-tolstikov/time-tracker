using System;
using System.Collections.Generic;
using System.Windows.Threading;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    public class ProjectNotificationEventArgs : EventArgs
    {
        public ProjectNotificationEventArgs(Project project, string message, string severity, string? processName)
        {
            Project = project;
            Message = message;
            Severity = severity;
            ProcessName = processName;
        }

        public Project Project { get; }
        public string Message { get; }
        /// <summary>Уровень серьёзности: "normal" (белый), "warning" (жёлтый), "critical" (красный).</summary>
        public string Severity { get; }
        /// <summary>Процесс, на который предлагается переключиться (самый частый в проекте за день).</summary>
        public string? ProcessName { get; }
    }

    /// <summary>
    /// Уведомления с настройками на уровне проекта (таблица notifications,
    /// у проекта может быть несколько):
    /// - нет активности в проекте N секунд (при условии, что сегодня она уже была);
    /// - превышен дневной лимит секунд по проекту.
    /// «Уже уведомляли сегодня» хранится в памяти по id уведомления и сбрасывается
    /// при смене дня (для «неактивности» запоминается конец последней активности
    /// на момент срабатывания — после новой активности в проекте уведомление
    /// срабатывает повторно). Отложка (snooze) тоже в памяти.
    ///
    /// Таймер адаптивный: фиксированного опроса «N раз в секунду» нет. После каждой
    /// проверки следующий тик ставится ровно на ближайший момент возможного
    /// срабатывания (точность до долей секунды):
    /// - у «неактивности» дедлайн известен точно: конец последнего интервала + порог
    ///   (новая активность может только отодвинуть его, тогда пересчитаем на тике);
    /// - у «дневного лимита» сумма растёт не быстрее реального времени, значит раньше
    ///   (сейчас + остаток до лимита) сработать не может.
    /// Сверху интервал ограничен MaxInterval — это редкая ресинхронизация на случай
    /// новой активности, когда дедлайнов ещё нет. Изменения настроек попадают
    /// в расписание сразу через WakeUp().
    /// </summary>
    public class NotificationService : IDisposable
    {
        private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(30);

        private readonly DispatcherTimer _timer;
        private readonly ProjectRepository _projects;
        private readonly IntervalRepository _intervals;

        private readonly HashSet<long> _notifiedToday = new();
        /// <summary>
        /// Для уведомлений о неактивности: конец последней активности на момент
        /// срабатывания. Если после этого в проекте была новая активность
        /// (lastEnd сдвинулся вперёд), уведомление может сработать повторно.
        /// </summary>
        private readonly Dictionary<long, DateTime> _inactivityNotifiedEndUtc = new();
        private readonly Dictionary<long, DateTime> _snoozedUntilUtc = new();
        private DateTime _currentDate;

        public event EventHandler<ProjectNotificationEventArgs>? NotificationRequested;

        public NotificationService(ProjectRepository projects, IntervalRepository intervals)
        {
            _projects = projects;
            _intervals = intervals;
            _currentDate = DateTime.Now.Date;

            _timer = new DispatcherTimer { Interval = MinInterval };
            _timer.Tick += (s, e) => Check();
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        /// <summary>Немедленно пересчитать расписание (настройки уведомлений изменились).</summary>
        public void WakeUp() => _timer.Interval = MinInterval;

        /// <summary>Отложить уведомления по проекту на указанное время.</summary>
        public void Snooze(long projectId, TimeSpan duration)
        {
            _snoozedUntilUtc[projectId] = DateTime.UtcNow.Add(duration);
            WakeUp();
        }

        private void Check()
        {
            try
            {
                var today = DateTime.Now.Date;
                if (today != _currentDate)
                {
                    _currentDate = today;
                    _notifiedToday.Clear();
                    _inactivityNotifiedEndUtc.Clear();
                    _snoozedUntilUtc.Clear();
                }

                var (from, to) = StatsService.DayBoundsUtc(today);
                var nowUtc = DateTime.UtcNow;
                var notifications = _projects.GetNotifications();
                Dictionary<long, double>? totals = null;
                var lastActivityEnd = new Dictionary<long, DateTime?>();

                DateTime? nextDeadline = null;
                void Consider(DateTime candidate)
                {
                    if (nextDeadline == null || candidate < nextDeadline.Value)
                        nextDeadline = candidate;
                }

                foreach (var project in _projects.GetProjects())
                {
                    if (_snoozedUntilUtc.TryGetValue(project.Id, out var snoozedUntil) && nowUtc < snoozedUntil)
                    {
                        Consider(snoozedUntil);
                        continue;
                    }

                    foreach (var notification in notifications)
                    {
                        if (notification.ProjectId != project.Id)
                            continue;

                        if (notification.Type == "inactivity")
                        {
                            if (!lastActivityEnd.TryGetValue(project.Id, out var lastEnd))
                                lastActivityEnd[project.Id] = lastEnd = _intervals.GetLastActivityEndUtc(project.Id, from, to);

                            if (!lastEnd.HasValue)
                                continue;

                            // Уже уведомляли про этот период неактивности; повторное
                            // срабатывание возможно после новой активности в проекте.
                            if (_inactivityNotifiedEndUtc.TryGetValue(notification.Id, out var notifiedEnd)
                                && lastEnd.Value <= notifiedEnd)
                                continue;

                            var deadline = lastEnd.Value.AddSeconds(notification.ThresholdSeconds);
                            if (nowUtc >= deadline)
                            {
                                _inactivityNotifiedEndUtc[notification.Id] = lastEnd.Value;
                                Notify(project,
                                    $"Нет активности в проекте «{project.Name}» уже {ProjectNotification.FormatDuration(notification.ThresholdSeconds)}.",
                                    notification.Severity, from, to);
                            }
                            else
                            {
                                Consider(deadline);
                            }
                        }
                        else if (notification.Type == "daily_limit")
                        {
                            if (_notifiedToday.Contains(notification.Id))
                                continue;

                            totals ??= _intervals.GetProjectTotals(from, to);
                            totals.TryGetValue(project.Id, out var seconds);

                            if (seconds >= notification.ThresholdSeconds)
                            {
                                _notifiedToday.Add(notification.Id);
                                Notify(project,
                                    $"Превышен дневной лимит проекта «{project.Name}» ({ProjectNotification.FormatDuration(notification.ThresholdSeconds)}).",
                                    notification.Severity, from, to);
                            }
                            else
                            {
                                Consider(nowUtc.AddSeconds(notification.ThresholdSeconds - seconds));
                            }
                        }
                    }
                }

                // Полночь: смена дня, сброс «уже уведомляли» и отложек.
                Consider(to);

                var delay = (nextDeadline ?? nowUtc.Add(MaxInterval)) - nowUtc;
                _timer.Interval = delay < MinInterval ? MinInterval
                    : delay > MaxInterval ? MaxInterval
                    : delay;
            }
            catch (Exception ex)
            {
                LoggerService.LogInfo($"NotificationService.Check: {ex}");
                _timer.Interval = MaxInterval;
            }
        }

        private void Notify(Project project, string message, string severity, DateTime fromUtc, DateTime toUtc)
        {
            var process = _intervals.GetMostFrequentProcess(project.Id, fromUtc, toUtc);
            NotificationRequested?.Invoke(this, new ProjectNotificationEventArgs(project, message, severity, process));
        }

        public void Dispose() => Stop();
    }
}
