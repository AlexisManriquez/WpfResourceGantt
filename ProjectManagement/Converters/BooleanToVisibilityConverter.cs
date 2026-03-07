using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Default to 'false' if the value is not a boolean.
            bool isVisible = false;
            if (value is bool b)
            {
                isVisible = b;
            }

            // Check if the converter needs to invert the result.
            bool shouldInvert = false;
            if (parameter is string str && (str.Equals("invert", StringComparison.OrdinalIgnoreCase) || str.Equals("Inverse", StringComparison.OrdinalIgnoreCase)))
            {
                shouldInvert = true;
            }

            // --- THIS IS THE CORRECTED LOGIC ---
            // It explicitly separates the inversion from the final decision.
            if (shouldInvert)
            {
                // If inverting, a 'true' value means Collapsed, and a 'false' value means Visible.
                return isVisible ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                // If not inverting, a 'true' value means Visible, and a 'false' value means Collapsed.
                return isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this functionality.
            throw new NotImplementedException();
        }
    }
}
