using System.Windows;
using TimeTracker.Services;

namespace TimeTracker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Страховка: необработанное исключение в UI-потоке не должно ронять трекер.
            DispatcherUnhandledException += (s, args) =>
            {
                LoggerService.LogInfo($"Необработанное исключение UI-потока: {args.Exception}");
                args.Handled = true;
                MessageBox.Show(
                    $"Произошла ошибка, приложение продолжает работу. Подробности — в логе.\n{args.Exception.Message}",
                    "Time Tracker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            base.OnStartup(e);
        }
    }
}
