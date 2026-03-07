using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Data;
using WpfResourceGantt;

namespace WpfResourceGantt.ProjectManagement.Models
{
    // --- Enums ---
    public enum TaskStatus { NotStarted, InWork, OnHold, Future, Completed }


    public enum SegmentStatus { Busy, Available }

    public enum PersonRollupStatus
    {
        Free,
        OnHold,
        Assisting,
        Busy,
        Overbooked
    }

    // --- Models ---

    public class PowerBiSnapshot
    {
        public string Name { get; set; }
        public string Section { get; set; }
        public string Role { get; set; }

        // 1 = Busy, 0 = Available
        public int BusyToday { get; set; }
        public int BusyAt30Days { get; set; }
        public int BusyAt60Days { get; set; }
        public int BusyAt90Days { get; set; }
        public int BusyAt180Days { get; set; }
        public int BusyAt360Days { get; set; }
    }

    public class RollupSegment
    {
        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime Start { get; set; }

        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime End { get; set; }
        public SegmentStatus Status { get; set; }
    }

    public class AvailabilityAnalytics
    {
        public int AvailableNow { get; set; }
        public int AvailableIn30Days { get; set; }
        public int AvailableIn60Days { get; set; }
        public int AvailableIn90Days { get; set; }
        public int AvailableIn150Days { get; set; }
        public int AvailableIn180Days { get; set; }

        // Optional: Timestamp to know when this was calculated
        public string GeneratedAt { get; set; } = DateTime.Now.ToString("g");
    }

    // The new Root Object for the JSON file (kept for backward compatibility during migration)
    public class ProjectRootData
    {
        public AvailabilityAnalytics Analytics { get; set; }
        public ObservableCollection<ResourcePerson> Resources { get; set; }

        public List<ResourceTask> UnassignedBacklog { get; set; }

        public List<PowerBiSnapshot> ReportingSnapshots { get; set; }
    }

    public class TimelineSegment
    {
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        // Helper for UI Grid Column Spanning or Width
        // The View logic will handle display, this is just the data bucket.
    }

    // Simple class for the "Users" section in JSON
    public class UserDefinition
    {
        public string Name { get; set; }
        public string Section { get; set; } // Maps to "Office Symbol"
        public string Role { get; set; }    // Maps to "Role"
    }

    public class ResourceTask : INotifyPropertyChanged
    {
        private string _projectName;
        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private DateTime _startDate;
        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate;
        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private TaskStatus _status;
        public TaskStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        // NEW PROPERTY: Column 2
        private string _projectOfficeSymbol;
        public string ProjectOfficeSymbol
        {
            get => _projectOfficeSymbol;
            set { _projectOfficeSymbol = value; OnPropertyChanged(); }
        }

        // NEW PROPERTY: Column 6
        private string _resourceOfficeSymbol;
        public string ResourceOfficeSymbol
        {
            get => _resourceOfficeSymbol;
            set { _resourceOfficeSymbol = value; OnPropertyChanged(); }
        }

        private AssignmentRole _assignmentRole = AssignmentRole.Primary;
        public AssignmentRole AssignmentRole
        {
            get => _assignmentRole;
            set { _assignmentRole = value; OnPropertyChanged(); }
        }

        private bool _isVisibleInView = true;
        public bool IsVisibleInView
        {
            get => _isVisibleInView;
            set { _isVisibleInView = value; OnPropertyChanged(); }
        }

        // Helper for UI Color
        public bool IsInWork => Status == TaskStatus.InWork;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void UpdateVisibility(DateTime viewStart, DateTime viewEnd)
        {
            // Visible if the task overlaps the view
            IsVisibleInView = (EndDate >= viewStart && StartDate <= viewEnd);
        }
    }

    public class ResourcePerson : INotifyPropertyChanged
    {
        // --- BASIC INFO ---
        public string Name { get; set; }

        // Section is used for Grouping
        private string _section;
        public string Section
        {
            get => _section;
            set { _section = value; OnPropertyChanged(); OnPropertyChanged(nameof(SectionGroupKey)); }
        }

        // Role is used for the Badge (DEV, PM, etc.)
        private string _role;
        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        private ObservableCollection<RollupSegment> _rollupSegments;
        public ObservableCollection<RollupSegment> RollupSegments
        {
            get => _rollupSegments;
            set { _rollupSegments = value; OnPropertyChanged(nameof(RollupSegments)); }
        }
        public string Initials => string.IsNullOrWhiteSpace(Name) ? "?" : string.Join("", Name.Split(' ').Select(x => x[0]));

        // --- TASK LIST ---
        private ObservableCollection<ResourceTask> _tasks = new ObservableCollection<ResourceTask>();
        public ObservableCollection<ResourceTask> Tasks
        {
            get => _tasks;
            set
            {
                if (_tasks != value)
                {
                    if (_tasks != null)
                    {
                        _tasks.CollectionChanged -= Tasks_CollectionChanged;
                        foreach (var task in _tasks) task.PropertyChanged -= Task_PropertyChanged;
                    }
                    _tasks = value;
                    if (_tasks != null)
                    {
                        _tasks.CollectionChanged += Tasks_CollectionChanged;
                        foreach (var task in _tasks) task.PropertyChanged += Task_PropertyChanged;
                    }
                    OnPropertyChanged(nameof(Tasks));
                    RefreshRollups();
                }
            }
        }

        public string RollupSummaryText
        {
            get
            {
                // Filter: Only look at tasks visible in the current view
                var visibleTasks = Tasks.Where(t => t.IsVisibleInView).ToList();

                if (!visibleTasks.Any()) return "";

                int primary = visibleTasks.Count(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary);
                int secondary = visibleTasks.Count(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Secondary);
                int onHold = visibleTasks.Count(t => t.Status == TaskStatus.OnHold);

                var parts = new List<string>();
                if (primary > 0) parts.Add($"{primary} Critical");
                if (secondary > 0) parts.Add($"{secondary} Non-Critical");
                if (onHold > 0) parts.Add($"{onHold} Hold");

                return string.Join(" · ", parts);
            }
        }

        public PersonRollupStatus OverallStatus
        {
            get
            {
                // Filter visible tasks first (respecting the view range)
                var visibleTasks = Tasks.Where(t => t.IsVisibleInView).ToList();

                if (!visibleTasks.Any()) return PersonRollupStatus.Free; // No visible tasks = Free (Green)

                // Check for Capacity Consumption (Primary + InWork)
                bool isBusy = visibleTasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary);

                if (isBusy)
                {
                    // If they have ANY primary work, they are Busy.
                    return PersonRollupStatus.Busy;
                }
                else
                {
                    // If they have tasks (Secondary or OnHold) but none are Primary/InWork, 
                    // they are technically "Available" to take a lead role.
                    // We map this to 'Assisting' enum value but will style it GREEN.
                    return PersonRollupStatus.Assisting;
                }
            }
        }
        private bool _hasTasksInCurrentView;
        public bool HasTasksInCurrentView
        {
            get => _hasTasksInCurrentView;
            set
            {
                if (_hasTasksInCurrentView != value)
                {
                    _hasTasksInCurrentView = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SectionGroupKey)); // Trigger re-grouping
                }
            }
        }
        // --- CONSTRUCTOR & EVENTS ---
        public ResourcePerson()
        {
            Tasks.CollectionChanged += Tasks_CollectionChanged;
        }

        private void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (ResourceTask item in e.NewItems) item.PropertyChanged += Task_PropertyChanged;
            if (e.OldItems != null) foreach (ResourceTask item in e.OldItems) item.PropertyChanged -= Task_PropertyChanged;
            RefreshRollups();
        }

        private void Task_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResourceTask.StartDate) ||
                e.PropertyName == nameof(ResourceTask.EndDate) ||
                e.PropertyName == nameof(ResourceTask.Status) ||
                e.PropertyName == nameof(ResourceTask.AssignmentRole))
            {
                RefreshRollups();
            }
        }

        public void RefreshViewVisibility(DateTime viewStart, DateTime viewEnd)
        {
            bool anyVisible = false;

            foreach (var task in Tasks)
            {
                task.UpdateVisibility(viewStart, viewEnd);
                if (task.IsVisibleInView) anyVisible = true;
            }

            // Update the flag used for "Unassigned" grouping
            HasTasksInCurrentView = anyVisible;

            // Trigger updates for Text and Color
            OnPropertyChanged(nameof(RollupSummaryText));
            OnPropertyChanged(nameof(OverallStatus));
        }
        private void RefreshRollups()
        {
            OnPropertyChanged(nameof(RollupStartDate));
            OnPropertyChanged(nameof(RollupEndDate));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(AvailabilityDate));
            OnPropertyChanged(nameof(SectionGroupKey));
            OnPropertyChanged(nameof(RollupSummaryText));
            CalculateRollupSegments();
            OnPropertyChanged(nameof(OverallStatus));

        }

        // --- CALCULATED PROPERTIES ---
        // Only Busy if InWork AND Primary
        public bool IsBusy => Tasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary);
        public bool HasTasks => Tasks.Any();


        public string SectionGroupKey
        {
            get
            {
                if (!HasTasks) return "Unassigned Resources";
                if (!HasTasksInCurrentView) return "Unassigned Resources";

                return string.IsNullOrWhiteSpace(Section) ? "General" : $"Section {Section}";
            }
        }

        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime RollupStartDate => Tasks.Any() ? Tasks.Min(t => t.StartDate).Date : DateTime.MinValue;

        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime RollupEndDate => Tasks.Any() ? Tasks.Max(t => t.EndDate).Date : DateTime.MinValue;

        [JsonConverter(typeof(CustomDateConverter))]
        public DateTime AvailabilityDate
        {
            get
            {
                var activePrimaryTasks = Tasks.Where(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary).ToList();
                if (!activePrimaryTasks.Any()) return DateTime.MinValue;
                return activePrimaryTasks.Max(t => t.EndDate);
            }
        }

        // --- UI STATE ---
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void CalculateRollupSegments()
        {
            if (!Tasks.Any())
            {
                RollupSegments = new ObservableCollection<RollupSegment>();
                return;
            }

            var segments = new List<RollupSegment>();

            // 1. Force .Date on Min/Max to remove any timestamp noise
            var minDate = Tasks.Min(t => t.StartDate).Date;
            var maxDate = Tasks.Max(t => t.EndDate).Date;

            // 2. Critical Points
            var criticalDates = new HashSet<DateTime>();
            criticalDates.Add(minDate);
            criticalDates.Add(maxDate.AddDays(1)); // +1 Day for the final boundary

            foreach (var t in Tasks)
            {
                // Force .Date on every task input
                var tStart = t.StartDate.Date;
                var tEnd = t.EndDate.Date;

                if (tStart >= minDate && tStart <= maxDate)
                    criticalDates.Add(tStart);

                // Add End + 1 (Transition point)
                var dayAfter = tEnd.AddDays(1);
                if (dayAfter >= minDate && dayAfter <= maxDate.AddDays(1))
                    criticalDates.Add(dayAfter);
            }

            var sortedDates = criticalDates.OrderBy(d => d).ToList();

            // 3. Iterate Intervals
            for (int i = 0; i < sortedDates.Count - 1; i++)
            {
                var segmentStart = sortedDates[i];
                var segmentEnd = sortedDates[i + 1]; // Continuous Logic

                // 4. Check Overlap using Strict Dates
                // Task covers this slice if: TaskStart < SegEnd AND (TaskEnd + 1) > SegStart
                var activeTasks = Tasks.Where(t =>
                    t.StartDate.Date < segmentEnd &&
                    t.EndDate.Date.AddDays(1) > segmentStart
                ).ToList();

                SegmentStatus status;

                if (!activeTasks.Any())
                {
                    status = SegmentStatus.Available;
                }
                else
                {
                    bool isBusy = activeTasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary);
                    status = isBusy ? SegmentStatus.Busy : SegmentStatus.Available;
                }

                // 5. Merge contiguous
                if (segments.Any() && segments.Last().Status == status && segments.Last().End == segmentStart)
                {
                    segments.Last().End = segmentEnd;
                }
                else
                {
                    segments.Add(new RollupSegment
                    {
                        Start = segmentStart,
                        End = segmentEnd,
                        Status = status
                    });
                }
            }

            RollupSegments = new ObservableCollection<RollupSegment>(segments);
        }

    }
}
