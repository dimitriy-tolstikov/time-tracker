using System;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using TimeTracker.Services;
using TimeTracker.ViewModels;

namespace TimeTracker
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _notifyIcon;
        private MainViewModel? _viewModel;
        private InactivityWarningWindow? _warningWindow;

        public MainWindow()
        {
            InitializeComponent();
            // Одноразовый импорт истории старой версии (history.json) до создания VM.
            LegacyMigrationService.ImportIfNeeded();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _viewModel.ShowProjectNotification += OnShowProjectNotification;
            _viewModel.OpenProjectsRequested += OnOpenProjectsRequested;

            // Вкладка проектов: отдельная VM на том же репозитории,
            // любые её изменения сразу отражаются на главной вкладке.
            var projectsViewModel = new ProjectsViewModel(_viewModel.ProjectsRepository);
            projectsViewModel.Changed += (s, e) => _viewModel.OnProjectsChanged();
            ProjectsViewControl.DataContext = projectsViewModel;

            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            var contextMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("Показать");
            showItem.Click += (s, e) => ShowMainWindow();

            var autostartItem = new ToolStripMenuItem("Автозапуск")
            {
                Checked = IsAutostartEnabled()
            };
            autostartItem.Click += (s, e) =>
            {
                ToggleAutostart(!autostartItem.Checked);
                autostartItem.Checked = IsAutostartEnabled();
            };

            var exitItem = new ToolStripMenuItem("Выход");
            exitItem.Click += (s, e) => CloseApplication();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(autostartItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clock.ico");
            var trayIcon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;

            _notifyIcon = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "Time Tracker",
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void OnShowProjectNotification(object? sender, ProjectNotificationEventArgs e)
        {
            // Если предыдущее уведомление ещё открыто — закрываем его и показываем актуальное.
            _warningWindow?.Close();

            SystemSound sound = e.Severity switch
            {
                "critical" => SystemSounds.Hand,
                "warning" => SystemSounds.Exclamation,
                _ => SystemSounds.Asterisk,
            };
            sound.Play();

            _warningWindow = new InactivityWarningWindow(
                "Time Tracker",
                e.Message,
                e.Severity,
                showSwitchButton: !string.IsNullOrEmpty(e.ProcessName));
            _warningWindow.SnoozeRequested += (s, duration) => _viewModel?.SnoozeNotification(e.Project.Id, duration);
            _warningWindow.SwitchRequested += (s, args) =>
            {
                if (!string.IsNullOrEmpty(e.ProcessName))
                    ProcessHelper.SwitchToWindow(e.ProcessName);
            };
            _warningWindow.Closed += (s, args) => _warningWindow = null;
            _warningWindow.Show();
        }

        private void OnOpenProjectsRequested(object? sender, EventArgs e)
        {
            // Кнопка «Проекты и правила» на панели инструментов просто переключает вкладку.
            MainTabControl.SelectedItem = ProjectsTab;
        }

        /// <summary>Таймлайн при зуме 1x растягивается на всю видимую ширину скролла.</summary>
        private void TimelineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.NewSize.Width > 0)
                vm.TimelineBaseWidth = e.NewSize.Width;
        }

        /// <summary>Столбцы почасового графика масштабируются по фактической высоте области.</summary>
        private void ProjectsChart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Минус место под подписи часов под столбцами.
            if (DataContext is MainViewModel vm && e.NewSize.Height > 20)
                vm.ProjectsChartMaxHeight = e.NewSize.Height - 20;
        }

        /// <summary>Подсветка названия проекта сегмента таймлайна под курсором.</summary>
        private void TimelineSegment_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is MainViewModel vm &&
                sender is FrameworkElement fe &&
                fe.DataContext is TimeTracker.Models.TimelineSegment seg)
            {
                var timeRange = seg.ToolTipText.Split('\n')[0];
                vm.HoveredTimelineText = $"{seg.ProjectName}  ·  {timeRange}";
            }
        }

        private void TimelineSegment_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.HoveredTimelineText = string.Empty;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (DataContext is IDisposable disposable)
                disposable.Dispose();

            base.OnClosing(e);
        }

        private void CloseApplication()
        {
            Close();
        }

        private static bool IsAutostartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("TimeTracker") != null;
            }
            catch
            {
                return false;
            }
        }

        private static void ToggleAutostart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null)
                    return;

                if (enable)
                {
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimeTracker.exe");
                    key.SetValue("TimeTracker", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("TimeTracker", false);
                }
            }
            catch { }
        }
    }
}
