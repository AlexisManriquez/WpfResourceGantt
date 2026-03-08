using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class EvmDeltaColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.White;

            // Handle various numeric types coming from XAML bindings
            double delta = 0;
            try
            {
                delta = System.Convert.ToDouble(value);
            }
            catch { return Brushes.White; }

            string param = parameter?.ToString();
            bool inverted = param == "Inverted";
            bool isHighTcpiWarning = param == "HighTcpiWarning";

            if (isHighTcpiWarning)
            {
                // High TCPI ( > 1.10 ) or Infinity means high risk / budget consumed
                return (delta > 1.10 || double.IsInfinity(delta)) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Threshold for "neutral" (near zero)
            // Indices (SPI/CPI) are rounded to 2 places, so 0.005 threshold is safe.
            // Currency (SV/CV) can be large, but we only care about real movement.
            double threshold = 0.001; 
            
            if (Math.Abs(delta) < threshold)
            {
                return Application.Current.TryFindResource("TacticalGoldBrush") as Brush ?? Brushes.Gold;
            }

            // Color logic:
            // For SV, CV, SPI, CPI: Positive is Green (Good), Negative is Red (Bad)
            // For EAC: Positive is Red (Bad - cost increased), Negative is Green (Good - cost decreased)
            
            bool isPositiveGood = !inverted;

            if (delta > 0)
            {
                // Improvement if positiveGood, otherwise deterioration
                return isPositiveGood ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"))  // Green
                                     : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555")); // Red
            }
            else
            {
                // Deterioration if positiveGood (because delta is negative), otherwise improvement
                return isPositiveGood ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555")) // Red
                                     : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // Green
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
