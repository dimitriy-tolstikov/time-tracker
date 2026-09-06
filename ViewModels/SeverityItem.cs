namespace TimeTracker.ViewModels
{
    /// <summary>Элемент выбора уровня серьёзности уведомления (ComboBox).</summary>
    public class SeverityItem
    {
        public SeverityItem(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }

        public static readonly SeverityItem[] All =
        {
            new("normal", "Белый"),
            new("warning", "Жёлтый"),
            new("critical", "Красный")
        };

        public override string ToString() => Display;
    }
}
