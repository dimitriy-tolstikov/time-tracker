using System.Windows;

namespace TimeTracker.Services
{
    public static class TopmostMessageBox
    {
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var window = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000
            };

            window.Show();
            try
            {
                return MessageBox.Show(window, messageBoxText, caption, button, icon);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
