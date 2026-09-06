namespace TimeTracker.ViewModels
{
    /// <summary>Элемент выбора типа правила классификации (ComboBox).</summary>
    public class RuleTypeItem
    {
        public RuleTypeItem(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }

        public static readonly RuleTypeItem[] All =
        {
            new("process", "Процесс (точное имя)"),
            new("title", "Заголовок окна (фрагмент)"),
            new("url", "URL (фрагмент)")
        };

        public override string ToString() => Display;
    }
}
