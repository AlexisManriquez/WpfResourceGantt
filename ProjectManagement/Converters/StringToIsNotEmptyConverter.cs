using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class StringToIsNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. If value is null, it's empty -> return False (Disabled)
            if (value == null)
                return false;

            // 2. If value is a string, check if it's empty/whitespace
            if (value is string text)
                return !string.IsNullOrWhiteSpace(text);

            // 3. If value is any other object (like FilterItem), it exists -> return True (Enabled)
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
