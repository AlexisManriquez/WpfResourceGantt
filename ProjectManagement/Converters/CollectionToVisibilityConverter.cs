using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    /// <summary>
    /// Returns Visible if a collection has items, Collapsed if empty or null.
    /// Supports 'Inverted' parameter to return Visible only when empty.
    /// </summary>
    public class CollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasItems = false;

            if (value is IEnumerable enumerable)
            {
                // Check if there is at least one item
                var enumerator = enumerable.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    hasItems = true;
                }
            }

            bool isInverted = parameter?.ToString() == "Inverted";

            if (isInverted)
            {
                return hasItems ? Visibility.Collapsed : Visibility.Visible;
            }

            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
