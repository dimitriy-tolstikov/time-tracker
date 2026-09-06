using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TimeTracker.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>true — поменять видимость на противоположную.</summary>
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (Invert)
                flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is Visibility v && v == Visibility.Visible;
            return Invert ? !flag : flag;
        }
    }
}
