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
    public class AutoFitWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Inputs:
                // 0: StartDate
                // 1: EndDate
                // 2: ViewStartDate
                // 3: ViewEndDate
                // 4: ContainerActualWidth

                if (values.Length < 5 || !(values[4] is double containerWidth) || containerWidth == 0)
                    return 0.0;

                DateTime start = (DateTime)values[0];
                DateTime end = (DateTime)values[1];
                DateTime viewStart = (DateTime)values[2];
                DateTime viewEnd = (DateTime)values[3];

                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return 0.0;

                // --- THE FIX ---
                // Check if we want "Inclusive" mode (Task Bars, Headers)
                bool isInclusive = parameter as string == "Inclusive";

                // Calculate Duration
                double itemDuration;
                if (isInclusive)
                {
                    // Add 1 day so "Jan 1 to Jan 1" counts as 1 day width
                    itemDuration = (end.AddDays(1) - start).TotalDays;
                }
                else
                {
                    // Default: Exact math (For Rollup Segments which are already adjusted)
                    itemDuration = (end - start).TotalDays;
                }

                double pixelsPerDay = containerWidth / totalViewDays;
                double result = itemDuration * pixelsPerDay;

                return result < 0 ? 0 : result;
            }
            catch
            {
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }


}
