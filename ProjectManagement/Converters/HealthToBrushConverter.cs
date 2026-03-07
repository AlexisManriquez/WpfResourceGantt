using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class HealthToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MetricStatus status)
            {
                switch (status)
                {
                    case MetricStatus.Good:
                        // Emerald 500 - Clear "Go" signal
                        return GetBrush("#10B981");

                    case MetricStatus.Warning:
                        // Amber 500 - Standard "Caution" signal (replaces Gold/Yellow)
                        return GetBrush("#F59E0B");

                    case MetricStatus.Bad:
                        // Red 500 - Authoritative "Critical" signal
                        return GetBrush("#EF4444");

                    default:
                        // Slate 400 - Unknown or Neutral status
                        return GetBrush("#94A3B8");
                }
            }
            return Brushes.Transparent; // Default
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter only works one-way, so we don't need to implement this.
            throw new NotImplementedException();
        }
        private static SolidColorBrush GetBrush(string hex) =>
    (SolidColorBrush)new BrushConverter().ConvertFrom(hex);
    }
}
