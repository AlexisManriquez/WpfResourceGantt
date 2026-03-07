using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;

            string checkValue = value.ToString();
            string paramString = parameter.ToString();

            // Check if we are looking for the inverse
            bool invert = paramString.EndsWith(":Inverse", StringComparison.OrdinalIgnoreCase);
            string targetValue = invert ? paramString.Replace(":Inverse", "") : paramString;

            bool isMatch = checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);

            if (invert)
                return isMatch ? Visibility.Collapsed : Visibility.Visible;

            return isMatch ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
