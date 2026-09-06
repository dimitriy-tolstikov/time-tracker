using System.Collections.Generic;
using System.Linq;
using Dapper;
using TimeTracker.Models;

namespace TimeTracker.Services
{
    /// <summary>Проекты и правила классификации (Dapper).</summary>
    public class ProjectRepository
    {
        public List<Project> GetProjects()
        {
            using var connection = Database.OpenConnection();
            return connection.Query<Project>("SELECT * FROM projects ORDER BY name;").ToList();
        }

        public long AddProject(Project project)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<long>(@"
                INSERT INTO projects (name, color)
                VALUES (@Name, @Color);
                SELECT last_insert_rowid();",
                new { project.Name, project.Color });
        }

        public void UpdateProject(Project project)
        {
            using var connection = Database.OpenConnection();
            connection.Execute(@"
                UPDATE projects
                SET name = @Name, color = @Color
                WHERE id = @Id;",
                new { project.Name, project.Color, project.Id });
        }

        public void DeleteProject(long id)
        {
            using var connection = Database.OpenConnection();
            connection.Execute("DELETE FROM projects WHERE id = @Id;", new { Id = id });
        }

        public List<ClassificationRule> GetRules()
        {
            using var connection = Database.OpenConnection();
            return connection.Query<ClassificationRule>("SELECT * FROM rules ORDER BY priority DESC, id;").ToList();
        }

        public long AddRule(ClassificationRule rule)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<long>(@"
                INSERT INTO rules (project_id, rule_type, pattern, is_regex, priority)
                VALUES (@ProjectId, @RuleType, @Pattern, @IsRegex, @Priority);
                SELECT last_insert_rowid();",
                new { rule.ProjectId, rule.RuleType, rule.Pattern, IsRegex = rule.IsRegex ? 1 : 0, rule.Priority });
        }

        public void UpdateRule(ClassificationRule rule)
        {
            using var connection = Database.OpenConnection();
            connection.Execute(@"
                UPDATE rules
                SET project_id = @ProjectId, rule_type = @RuleType, pattern = @Pattern,
                    is_regex = @IsRegex, priority = @Priority
                WHERE id = @Id;",
                new { rule.ProjectId, rule.RuleType, rule.Pattern, IsRegex = rule.IsRegex ? 1 : 0, rule.Priority, rule.Id });
        }

        public void DeleteRule(long id)
        {
            using var connection = Database.OpenConnection();
            connection.Execute("DELETE FROM rules WHERE id = @Id;", new { Id = id });
        }

        public List<ProjectNotification> GetNotifications()
        {
            using var connection = Database.OpenConnection();
            return connection.Query<ProjectNotification>("SELECT * FROM notifications ORDER BY id;").ToList();
        }

        public long AddNotification(ProjectNotification notification)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<long>(@"
                INSERT INTO notifications (project_id, type, threshold_seconds, severity)
                VALUES (@ProjectId, @Type, @ThresholdSeconds, @Severity);
                SELECT last_insert_rowid();",
                new { notification.ProjectId, notification.Type, notification.ThresholdSeconds, notification.Severity });
        }

        public void UpdateNotification(ProjectNotification notification)
        {
            using var connection = Database.OpenConnection();
            connection.Execute(@"
                UPDATE notifications
                SET project_id = @ProjectId, type = @Type, threshold_seconds = @ThresholdSeconds,
                    severity = @Severity
                WHERE id = @Id;",
                new { notification.ProjectId, notification.Type, notification.ThresholdSeconds, notification.Severity, notification.Id });
        }

        public void DeleteNotification(long id)
        {
            using var connection = Database.OpenConnection();
            connection.Execute("DELETE FROM notifications WHERE id = @Id;", new { Id = id });
        }
    }
}
