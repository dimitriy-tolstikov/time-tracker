using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TimeTracker.Services;

namespace TimeTracker
{
    public partial class InactivityWarningWindow : Window
    {
        /// <summary>
        /// Возникает при нажатии «Отложить». Аргумент — выбранная длительность отложки.
        /// </summary>
        public event EventHandler<TimeSpan>? SnoozeRequested;

        /// <summary>
        /// Возникает при нажатии «Переключиться» — нужно активировать окно проекта.
        /// </summary>
        public event EventHandler? SwitchRequested;

        public InactivityWarningWindow(string title, string message, string severity, bool showSwitchButton)
        {
            InitializeComponent();

            // Оформление по уровню серьёзности: белый / жёлтый / красный.
            switch (severity)
            {
                case "critical":
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDECEA"));
                    TitleText.Text = "⛔ " + title;
                    TitleText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B71C1C"));
                    break;
                case "warning":
                    // Стиль по умолчанию из XAML (желтоватый).
                    TitleText.Text = "⚠ " + title;
                    break;
                default:
                    Background = new SolidColorBrush(Colors.White);
                    TitleText.Text = "ℹ " + title;
                    TitleText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                    break;
            }

            MessageText.Text = message;

            if (!showSwitchButton)
                SwitchButton.Visibility = Visibility.Collapsed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Гарантируем, что окно останется поверх всех окон, включая другие topmost-окна.
            var helper = new WindowInteropHelper(this);
            NativeMethods.SetWindowPos(
                helper.Handle,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void OnSnoozeClick(object sender, RoutedEventArgs e)
        {
            int minutes = 5;
            if (SnoozeCombo.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int parsed))
            {
                minutes = parsed;
            }

            SnoozeRequested?.Invoke(this, TimeSpan.FromMinutes(minutes));
            Close();
        }

        private void OnSwitchClick(object sender, RoutedEventArgs e)
        {
            SwitchRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
