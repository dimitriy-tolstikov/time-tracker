using System;
using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;

namespace TimeTracker.Services
{
    /// <summary>
    /// Подключение к SQLite (%LocalAppData%/TimeTracker/tracker.db) и версионирование
    /// схемы через PRAGMA user_version. Старые config.json/history.json здесь
    /// не используются — ими владеет параллельно работающая старая версия приложения.
    /// </summary>
    public static class Database
    {
        private static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTracker",
            "tracker.db");

        private static bool _initialized;
        private static readonly object InitLock = new();

        /// <summary>Формат хранения времени в БД (понятен и SQLite julianday(), и DateTime.Parse).</summary>
        public const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        static Database()
        {
            // Dapper: сопоставление snake_case колонок БД с PascalCase свойствами моделей.
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public static string FormatUtc(DateTime utc) => utc.ToString(TimeFormat);

        public static SqliteConnection OpenConnection()
        {
            EnsureInitialized();
            var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            connection.Execute("PRAGMA foreign_keys = ON;");
            return connection;
        }

        private static void EnsureInitialized()
        {
            lock (InitLock)
            {
                if (_initialized)
                    return;

                var directory = Path.GetDirectoryName(DbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using var connection = new SqliteConnection($"Data Source={DbPath}");
                connection.Open();

                var version = connection.ExecuteScalar<int>("PRAGMA user_version;");
                if (version < 1)
                {
                    // Свежая БД сразу создаётся в актуальной схеме.
                    connection.Execute(InitialSchema);
                    connection.Execute("PRAGMA user_version = 4;");
                }
                else
                {
                    if (version < 2)
                    {
                        // v2: период неактивности хранится в секундах, а не в минутах.
                        connection.Execute(@"
ALTER TABLE projects ADD COLUMN notify_inactivity_seconds INTEGER NULL;
UPDATE projects SET notify_inactivity_seconds = notify_inactivity_minutes * 60
WHERE notify_inactivity_minutes IS NOT NULL;
ALTER TABLE projects DROP COLUMN notify_inactivity_minutes;");
                        connection.Execute("PRAGMA user_version = 2;");
                    }

                    if (version < 3)
                    {
                        // v3: уведомления — отдельная таблица, у проекта может быть несколько.
                        connection.Execute(@"
CREATE TABLE notifications (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  type TEXT NOT NULL CHECK (type IN ('inactivity','daily_limit')),
  threshold_seconds INTEGER NOT NULL
);
INSERT INTO notifications (project_id, type, threshold_seconds)
SELECT id, 'inactivity', notify_inactivity_seconds FROM projects
WHERE notify_enabled = 1 AND notify_inactivity_seconds IS NOT NULL;
INSERT INTO notifications (project_id, type, threshold_seconds)
SELECT id, 'daily_limit', notify_daily_limit_minutes * 60 FROM projects
WHERE notify_enabled = 1 AND notify_daily_limit_minutes IS NOT NULL;
ALTER TABLE projects DROP COLUMN notify_inactivity_seconds;
ALTER TABLE projects DROP COLUMN notify_daily_limit_minutes;
ALTER TABLE projects DROP COLUMN notify_enabled;");
                        connection.Execute("PRAGMA user_version = 3;");
                    }

                    if (version < 4)
                    {
                        // v4: у уведомления появляется уровень серьёзности (цвет окна).
                        connection.Execute(@"
ALTER TABLE notifications ADD COLUMN severity TEXT NOT NULL DEFAULT 'normal'
CHECK (severity IN ('normal','warning','critical'));");
                        connection.Execute("PRAGMA user_version = 4;");
                    }
                }

                _initialized = true;
            }
        }

        private const string InitialSchema = @"
CREATE TABLE projects (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  color TEXT NOT NULL DEFAULT '#888888'
);

CREATE TABLE notifications (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  type TEXT NOT NULL CHECK (type IN ('inactivity','daily_limit')),
  threshold_seconds INTEGER NOT NULL,
  severity TEXT NOT NULL DEFAULT 'normal' CHECK (severity IN ('normal','warning','critical'))
);

CREATE TABLE rules (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  rule_type TEXT NOT NULL CHECK (rule_type IN ('process','title','url')),
  pattern TEXT NOT NULL,
  is_regex INTEGER NOT NULL DEFAULT 0,
  priority INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE intervals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  start_utc TEXT NOT NULL,
  end_utc TEXT NOT NULL,
  process_name TEXT NOT NULL,
  window_title TEXT NOT NULL DEFAULT '',
  url TEXT NULL,
  is_idle INTEGER NOT NULL DEFAULT 0,
  project_id INTEGER NULL REFERENCES projects(id) ON DELETE SET NULL
);
CREATE INDEX ix_intervals_start ON intervals(start_utc);
CREATE INDEX ix_intervals_project ON intervals(project_id);

CREATE TABLE settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
";
    }
}
