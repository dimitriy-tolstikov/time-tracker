using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    /// <summary>
    /// CRUD интервалов активности и агрегирующие запросы (Dapper).
    /// Все границы периодов передаются в UTC в формате <see cref="Database.TimeFormat"/>.
    /// </summary>
    public class IntervalRepository
    {
        /// <summary>Вставляет открытый интервал (start = end), возвращает его id.</summary>
        public long InsertInterval(ActivityInterval interval)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<long>(@"
                INSERT INTO intervals (start_utc, end_utc, process_name, window_title, url, is_idle, project_id)
                VALUES (@Start, @End, @Process, @Title, @Url, @IsIdle, @ProjectId);
                SELECT last_insert_rowid();",
                new
                {
                    Start = Database.FormatUtc(interval.StartUtc),
                    End = Database.FormatUtc(interval.EndUtc),
                    Process = interval.ProcessName,
                    Title = interval.WindowTitle,
                    interval.Url,
                    IsIdle = interval.IsIdle ? 1 : 0,
                    interval.ProjectId
                });
        }

        public void UpdateIntervalEnd(long id, DateTime endUtc, bool isIdle)
        {
            using var connection = Database.OpenConnection();
            connection.Execute(@"
                UPDATE intervals SET end_utc = @End, is_idle = @IsIdle WHERE id = @Id;",
                new { End = Database.FormatUtc(endUtc), IsIdle = isIdle ? 1 : 0, Id = id });
        }

        public void SetIntervalProject(long id, long? projectId)
        {
            using var connection = Database.OpenConnection();
            connection.Execute("UPDATE intervals SET project_id = @ProjectId WHERE id = @Id;",
                new { ProjectId = projectId, Id = id });
        }

        /// <summary>Интервалы, пересекающиеся с периодом [fromUtc; toUtc).</summary>
        public List<ActivityInterval> GetIntervals(DateTime fromUtc, DateTime toUtc)
        {
            using var connection = Database.OpenConnection();
            return connection.Query<ActivityInterval>(@"
                SELECT * FROM intervals
                WHERE start_utc < @To AND end_utc > @From
                ORDER BY start_utc;",
                new { From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) }).ToList();
        }

        public List<ActivityInterval> GetUnclassifiedIntervals(DateTime fromUtc, DateTime toUtc)
        {
            using var connection = Database.OpenConnection();
            return connection.Query<ActivityInterval>(@"
                SELECT * FROM intervals
                WHERE project_id IS NULL AND start_utc < @To AND end_utc > @From
                ORDER BY start_utc DESC
                LIMIT 500;",
                new { From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) }).ToList();
        }

        /// <summary>Суммарные секунды по проектам за период (idle-интервалы не считаются).</summary>
        public Dictionary<long, double> GetProjectTotals(DateTime fromUtc, DateTime toUtc, bool excludeIdle = true)
        {
            using var connection = Database.OpenConnection();
            var idleFilter = excludeIdle ? "AND is_idle = 0" : string.Empty;
            return connection.Query<(long ProjectId, double TotalSeconds)>($@"
                SELECT project_id AS ProjectId,
                       SUM((julianday(end_utc) - julianday(start_utc)) * 86400.0) AS TotalSeconds
                FROM intervals
                WHERE project_id IS NOT NULL AND start_utc < @To AND end_utc > @From {idleFilter}
                GROUP BY project_id;",
                new { From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) })
                .ToDictionary(r => r.ProjectId, r => r.TotalSeconds);
        }

        /// <summary>Суммарные секунды неклассифицированных (и idle) интервалов за период.</summary>
        public (double Unclassified, double Idle) GetUnclassifiedAndIdleTotals(DateTime fromUtc, DateTime toUtc)
        {
            using var connection = Database.OpenConnection();
            return connection.QuerySingle<(double Unclassified, double Idle)>(@"
                SELECT
                  IFNULL(SUM(CASE WHEN project_id IS NULL AND is_idle = 0
                       THEN (julianday(end_utc) - julianday(start_utc)) * 86400.0 END), 0) AS Unclassified,
                  IFNULL(SUM(CASE WHEN is_idle = 1
                       THEN (julianday(end_utc) - julianday(start_utc)) * 86400.0 END), 0) AS Idle
                FROM intervals
                WHERE start_utc < @To AND end_utc > @From;",
                new { From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) });
        }

        /// <summary>Дни, в которые есть интервалы, с суммой не-idle секунд (для списка дней).</summary>
        public List<(DateTime Date, double TotalSeconds)> GetDaySummaries()
        {
            using var connection = Database.OpenConnection();
            // Интервалы не пересекают локальную полночь, поэтому date(start_utc,'localtime') — точный день.
            var rows = connection.Query<(string Day, double TotalSeconds)>(@"
                SELECT date(start_utc, 'localtime') AS Day,
                       SUM((julianday(end_utc) - julianday(start_utc)) * 86400.0) AS TotalSeconds
                FROM intervals
                WHERE is_idle = 0
                GROUP BY Day
                ORDER BY Day DESC;");
            var result = new List<(DateTime, double)>();
            foreach (var row in rows)
            {
                if (DateTime.TryParse(row.Day, out var date))
                    result.Add((date.Date, row.TotalSeconds));
            }
            return result;
        }

        /// <summary>Конец последнего не-idle интервала проекта за период (NULL — активности не было).</summary>
        public DateTime? GetLastActivityEndUtc(long projectId, DateTime fromUtc, DateTime toUtc)
        {
            using var connection = Database.OpenConnection();
            var value = connection.ExecuteScalar<string?>(@"
                SELECT MAX(end_utc) FROM intervals
                WHERE project_id = @Id AND is_idle = 0 AND start_utc < @To AND end_utc > @From;",
                new { Id = projectId, From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) });
            if (string.IsNullOrEmpty(value))
                return null;
            return DateTime.SpecifyKind(DateTime.Parse(value), DateTimeKind.Utc);
        }

        /// <summary>Самый частый процесс проекта за период (для кнопки «Переключиться» в уведомлении).</summary>
        public string? GetMostFrequentProcess(long projectId, DateTime fromUtc, DateTime toUtc)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<string?>(@"
                SELECT process_name FROM intervals
                WHERE project_id = @Id AND is_idle = 0 AND start_utc < @To AND end_utc > @From
                GROUP BY process_name
                ORDER BY COUNT(*) DESC
                LIMIT 1;",
                new { Id = projectId, From = Database.FormatUtc(fromUtc), To = Database.FormatUtc(toUtc) });
        }
    }
}
