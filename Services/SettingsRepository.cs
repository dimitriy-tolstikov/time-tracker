using Dapper;

namespace TimeTracker.Services
{
    /// <summary>
    /// Настройки приложения в таблице settings (замена config.json).
    /// </summary>
    public class SettingsRepository
    {
        public const string InactivityTimeoutSecondsKey = "InactivityTimeoutSeconds";
        public const int DefaultInactivityTimeoutSeconds = 300;

        public string? GetString(string key)
        {
            using var connection = Database.OpenConnection();
            return connection.ExecuteScalar<string?>("SELECT value FROM settings WHERE key = @Key;", new { Key = key });
        }

        public int GetInt(string key, int defaultValue)
        {
            var value = GetString(key);
            return int.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public void SetString(string key, string value)
        {
            using var connection = Database.OpenConnection();
            connection.Execute(@"
                INSERT INTO settings (key, value) VALUES (@Key, @Value)
                ON CONFLICT(key) DO UPDATE SET value = @Value;",
                new { Key = key, Value = value });
        }

        public void SetInt(string key, int value) => SetString(key, value.ToString());
    }
}
