using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class IndentationConverter : IValueConverter
    {
        public double Indentation { get; set; } = 20;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // Simply return the calculated width: Level 0 = 0, Level 1 = 20, etc.
                return (double)(level * Indentation);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
