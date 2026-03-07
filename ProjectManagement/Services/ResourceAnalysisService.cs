using System;
using System.Collections.Generic;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public class ResourceAnalysisService : IResourceAnalysisService
    {
        public void AnalyzeResources(IEnumerable<SystemItem> systems, IEnumerable<User> users)
        {
            var leafItems = GetAllLeafItems(systems).ToList();
            if (!leafItems.Any()) return;

            // 1. Reset status
            foreach (var item in leafItems)
            {
                item.IsOverAllocated = false;
            }

            // 2. Process each user
            foreach (var user in users)
            {
                // Find items assigned to this user
                // Check both AssignedDeveloperId (Main) and Assignments list (Support)
                var userItems = leafItems.Where(i =>
                    (i.AssignedDeveloperId == user.Id ||
                     (i.Assignments != null && i.Assignments.Any(a => a.DeveloperId == user.Id)))
                    && i.StartDate.HasValue && i.EndDate.HasValue).ToList();

                if (userItems.Count < 2) continue;

                // Daily capacity
                double dailyCapacity = user.WeeklyCapacity / 5.0;

                // Determine project date range for this user
                var minDate = userItems.Min(i => i.StartDate.Value);
                var maxDate = userItems.Max(i => i.EndDate.Value);

                // Check each business day
                for (var date = minDate.Date; date <= maxDate.Date; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;

                    double totalDemand = 0;
                    var activeItemsAtDate = new List<WorkBreakdownItem>();

                    foreach (var item in userItems)
                    {
                        if (date >= item.StartDate.Value.Date && date <= item.EndDate.Value.Date)
                        {
                            int duration = WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate.Value, item.EndDate.Value);
                            if (duration <= 0) duration = 1;

                            // If Work is specified (hours), divide by duration.
                            // If not specified, assume 8h/day (full commitment).
                            double itemDailyDemand = (item.Work ?? (duration * 8.0)) / duration;

                            totalDemand += itemDailyDemand;
                            activeItemsAtDate.Add(item);
                        }
                    }

                    if (totalDemand > dailyCapacity + 0.1) // 0.1h margin
                    {
                        foreach (var item in activeItemsAtDate)
                        {
                            item.IsOverAllocated = true;
                        }
                    }
                }
            }
        }

        private IEnumerable<WorkBreakdownItem> GetAllLeafItems(IEnumerable<SystemItem> systems)
        {
            var results = new List<WorkBreakdownItem>();
            foreach (var sys in systems)
            {
                if (sys.Children == null) continue;
                foreach (var child in sys.Children)
                {
                    results.AddRange(GetLeaves(child));
                }
            }
            return results;
        }

        private IEnumerable<WorkBreakdownItem> GetLeaves(WorkBreakdownItem item)
        {
            if (item.Children == null || !item.Children.Any())
            {
                yield return item;
            }
            else
            {
                foreach (var child in item.Children)
                {
                    foreach (var leaf in GetLeaves(child))
                    {
                        yield return leaf;
                    }
                }
            }
        }
    }
}
