using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class LevelToIndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Indent 20 pixels per level
            int level = (int)value;
            return new Thickness(level * 20, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
