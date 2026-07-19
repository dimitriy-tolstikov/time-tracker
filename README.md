# Time Tracker

[Русская версия](README.ru.md)

A desktop WPF application for Windows that tracks working time in a selected application: it measures activity based on keyboard and mouse input, builds hourly statistics, and keeps a day-by-day history.

## Features

- **Target window selection** — the "Specify window" button gives you 5 seconds to switch to the desired application; after that, the tracker monitors its process.
- **Active time tracking** — global input hooks (keyboard and mouse) capture activity; time is counted only while the user is working in the target window.
- **Hourly statistics** — distribution of working time and idle time across the 24 hours of the selected day.
- **Day-by-day history** — a list of days with total time worked, with automatic rollover to a new day.
- **Inactivity warning** — a separate always-on-top notification if input has been absent for a long time.
- **Autosave** — the config and history are saved automatically (the last save time is shown in the interface).
- **Reset** — reset the current day's counter with a single button.

## Screenshot

![Time Tracker main window](clock.png)

## Tech Stack

- C# / .NET 8 (`net8.0-windows`)
- WPF + Windows Forms (global input hooks via Win32 API)
- MVVM (Models / ViewModels / Services / Converters)
- `System.Text.Json` for storing the config and history

## Project Structure

```
.
├── Models/                        # ActivityAction, DayHistory, DaySummary, HourlyStat
├── ViewModels/                    # MainViewModel, RelayCommand
├── Services/                      # TrackerService, InputHooks, HistoryService,
│                                  # ConfigService, NativeMethods, ProcessHelper and others
├── Converters/                    # XAML converters
├── tools/                         # IconConverter — icon utility (excluded from the build)
├── MainWindow.xaml                # main window
├── InactivityWarningWindow.xaml   # inactivity warning window
└── TimeTracker.sln
```

## Requirements

- Windows
- .NET 8 SDK (the exact version is pinned in `global.json`)

## Build and Run

Via the .NET CLI:

```bash
dotnet build
dotnet run --project TimeTracker.csproj
```

Or open `TimeTracker.sln` in Visual Studio and run the project (F5).

## Where Data Is Stored

The config (`config.json`) and history (`history.json`) are saved to `%LocalAppData%\TimeTracker\` and duplicated next to the executable file. On load, data from `%LocalAppData%` takes priority.

## License

MIT — see [LICENSE](LICENSE).
