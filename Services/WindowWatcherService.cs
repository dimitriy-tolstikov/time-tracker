using System;
using System.Windows.Threading;

namespace TimeTracker.Services
{
    /// <summary>
    /// Следит за активным окном (поллинг раз в 2 секунды) и пишет интервалы активности в БД.
    /// Заменяет старый TrackerService: вместо одного целевого окна логируются все окна.
    ///
    /// Правила формирования интервалов:
    /// - микро-интервалы от Alt-Tab отсекаются: новое окно открывается, только если
    ///   удерживается ≥ 2 тиков подряд;
    /// - переход через полночь: интервал закрывается на границе дня и открывается новый;
    /// - если ввода не было дольше <see cref="InactivityTimeout"/> — новые интервалы
    ///   помечаются is_idle и не засчитываются в рабочее время проектов.
    /// </summary>
    public class WindowWatcherService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        // Как часто продлеваем end_utc открытого интервала в БД, чтобы долгая
        // работа в одном окне не выглядела неактивностью. Заметно меньше любого
        // разумного порога уведомлений, поэтому писать чаще смысла нет.
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

        private readonly DispatcherTimer _timer;
        private readonly IntervalRepository _intervals;
        private readonly ClassificationService _classifier;

        private DateTime _lastInputTime = DateTime.Now;
        private DateTime _currentDate;
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;
        private Models.ActivityInterval? _open;

        // Кандидат на смену окна: накапливаем подтверждения против мусора Alt-Tab.
        private WindowKey? _pendingKey;
        private int _pendingTicks;

        /// <summary>Через сколько бездействия интервалы помечаются idle.</summary>
        public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public DateTime CurrentDate => _currentDate;

        /// <summary>Сейчас открытый (ещё не закрытый) интервал; null — окно ещё не подтвердилось.</summary>
        public Models.ActivityInterval? CurrentOpenInterval => _open;

        public string CurrentWindowText { get; private set; } = string.Empty;

        /// <summary>Интервал открыт/закрыт или сменился день — UI стоит обновить статистику.</summary>
        public event EventHandler? Updated;
        public event EventHandler? DayRolledOver;

        public WindowWatcherService(IntervalRepository intervals, ClassificationService classifier)
        {
            _intervals = intervals;
            _classifier = classifier;
            _currentDate = DateTime.Now.Date;

            _timer = new DispatcherTimer { Interval = PollInterval };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            InputHooks.InputDetected += OnInputDetected;
            InputHooks.Install();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            InputHooks.InputDetected -= OnInputDetected;
            InputHooks.Uninstall();

            if (_open != null)
            {
                CloseOpenInterval(DateTime.UtcNow);
            }
        }

        private void OnInputDetected()
        {
            _lastInputTime = DateTime.Now;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                LoggerService.LogInfo($"WindowWatcherService.Tick: {ex}");
            }
        }

        private void Tick()
        {
            var now = DateTime.Now;
            var nowUtc = now.ToUniversalTime();

            // Переход через полночь: закрываем интервал на границе дня, дальше откроется новый.
            if (now.Date != _currentDate)
            {
                if (_open != null)
                    CloseOpenInterval(now.Date.ToUniversalTime());

                _currentDate = now.Date;
                _pendingKey = null;
                _pendingTicks = 0;
                DayRolledOver?.Invoke(this, EventArgs.Empty);
            }

            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return;

            var processName = ProcessHelper.GetActiveProcessName();
            if (string.IsNullOrEmpty(processName))
                return;

            var title = NativeMethods.GetWindowTitle(hwnd);
            string? url = null;
            if (string.Equals(processName, "msedge", StringComparison.OrdinalIgnoreCase))
                url = EdgeUrlProvider.GetUrl(hwnd);

            var key = new WindowKey(processName, title, url);
            var isIdle = now - _lastInputTime > InactivityTimeout;
            CurrentWindowText = string.IsNullOrEmpty(title)
                ? processName
                : $"{processName} — {title}";

            if (_open != null && _open.IsIdle != isIdle)
            {
                // Смена idle-состояния — применяем сразу, без подтверждения.
                CloseOpenInterval(nowUtc);
                OpenInterval(key, isIdle, nowUtc);
                _pendingKey = null;
                _pendingTicks = 0;
                return;
            }

            // Heartbeat: продлеваем открытый интервал раз в HeartbeatInterval,
            // чтобы БД видела живую активность. Иначе end_utc заморожен на моменте
            // входа в окно, и долгая работа в одном окне выглядит как неактивность.
            if (_open != null && nowUtc - _lastHeartbeatUtc >= HeartbeatInterval)
            {
                _intervals.UpdateIntervalEnd(_open.Id, nowUtc, _open.IsIdle);
                _lastHeartbeatUtc = nowUtc;
            }

            var currentKey = _open != null
                ? new WindowKey(_open.ProcessName, _open.WindowTitle, _open.Url)
                : (WindowKey?)null;

            if (currentKey.HasValue && currentKey.Value.Equals(key))
            {
                // Окно то же — сбрасываем кандидата.
                _pendingKey = null;
                _pendingTicks = 0;
                return;
            }

            // Окно другое: ждём подтверждения (тот же кандидат ≥ 2 тиков подряд).
            if (_pendingKey.HasValue && _pendingKey.Value.Equals(key))
            {
                _pendingTicks++;
            }
            else
            {
                _pendingKey = key;
                _pendingTicks = 1;
            }

            if (_pendingTicks >= 2)
            {
                if (_open != null)
                    CloseOpenInterval(nowUtc);
                OpenInterval(key, isIdle, nowUtc);
                _pendingKey = null;
                _pendingTicks = 0;
            }
        }

        private void OpenInterval(WindowKey key, bool isIdle, DateTime utc)
        {
            var interval = new Models.ActivityInterval
            {
                StartUtc = utc,
                EndUtc = utc,
                ProcessName = key.ProcessName,
                WindowTitle = key.Title,
                Url = key.Url,
                IsIdle = isIdle,
                ProjectId = _classifier.Classify(key.ProcessName, key.Title, key.Url)
            };
            interval.Id = _intervals.InsertInterval(interval);
            _open = interval;
            // Только что вставили с end = start — ближайшие HeartbeatInterval писать не нужно.
            _lastHeartbeatUtc = utc;
            Updated?.Invoke(this, EventArgs.Empty);
        }

        private void CloseOpenInterval(DateTime endUtc)
        {
            if (_open == null)
                return;

            // Интервал нулевой длительности смысла не имеет — не трогаем end, если он уже равен start.
            if (endUtc < _open.StartUtc)
                endUtc = _open.StartUtc;

            _intervals.UpdateIntervalEnd(_open.Id, endUtc, _open.IsIdle);
            _open = null;
            Updated?.Invoke(this, EventArgs.Empty);
        }

        private readonly struct WindowKey : IEquatable<WindowKey>
        {
            public WindowKey(string processName, string title, string? url)
            {
                ProcessName = processName;
                Title = title;
                Url = url;
            }

            public string ProcessName { get; }
            public string Title { get; }
            public string? Url { get; }

            public bool Equals(WindowKey other)
                => string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase)
                   && Title == other.Title
                   && Url == other.Url;

            public override bool Equals(object? obj) => obj is WindowKey other && Equals(other);

            public override int GetHashCode()
                => (ProcessName.ToLowerInvariant(), Title, Url).GetHashCode();
        }
    }
}
