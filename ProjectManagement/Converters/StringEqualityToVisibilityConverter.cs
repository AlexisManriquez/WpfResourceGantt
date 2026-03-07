using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class StringEqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string boundValue = value.ToString();
            string targetValue = parameter.ToString();

            // Returns Visible if the strings match, otherwise Collapsed
            return string.Equals(boundValue, targetValue, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
