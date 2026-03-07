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
    public class AutoFitMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Expected inputs:
                // 0: TaskStartDate (DateTime)
                // 1: ViewStartDate (DateTime)
                // 2: ViewEndDate (DateTime)
                // 3: ContainerActualWidth (double)

                if (values.Length < 4 || !(values[3] is double containerWidth) || containerWidth == 0)
                    return new Thickness(0);

                DateTime taskStart = (DateTime)values[0];
                DateTime viewStart = (DateTime)values[1];
                DateTime viewEnd = (DateTime)values[2];

                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return new Thickness(0);

                // Calculate Offset
                double daysOffset = (taskStart - viewStart).TotalDays;

                // Calculate Pixels Per Day
                double pixelsPerDay = containerWidth / totalViewDays;

                double leftMargin = daysOffset * pixelsPerDay;
                return new Thickness(leftMargin, 0, 0, 0);
            }
            catch
            {
                return new Thickness(0);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
