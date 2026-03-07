using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;
using WpfResourceGantt.ProjectManagement.Models;
using System.Collections.ObjectModel;

namespace WpfResourceGantt.ProjectManagement.Features.ResourceGantt
{
    // Calculates the Width of a task bar or header segment
    public class AutoFitWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 5 || !(values[4] is double containerWidth) || containerWidth == 0)
                    return 0.0;

                DateTime start = (DateTime)values[0];
                DateTime end = (DateTime)values[1];
                DateTime viewStart = (DateTime)values[2];
                DateTime viewEnd = (DateTime)values[3];

                // Denominator must be inclusive to match the item duration logic
                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return 0.0;

                bool isInclusive = parameter as string == "Inclusive";
                double itemDuration;

                if (isInclusive)
                    itemDuration = (end.AddDays(1) - start).TotalDays;
                else
                    itemDuration = (end - start).TotalDays;

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

    // Calculates the Left Margin (Offset) of a task bar
    public class AutoFitMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 4 || !(values[3] is double containerWidth) || containerWidth == 0)
                    return new Thickness(0);

                DateTime taskStart = (DateTime)values[0];
                DateTime viewStart = (DateTime)values[1];
                DateTime viewEnd = (DateTime)values[2];

                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return new Thickness(0);

                double daysOffset = (taskStart - viewStart).TotalDays;
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

    public class GanttBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // New/Restored: Checks if a date is within the view range
    public class DateInRangeToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 3) return Visibility.Collapsed;
                if (values[0] is DateTime date && values[1] is DateTime start && values[2] is DateTime end)
                {
                    if (date >= start && date <= end) return Visibility.Visible;
                }
                return Visibility.Collapsed;
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // New/Restored: Summarizes Group Info (e.g. "5 Members")
    public class GroupSummaryMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // value[0] = Group Name (Section Name)
            // value[1] = All Resources Collection
            try
            {
                if (values.Length < 2) return "";
                string groupName = values[0] as string;
                var resources = values[1] as IEnumerable<ResourcePerson>;

                if (resources == null) return "";

                // Logic: Count people in this section group
                // Note: The grouping logic in ViewModel uses "SectionGroupKey" which might be same as Name.
                if (string.IsNullOrEmpty(groupName)) return "";

                int count = resources.Count(r => r.SectionGroupKey == groupName);
                return $"{count} Members";
            }
            catch
            {
                return "";
            }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Helper for Sticky Text positioning
    public class StickyTextMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Logic to keep text visible even if start is scrolled off? 
            // Or just simple margin.
            // Original likely referenced AutoFitMargin logic.
            // We'll map it to AutoFitMargin for now as a fallback or implement similar.
            // Wait, Sticky Text implies it stays in view. 
            // For now, let's just use the same logic as Margin.
            try
            {
                if (values.Length < 4 || !(values[3] is double containerWidth) || containerWidth == 0)
                    return new Thickness(5, 0, 0, 0);

                DateTime taskStart = (DateTime)values[0];
                DateTime viewStart = (DateTime)values[1];
                DateTime viewEnd = (DateTime)values[2];

                // If task starts before view, clamp it?
                // This is complex. Let's assume standard margin + small buffer.
                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;
                if (totalViewDays <= 0) return new Thickness(0);

                double daysOffset = (taskStart - viewStart).TotalDays;
                double pixelsPerDay = containerWidth / totalViewDays;
                double leftMargin = daysOffset * pixelsPerDay;

                // If leftMargin < 0 (started before view), we might want to clamp to 0 
                // IF we want "Sticky" behavior.
                if (leftMargin < 0) leftMargin = 5; // A simple 'Sticky' effect

                return new Thickness(leftMargin, 0, 0, 0);

            }
            catch { return new Thickness(0); }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RelativeSegmentMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Calculates margin of a segment RELATIVE to the Rollup Bar Container
            // Start of segment, Start of Rollup, ViewStart, ViewEnd, Width
            try
            {
                if (values.Length < 5 || !(values[4] is double containerWidth) || containerWidth == 0)
                    return new Thickness(0);

                DateTime segStart = (DateTime)values[0];
                DateTime rollupStart = (DateTime)values[1]; // The container starts here

                // We need pixels per day
                DateTime viewStart = (DateTime)values[2];
                DateTime viewEnd = (DateTime)values[3];
                double totalViewDays = (viewEnd.AddDays(1) - viewStart).TotalDays;

                double pixelsPerDay = containerWidth / totalViewDays;

                // Offset from Rollup Start
                double daysFromRollupStart = (segStart - rollupStart).TotalDays;

                return new Thickness(daysFromRollupStart * pixelsPerDay, 0, 0, 0);
            }
            catch { return new Thickness(0); }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TodayLineLeftConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Values: [0]=TimelineWidth, [1]=ViewStartDate, [2]=ViewEndDate
                if (values.Length < 3 || !(values[0] is double width) || width <= 0) return 0.0;
                if (!(values[1] is DateTime start) || !(values[2] is DateTime end)) return 0.0;

                double totalDays = (end.AddDays(1) - start).TotalDays;
                if (totalDays <= 0) return 0.0;

                double daysFromStart = (DateTime.Now - start).TotalDays;
                double pixelsPerDay = width / totalDays;

                return Math.Max(0, daysFromStart * pixelsPerDay);
            }
            catch { return 0.0; }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
