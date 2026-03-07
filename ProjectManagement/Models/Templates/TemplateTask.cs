using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Models.Templates
{
    /// <summary>
    /// Represents a leaf-level task within a Template Gate.
    /// When a gate has TemplateTasks, it becomes a Summary Node
    /// and its tasks become individually-schedulable WorkBreakdownItems.
    /// 
    /// GAO Best Practice: "Does another task depend on this item finishing?"
    ///   YES → Make it a TemplateTask (becomes a Leaf WorkBreakdownItem).
    ///   NO  → Keep it as a TemplateProgressItem (checklist).
    /// </summary>
    public class TemplateTask
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The name of the leaf task (e.g., "Software Development", "Hardware Delivery").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Sort order within the parent gate.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Default duration in business days.
        /// For Receipt/waiting tasks this is the vendor lead time.
        /// </summary>
        public int DurationDays { get; set; }

        /// <summary>
        /// Default work hours. Receipt tasks should be 0.
        /// </summary>
        public double WorkHours { get; set; }

        /// <summary>
        /// The type of task: Leaf (normal work) or Receipt (external dependency, 0 effort).
        /// </summary>
        public WorkItemType ItemType { get; set; } = WorkItemType.Leaf;

        /// <summary>
        /// Relative predecessors within this template, by SortOrder index.
        /// Format: "GateSortOrder.TaskSortOrder" (e.g., "0.0" = Gate 0, Task 0).
        /// Multiple can be separated by commas: "1.0, 1.1, 1.2".
        /// When applied, TemplateService maps these to actual WorkBreakdownItem IDs.
        /// </summary>
        public string? Predecessors { get; set; }

        // === Navigation Properties ===

        [ForeignKey("TemplateGate")]
        public int TemplateGateId { get; set; }
        public virtual TemplateGate TemplateGate { get; set; }

        /// <summary>
        /// Optional: ProgressBlocks attached directly to this leaf task.
        /// These are the true "checklist" items for this specific task.
        /// </summary>
        public virtual ICollection<TemplateProgressBlock> Blocks { get; set; } = new List<TemplateProgressBlock>();
    }
}
