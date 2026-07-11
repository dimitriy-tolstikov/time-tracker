using System;
using System.Windows;
using System.Windows.Interop;
using TimeTracker.Services;

namespace TimeTracker
{
    public partial class InactivityWarningWindow : Window
    {
        public InactivityWarningWindow()
        {
            InitializeComponent();
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

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
