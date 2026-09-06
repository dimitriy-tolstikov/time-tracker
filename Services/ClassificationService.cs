using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    /// <summary>
    /// Классификация интервалов на проекты по правилам из БД.
    /// Правила кэшируются; после редактирования проектов/правил нужно вызвать <see cref="Reload"/>.
    /// </summary>
    public class ClassificationService
    {
        private readonly ProjectRepository _projects;
        private readonly IntervalRepository _intervals;

        private List<ClassificationRule> _rules = new();
        private readonly object _lock = new();

        public ClassificationService(ProjectRepository projects, IntervalRepository intervals)
        {
            _projects = projects;
            _intervals = intervals;
            Reload();
        }

        public void Reload()
        {
            lock (_lock)
            {
                _rules = _projects.GetRules()
                    .OrderByDescending(r => r.Priority)
                    .ToList();
            }
        }

        /// <summary>Первое совпадение по приоритету (убывание) побеждает; NULL — не классифицировано.</summary>
        public long? Classify(string processName, string windowTitle, string? url)
        {
            List<ClassificationRule> rules;
            lock (_lock)
                rules = _rules.ToList();

            foreach (var rule in rules)
            {
                if (Matches(rule, processName, windowTitle, url))
                    return rule.ProjectId;
            }
            return null;
        }

        /// <summary>
        /// Переклассификация существующих неклассифицированных интервалов за период
        /// (вызывается после создания/изменения правил). Возвращает число обновлённых.
        /// </summary>
        public int ReclassifyUnclassified(DateTime fromUtc, DateTime toUtc)
        {
            var unclassified = _intervals.GetUnclassifiedIntervals(fromUtc, toUtc);
            int updated = 0;
            foreach (var interval in unclassified)
            {
                var projectId = Classify(interval.ProcessName, interval.WindowTitle, interval.Url);
                if (projectId.HasValue)
                {
                    _intervals.SetIntervalProject(interval.Id, projectId.Value);
                    updated++;
                }
            }
            return updated;
        }

        private static bool Matches(ClassificationRule rule, string processName, string windowTitle, string? url)
        {
            switch (rule.RuleType)
            {
                case "process":
                    return string.Equals(processName, rule.Pattern, StringComparison.OrdinalIgnoreCase);
                case "title":
                    return MatchesText(rule, windowTitle);
                case "url":
                    return url != null && MatchesText(rule, url);
                default:
                    return false;
            }
        }

        private static bool MatchesText(ClassificationRule rule, string text)
        {
            if (rule.IsRegex)
            {
                try
                {
                    return Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    // Битый regex — считаем несовпадением, а не падаем.
                    return false;
                }
            }
            return text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
