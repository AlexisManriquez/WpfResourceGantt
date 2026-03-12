using System.Collections.Generic;
using System.Linq;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public static class WorkItemExtensions
    {
        /// <summary>
        /// Flattens a hierarchical tree of WorkItems into a single linear sequence.
        /// This allows searching all levels using standard LINQ.
        /// </summary>
        public static IEnumerable<WorkItem> Flatten(this IEnumerable<WorkItem> items)
        {
            if (items == null) yield break;

            foreach (var item in items)
            {
                // Return the parent first
                yield return item;

                // Then recursively return all its children
                if (item.Children != null && item.Children.Any())
                {
                    foreach (var child in item.Children.Flatten())
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
