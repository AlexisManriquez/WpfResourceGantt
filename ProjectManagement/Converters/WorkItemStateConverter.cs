using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class WorkItemStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkBreakdownItem item)
            {
                // 1. Check for Overdue
                // If not complete (Progress < 1.0) AND EndDate is in the past
                if (item.Progress < 1.0 && item.EndDate.HasValue && item.EndDate.Value.Date < DateTime.Today)
                {
                    return "Overdue";
                }

                // 2. Check for Not Started
                if (item.Progress <= 0)
                {
                    return "NotStarted";
                }

                // 3. Check for Completed
                if (item.Progress >= 1.0)
                {
                    return "Completed";
                }

                // 4. Default
                return "InProgress";
            }
            return "Normal";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
