using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt
{
    public class GroupSummaryMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Expected Inputs:
            // 0: Group Name (string) - e.g., "Section B" or "Unassigned Resources"
            // 1: Full Resource List (IEnumerable<ResourcePerson>)

            if (values.Length < 2 || values[0] == null || values[1] == null)
                return "";

            string groupName = values[0].ToString();
            var allResources = values[1] as IEnumerable<ResourcePerson>;

            if (allResources == null) return "";

            // 1. HIDE STATS FOR "UNASSIGNED RESOURCES" GROUP
            if (groupName == "Unassigned Resources")
            {
                return ""; // Return empty string to hide the text
            }

            // 2. EXTRACT SECTION CODE
            // The Group Name is format "Section {Code}". We need just "{Code}".
            string sectionCode = groupName.Replace("Section ", "").Trim();

            // 3. GET TRUE TOTALS FOR THIS SECTION
            // Find everyone who belongs to this section, regardless of what group they are visually in right now.
            var sectionMembers = allResources
                .Where(r => string.Equals(r.Section, sectionCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sectionMembers.Count == 0) return "";

            int total = sectionMembers.Count;

            // Busy: Has Primary InWork tasks
            int busy = sectionMembers.Count(p => p.OverallStatus == PersonRollupStatus.Busy || p.OverallStatus == PersonRollupStatus.Overbooked);

            // Unassigned: People in this section who have NO visible tasks right now
            // (These are the people visually sitting in the "Unassigned Resources" group)
            int unassigned = sectionMembers.Count(p => !p.HasTasksInCurrentView);

            // Available: Everyone else (Secondary, OnHold, or Free)
            // Logic: Total - Busy
            int available = total - busy;

            return $"{total} Total · {busy} Busy · {available} Available · {unassigned} Unassigned";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
