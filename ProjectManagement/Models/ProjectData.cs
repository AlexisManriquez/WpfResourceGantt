using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public enum WorkItemType { System, Summary, Leaf, Receipt, Milestone }

    public class TaskItems
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("actualFinishDate")]
        public DateTime? ActualFinishDate { get; set; }

        [JsonPropertyName("work")]
        public double Work { get; set; }

        [JsonPropertyName("actualWork")]
        public double ActualWork { get; set; }

        [JsonPropertyName("bcws")]
        public double? Bcws { get; set; }

        [JsonPropertyName("bcwp")]
        public double? Bcwp { get; set; }

        [JsonPropertyName("acwp")]
        public double? Acwp { get; set; }

        [JsonPropertyName("progress")]
        public double Progress { get; set; }

        [JsonPropertyName("assignedDeveloperId")]
        public string AssignedDeveloperId { get; set; }

        [JsonPropertyName("isMilestone")]
        public bool IsMilestone { get; set; }

        [JsonPropertyName("predecessors")]
        public List<string> Predecessors { get; set; }
    }
    public class SubProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("work")]
        public double Work { get; set; }

        [JsonPropertyName("actualWork")]
        public double ActualWork { get; set; }

        [JsonPropertyName("bac")]
        public decimal? BAC { get; set; } // Budget at Completion

        [JsonPropertyName("bcws")]
        public double? Bcws { get; set; }

        [JsonPropertyName("bcwp")]
        public double? Bcwp { get; set; }

        [JsonPropertyName("acwp")]
        public double? Acwp { get; set; }

        [JsonPropertyName("tasks")]
        public List<TaskItems> Tasks { get; set; } = new List<TaskItems>();


    }

    public class Project
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("work")]
        public double Work { get; set; }

        [JsonPropertyName("actualWork")]
        public double ActualWork { get; set; }

        [JsonPropertyName("bac")]
        public decimal? BAC { get; set; } // Budget at Completion


        [JsonPropertyName("bcws")]
        public double? Bcws { get; set; }

        [JsonPropertyName("bcwp")]
        public double? Bcwp { get; set; }

        [JsonPropertyName("acwp")]
        public double? Acwp { get; set; }

        [JsonPropertyName("subProjects")]
        public List<SubProject> SubProjects { get; set; } = new List<SubProject>();
    }

    public enum ScheduleMode
    {
        Dynamic, // Automated GAO Schedule Engine
        Manual   // Fixed Start/End dates, computed duration
    }

    public class SystemItem
    {
        [Timestamp]
        public byte[] RowVersion { get; set; }

        [NotMapped]
        [JsonIgnore]
        public bool IsDirty { get; set; }

        public int Sequence { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("wbsValue")]
        public string? WbsValue { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public WorkItemStatus Status { get; set; } = WorkItemStatus.Active;

        [JsonPropertyName("children")] // Changed from "projects"
        public List<WorkBreakdownItem> Children { get; set; } = new List<WorkBreakdownItem>();

        public void RecalculateRollup(DateTime? statusDate = null)
        {
            if (Children != null && Children.Any())
            {
                foreach (var child in Children)
                {
                    // Forward the simulation date to the WorkBreakdownItems
                    child.RecalculateRollup(statusDate);
                }
            }
        }
    }

    // This is the root object in your JSON file
    public class ProjectData
    {
        [JsonPropertyName("isEvmHoursBased")]
        public bool IsEvmHoursBased { get; set; }

        [JsonPropertyName("users")]
        public List<User> Users { get; set; } = new List<User>();

        [JsonPropertyName("systems")]
        public List<SystemItem> Systems { get; set; } = new List<SystemItem>();

        [JsonPropertyName("adminTasks")]
        public List<AdminTask> AdminTasks { get; set; } = new List<AdminTask>();
    }

    public class WorkBreakdownItem : ViewModelBase
    {
        [Timestamp]
        public byte[] RowVersion { get; set; }

        [NotMapped]
        [JsonIgnore]
        public bool IsDirty { get; set; }

        public const decimal HourlyRate = 195m;
        public WorkItemType ItemType { get; set; } = WorkItemType.Leaf;

        /// <summary>
        /// Quick-access boolean for XAML bindings and service checks.
        /// True when ItemType is Milestone.
        /// </summary>
        [NotMapped]
        [JsonIgnore]
        public bool IsMilestone => ItemType == WorkItemType.Milestone;

        /// <summary>
        /// Enforces milestone constraints: DurationDays = 0, StartDate == EndDate.
        /// Called when ItemType is set to Milestone.
        /// </summary>
        public void EnforceMilestoneConstraints()
        {
            if (!IsMilestone) return;
            DurationDays = 0;
            if (EndDate.HasValue)
                StartDate = EndDate;
            else if (StartDate.HasValue)
                EndDate = StartDate;
        }

        public int Sequence { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("isBaselined")]
        public bool IsBaselined { get; set; }
        public int Level { get; set; } // Add this to WorkBreakdownItem class

        [JsonPropertyName("scheduleMode")]
        public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Dynamic;

        [JsonPropertyName("wbsValue")]
        public string? WbsValue { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // --- NEW LOGIC-DRIVEN SCHEDULE FIELDS ---
        [JsonPropertyName("durationDays")]
        public int DurationDays { get; set; }

        [JsonPropertyName("predecessors")]
        public string? Predecessors { get; set; } // e.g. "1.1, 1.2"

        [JsonPropertyName("startNoEarlierThan")]
        public DateTime? StartNoEarlierThan { get; set; }

        // --- PHASE 3 ENGINE COMPUTED FIELDS ---
        public bool IsCritical { get; set; }
        public int TotalFloat { get; set; }
        public DateTime? LateStart { get; set; }
        public DateTime? LateFinish { get; set; }
        public bool IsOverAllocated { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("baselineStartDate")]
        public DateTime? BaselineStartDate { get; set; }

        [JsonPropertyName("baselineEndDate")]
        public DateTime? BaselineEndDate { get; set; }

        [JsonPropertyName("work")]
        public double? Work { get; set; }

        [JsonPropertyName("actualWork")]
        public double? ActualWork { get; set; }

        [JsonPropertyName("actualFinishDate")]
        public DateTime? ActualFinishDate { get; set; }

        private double _progress;
        [JsonPropertyName("progress")]
        public double Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged(); // This is the "broadcast" to the UI
                }
            }
        }

        // --- EVM Data ---
        [JsonPropertyName("bac")]
        public decimal? BAC { get; set; } // Budget at Completion
        [JsonPropertyName("bcws")]
        public double? Bcws { get; set; }

        [JsonPropertyName("bcwp")]
        public double? Bcwp { get; set; }

        [JsonPropertyName("acwp")]
        public double? Acwp { get; set; }

        // --- SMTS Import Audit Trail ---
        // Tracks the origin of ACWP data. Set by CsvImportService only.
        // Never set manually. Gives PMs full traceability for customer reports.
        [JsonPropertyName("lastAcwpImportDate")]
        public DateTime? LastAcwpImportDate { get; set; }

        [JsonPropertyName("lastAcwpImportSource")]
        public string? LastAcwpImportSource { get; set; }

        // --- Developer Assignment (only for leaf nodes) ---
        [JsonPropertyName("assignedDeveloperId")]
        public string? AssignedDeveloperId { get; set; }

        // --- NEW PROPERTY ---
        [JsonPropertyName("progressHistory")]
        public List<ProgressHistoryItem> ProgressHistory { get; set; } = new List<ProgressHistoryItem>();

        // --- NEW PROPERTY ---
        [JsonPropertyName("assignments")]
        public List<ResourceAssignment> Assignments { get; set; } = new List<ResourceAssignment>();


        [JsonPropertyName("status")]
        public WorkItemStatus Status { get; set; } = WorkItemStatus.Active;

        // --- THE RECURSIVE PROPERTY ---
        // This allows for infinite nesting.
        [JsonPropertyName("children")]
        public List<WorkBreakdownItem> Children { get; set; } = new List<WorkBreakdownItem>();
        [JsonPropertyName("progressBlocks")]
        public List<ProgressBlock> ProgressBlocks { get; set; } = new List<ProgressBlock>();
        private bool _isTestBlocksExpanded;
        [NotMapped]
        [JsonIgnore] // We don't need to save this to the database, just keep it in memory
        public bool IsTestBlocksExpanded
        {
            get => _isTestBlocksExpanded;
            set { _isTestBlocksExpanded = value; OnPropertyChanged(); }
        }
        public void RecalculateRollup(DateTime? statusDate = null)
        {
            DateTime effectiveDate = statusDate ?? DateTime.Today;

            // MILESTONE ENFORCEMENT: Milestones are zero-duration, point-in-time items.
            // They never contribute to parent rollup and never generate EVM metrics.
            if (IsMilestone)
            {
                EnforceMilestoneConstraints();
                // Milestones produce zero PV / EV / AC
                Work = 0;
                BAC = 0;
                Bcws = 0;
                Bcwp = 0;
                Acwp = 0;
                return;
            }

            if (Children != null && Children.Any())
            {
                // --- SUMMARY LOGIC (Rollup from Children) ---
                foreach (var child in Children)
                {
                    child.RecalculateRollup(effectiveDate);
                }

                // MILESTONE EXCLUSION: Filter out milestone children from timeline and EVM rollup.
                // Milestones are point-in-time markers and must not expand parent date bounds
                // or contribute to financial/resource aggregation.
                var rollupChildren = Children.Where(c => !c.IsMilestone).ToList();

                // 2.Summary Logic: Include current Parent dates in the comparison
                // to avoid "shrinking" imported buffers.
                DateTime childrenMin = rollupChildren.Where(c => c.StartDate.HasValue)
                                              .Select(c => c.StartDate.Value)
                                              .DefaultIfEmpty(this.StartDate ?? DateTime.Today).Min();

                DateTime childrenMax = rollupChildren.Where(c => c.EndDate.HasValue)
                                              .Select(c => c.EndDate.Value)
                                              .DefaultIfEmpty(this.EndDate ?? DateTime.Today).Max();

                // 2. Strict Rollup: Parent takes the exact bounds of non-milestone children
                if (childrenMin != default(DateTime)) StartDate = childrenMin;
                if (childrenMax != default(DateTime)) EndDate = childrenMax;

                // 3. FIX: Calculate Parent Duration based on these new dates
                if (StartDate.HasValue && EndDate.HasValue)
                {
                    DurationDays = GetBusinessDaysSpan(StartDate.Value, EndDate.Value);
                }

                // Parent Duration = Sum of non-milestone child values
                Work = Math.Round(rollupChildren.Sum(c => c.Work ?? 0), 2);
                ActualWork = Math.Round(rollupChildren.Sum(c => c.ActualWork ?? 0), 2);
                BAC = Math.Round(rollupChildren.Sum(c => c.BAC ?? 0), 2);
                Bcws = Math.Round(rollupChildren.Sum(c => c.Bcws ?? 0), 2);
                Bcwp = Math.Round(rollupChildren.Sum(c => c.Bcwp ?? 0), 2);
                Acwp = Math.Round(rollupChildren.Sum(c => c.Acwp ?? 0), 2);

                // Progress (Weighted by BAC)
                decimal totalBac = BAC ?? 0;
                if (totalBac > 0)
                    Progress = (double)((decimal)(Bcwp ?? 0) / totalBac);
                else if (rollupChildren.Any())
                    Progress = rollupChildren.Average(c => c.Progress);
                else
                    Progress = 0;
            }
            else
            {
                if (StartDate.HasValue && DurationDays > 0)
                {
                    // Ensure the model uses the same inclusive logic
                    EndDate = AddBusinessDays(StartDate.Value, DurationDays);
                }
                // LEAF LOGIC
                if (!IsBaselined && (Work ?? 0) > 0)
                {
                    BAC = (decimal)Work.Value * HourlyRate;
                }

                if (StartDate.HasValue && EndDate.HasValue && BAC.HasValue && BAC.Value > 0)
                {
                    // DoD/MS Project Standard: Inclusive Business Days
                    int totalWorkingDays = GetBusinessDaysSpan(StartDate.Value, EndDate.Value);
                    int daysElapsed = GetBusinessDaysSpan(StartDate.Value, effectiveDate);

                    if (effectiveDate < StartDate.Value)
                        Bcws = 0;
                    else if (effectiveDate > EndDate.Value)
                        Bcws = (double)BAC.Value;
                    else
                    {
                        double plannedPercent = totalWorkingDays > 0 ? (double)daysElapsed / totalWorkingDays : 0;
                        Bcws = Math.Round((double)BAC.Value * plannedPercent, 2);
                    }
                }

                // Round Acwp and Bcwp to ensure clean math
                Bcwp = Math.Round((double)(BAC ?? 0) * Progress, 2);
                Acwp = Math.Round(Acwp ?? 0, 2);
            }
        }

        public static DateTime AddBusinessDays(DateTime start, int days)
        {
            // A 1-day task starts and ends on the same day.
            // So we add (days - 1) to the start date.
            if (days <= 0) return start;

            DateTime result = start;
            int remaining = days - 1; // Subtract 1 to make it inclusive

            while (remaining > 0)
            {
                result = result.AddDays(1);
                if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday)
                    remaining--;
            }
            return result;
        }
        public static DateTime EnsureBusinessDay(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday)
                return date.AddDays(2); // Move to Monday
            if (date.DayOfWeek == DayOfWeek.Sunday)
                return date.AddDays(1); // Move to Monday
            return date;
        }
        public static int GetBusinessDaysSpan(DateTime start, DateTime end)
        {
            int count = 0;
            // FIX: Make the loop inclusive (<=) to match MS Project's duration calculation
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    count++;
            }
            return count;
        }
    }
}
