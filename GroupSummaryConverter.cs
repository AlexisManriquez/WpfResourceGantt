using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WpfResourceGantt;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt
{
    public class GroupSummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Value is the CollectionViewGroup (the Header Data Context)
            if (value is System.Windows.Data.CollectionViewGroup group)
            {
                // Get the list of people in this specific group/section
                var people = group.Items.OfType<ResourcePerson>().ToList();

                int total = people.Count;

                // 1. UNAVAILABLE (Busy or Overbooked)
                int unavailable = people.Count(p =>
                    p.OverallStatus == PersonRollupStatus.Busy ||
                    p.OverallStatus == PersonRollupStatus.Overbooked);

                // 2. UNASSIGNED (Has 0 Tasks)
                // Note: In your current logic, these people usually get moved to a specific 
                // "Unassigned Resources" group, but this counts them dynamically regardless.
                int unassigned = people.Count(p => !p.HasTasks);

                // 3. AVAILABLE (Everyone else: Secondary, OnHold, Free)
                // Logic: Total - Unavailable - Unassigned? 
                // Or strictly based on Status? Let's use strict Status for accuracy.
                int available = people.Count(p =>
                    p.OverallStatus == PersonRollupStatus.Assisting || // Secondary
                    p.OverallStatus == PersonRollupStatus.OnHold ||    // On Hold
                    p.OverallStatus == PersonRollupStatus.Free);       // Free (if not counted as unassigned above)

                // Format: "10 Total | 5 Busy | 3 Available | 2 Unassigned"
                return $"{total} Total · {unavailable} Busy · {available} Available · {unassigned} Unassigned";
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
