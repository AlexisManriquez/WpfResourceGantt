using System.Collections.Generic;

namespace WpfResourceGantt.ProjectManagement.Models
{
    /// <summary>
    /// Represents the current status of a work item.
    /// </summary>
    public enum WorkItemStatus
    {
        Active,
        OnHold,
        Future
    }

    /// <summary>
    /// Static helper to expose enum values for XAML binding.
    /// </summary>
    public static class WorkItemStatusHelper
    {
        public static List<WorkItemStatus> AllStatuses { get; } = new List<WorkItemStatus>
        {
            WorkItemStatus.Active,
            WorkItemStatus.OnHold,
            WorkItemStatus.Future
        };

        public static List<WorkItemType> AllWorkItemTypes { get; } = new List<WorkItemType>
        {
            WorkItemType.Leaf,
            WorkItemType.Receipt,
            WorkItemType.Milestone
        };
    }
}
