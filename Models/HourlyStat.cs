namespace TimeTracker.Models
{
    public class HourlyStat
    {
        public int Hour { get; set; }
        public double WorkMinutes { get; set; }
        public double IdleMinutes { get; set; }
        public string HourLabel => $"{Hour:00}:00";
    }
}
