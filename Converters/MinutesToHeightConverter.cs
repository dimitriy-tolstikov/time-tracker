using System;
using System.Globalization;
using System.Windows.Data;

namespace TimeTracker.Converters
{
    public class MinutesToHeightConverter : IValueConverter
    {
        public double MaxHeight { get; set; } = 150;
        public double MaxMinutes { get; set; } = 60;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double minutes)
            {
                return Math.Min(minutes / MaxMinutes, 1.0) * MaxHeight;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
