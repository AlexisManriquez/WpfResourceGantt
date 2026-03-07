using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    public class LevelToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // Base offset of 30px + (24px expander/spacer) + (30px icon/margin) = 84px Name Start for Level 0.
                int baseIndent = 30 + level * 30;
                if (parameter?.ToString() == "Child")
                {
                    baseIndent += 30;
                }
                return new Thickness(baseIndent, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                bool isVisible = count > 0;
                if (parameter?.ToString() == "invert" || parameter?.ToString() == "Inverse")
                    isVisible = !isVisible;

                return isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class LevelToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                if (parameter?.ToString() == "Child") level++;

                return level switch
                {
                    0 => "🏢", // System
                    1 => "📁", // Project
                    2 => "📂", // Sub-Project
                    3 => "⛩️", // Gate
                    _ => "📄"  // Task / Sub-Task
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class LevelToCanAddChildConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // Systems (0) -> Projects (1)
                // Projects (1) -> Subprojects (2)
                // Subprojects (2) -> Gates (3)
                // Gates (3) -> Tasks (4)
                // Tasks (4) -> Sub-tasks (5)...
                // We allow adding children for any level < 5 to keep it reasonable but deep.
                return level < 5 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "▼" : "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
