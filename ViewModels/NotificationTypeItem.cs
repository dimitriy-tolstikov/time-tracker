namespace TimeTracker.ViewModels
{
    /// <summary>Элемент выбора типа уведомления проекта (ComboBox).</summary>
    public class NotificationTypeItem
    {
        public NotificationTypeItem(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }

        public static readonly NotificationTypeItem[] All =
        {
            new("inactivity", "Нет активности"),
            new("daily_limit", "Дневной лимит")
        };

        public override string ToString() => Display;
    }
}
