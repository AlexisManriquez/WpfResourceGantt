using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class EvmMetricMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 1) return "0";

            double d = 0;
            if (values[0] is double dv) d = dv;
            else if (values[0] is decimal mv) d = (double)mv;
            else if (values[0] is float fv) d = (double)fv;
            else if (values[0] is int iv) d = (double)iv;
            else if (values[0] == null) return "--";

            // Second value is the DisplayMode
            string mode = "Dollars";
            if (values.Length > 1 && values[1] != null)
            {
                mode = values[1].ToString();
            }

            if (mode.Contains("Hours"))
            {
                return d.ToString("N1") + " h";
            }
            if (mode.Contains("Percent"))
            {
                return d.ToString("P1");
            }

            // Default to Dollars
            return d.ToString("C0");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
