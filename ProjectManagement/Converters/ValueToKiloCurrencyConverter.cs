using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class ValueToKiloCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double d) return "$0";

            if (Math.Abs(d) < 1000)
            {
                // For values under 1000, show the exact currency with no decimals.
                return d.ToString("C0", culture);
            }
            else
            {
                // For values 1000 or over, divide by 1000 and show one decimal place with a 'k'.
                return $"{(d / 1000.0):C1}k".Replace(".0k", "k"); // C1 adds $, handles negatives. .Replace cleans up ".0"
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
