# Time Tracker

[Русская версия](README.ru.md)

A desktop WPF application for Windows that automatically tracks time in all active windows: it logs activity intervals (process, window title, URL for Edge), classifies them into projects using configurable rules, and builds per-project statistics with notifications.

## Features

- **Automatic activity logging** — the active window is polled every 2 seconds; intervals are written to SQLite with the process name, window title, and idle flag (no keyboard/mouse input). Alt-Tab flicker is filtered out.
- **Projects and classification rules** — intervals are assigned to projects by rules: exact process name, window title fragment, or URL fragment (plain text or regex, with priorities). Unclassified intervals can be assigned manually from the "Unclassified" list, optionally creating a rule.
- **Edge tab URL** — the URL of the active Edge tab is captured via UI Automation and stored with the interval (Firefox/Chrome are out of scope for now).
- **Per-project statistics** — a list of projects with the total time for the selected day and a day chart in two modes: "By projects" (hourly stacked bars) and "Timeline" (a 00:00–24:00 strip of activity segments).
- **Per-project notifications** — warn when there is no activity in a project for N minutes or when the project's daily limit is exceeded (always-on-top window with snooze). Each notification has a configurable severity level (white/yellow/red) driving the window color and sound; triggering is sub-second precise (adaptive timer, no busy polling).
- **Day history** — a list of days with total worked time, automatic rollover at midnight (intervals are split at the day boundary).

## Screenshot

![Time Tracker main window](clock.png)

## Tech Stack

- C# / .NET 8 (`net8.0-windows`)
- WPF + Windows Forms (global input hooks via Win32 API)
- MVVM (Models / ViewModels / Services / Converters)
- SQLite (`Microsoft.Data.Sqlite`) + Dapper for storage
- FlaUI (UIA3) for reading the Edge address bar

## Project Structure

```
.
├── Models/                        # ActivityInterval, Project, ClassificationRule and UI models
├── ViewModels/                    # MainViewModel, ProjectsViewModel, RelayCommand
├── Services/                      # WindowWatcherService, Database, IntervalRepository,
│                                  # ProjectRepository, ClassificationService, StatsService,
│                                  # NotificationService, EdgeUrlProvider, InputHooks and others
├── Converters/                    # XAML converters
├── tools/                         # IconConverter — icon utility (excluded from the build)
├── MainWindow.xaml                # main window
├── ProjectsWindow.xaml            # projects and rules management window
├── InactivityWarningWindow.xaml   # notification window
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

All data lives in a single SQLite database `%LocalAppData%\TimeTracker\tracker.db`: activity intervals, projects, classification rules, and application settings (the `settings` table). The old version's `config.json` / `history.json` files are neither read nor written — the old version keeps owning them while running in parallel.

## License

MIT — see [LICENSE](LICENSE).
