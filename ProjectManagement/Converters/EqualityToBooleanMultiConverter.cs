using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class EqualityToBooleanMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] is the CurrentView (string)
            // values[1] is the CommandParameter (string)
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return false;

            return values[0].ToString() == values[1].ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // MultiBindings usually don't support back-conversion in this context
            throw new NotImplementedException();
        }
    }
}
