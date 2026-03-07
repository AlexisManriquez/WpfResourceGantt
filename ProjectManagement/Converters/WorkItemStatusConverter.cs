using System;
using System.Globalization;
using System.Windows.Data;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    /// <summary>
    /// Converts WorkItemStatus enum to a human-readable display string and back.
    /// </summary>
    public class WorkItemStatusDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkItemStatus status)
            {
                return status switch
                {
                    WorkItemStatus.Active => "Active",
                    WorkItemStatus.OnHold => "On Hold",
                    WorkItemStatus.Future => "Future",
                    _ => status.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "Active" => WorkItemStatus.Active,
                    "On Hold" => WorkItemStatus.OnHold,
                    "Future" => WorkItemStatus.Future,
                    _ => WorkItemStatus.Active
                };
            }
            return WorkItemStatus.Active;
        }
    }
}
