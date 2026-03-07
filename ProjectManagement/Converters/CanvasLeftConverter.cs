using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    /// <summary>
    /// Converts a segment's StartDate to a Canvas.Left position (double).
    /// Used for positioning timeline header segments in a Canvas.
    /// </summary>
    public class CanvasLeftConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Expected inputs:
                // 0: SegmentStartDate (DateTime)
                // 1: ViewStartDate (DateTime) - ProjectStartDate
                // 2: ViewEndDate (DateTime) - ProjectEndDate
                // 3: ContainerWidth (double) - TimelineWidth

                if (values.Length < 4 || !(values[3] is double containerWidth) || containerWidth == 0)
                    return 0.0;

                DateTime segmentStart = (DateTime)values[0];
                DateTime viewStart = (DateTime)values[1];
                DateTime viewEnd = (DateTime)values[2];

                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return 0.0;

                // Calculate offset from the view start
                double daysOffset = (segmentStart - viewStart).TotalDays;

                // Calculate pixels per day
                double pixelsPerDay = containerWidth / totalViewDays;

                // Return the left position
                double leftPosition = daysOffset * pixelsPerDay;
                return leftPosition < 0 ? 0.0 : leftPosition;
            }
            catch
            {
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
