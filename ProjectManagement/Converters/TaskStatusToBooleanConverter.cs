using System;
using System.Globalization;
using System.Windows.Data;
using WpfResourceGantt.ProjectManagement.Models; // Ensure this matches your Enum namespace
using TaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus;
namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class TaskStatusToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                return status == TaskStatus.Completed;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
