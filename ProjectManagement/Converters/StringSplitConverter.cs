using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class StringSplitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string input)
            {
                return string.Empty;
            }

            // Find the index of the first space.
            int firstSpaceIndex = input.IndexOf(' ');

            // If a space is found and it's not the last character in the string...
            if (firstSpaceIndex > -1 && firstSpaceIndex < input.Length - 1)
            {
                string firstPart = input.Substring(0, firstSpaceIndex);
                string secondPart = input.Substring(firstSpaceIndex + 1);

                // Only strip if the first part looks like a code (contains '-' or is strictly numeric)
                if (firstPart.Contains("-") || double.TryParse(firstPart, out _))
                {
                    return secondPart;
                }
            }

            // If no space is found or it doesn't look like a code, return the original string.
            return input;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
