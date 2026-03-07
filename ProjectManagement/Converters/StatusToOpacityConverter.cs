using System;
using System.Globalization;
using System.Windows.Data;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class StatusToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WpfResourceGantt.ProjectManagement.Models.TaskStatus status)
            {
                return status == WpfResourceGantt.ProjectManagement.Models.TaskStatus.Completed ? 0.3 : 1.0;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
