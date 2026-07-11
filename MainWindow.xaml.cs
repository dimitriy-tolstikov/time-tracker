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
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _viewModel.ShowInactivityWarning += OnShowInactivityWarning;

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

        private void OnShowInactivityWarning(object? sender, EventArgs e)
        {
            if (_warningWindow != null)
                return;

            SystemSounds.Exclamation.Play();

            _warningWindow = new InactivityWarningWindow();
            _warningWindow.Closed += (s, args) => _warningWindow = null;
            _warningWindow.Show();
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
