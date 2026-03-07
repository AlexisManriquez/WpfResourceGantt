using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement
{
    public enum MetricStatus
    {
        Good,   // Green
        Warning, // Yellow
        Bad     // Red
    }
    public enum ProjectHealthStatus
    {
        NotStarted,
        OnTrack,
        Ahead,
        Behind,
        Complete
    }
    // Use this for INotifyPropertyChanged to update the UI automatically
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // Ensure your method signature has [CallerMemberName] and "= null"
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Represents any item in the breakdown structure
    public class WorkItem : ViewModelBase
    {

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        private string _id;
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
        public int Sequence { get; set; }
        public string Name { get; set; }

        public string WbsValue { get; set; }
        public string Predecessors { get; set; }
        public int Level { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public string StatusText => $"Progress: {Progress:P0} Complete";

        public string WorkDisplay => $"{ActualWork:N1} / {Work:N1} hrs";
        // For the Gantt chart
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public DateTime? ActualFinishDate { get; set; }
        public double Work { get; set; }
        public bool IsCritical { get; set; }
        public int TotalFloat { get; set; }

        /// <summary>
        /// 3-state schedule health based on Total Float:
        ///   Bad     = Critical path (TotalFloat ≤ 0)
        ///   Warning = Near-critical (TotalFloat 1–5 days) — "start the salad now"
        ///   Good    = Plenty of buffer (TotalFloat > 5)
        /// </summary>
        public MetricStatus ScheduleHealth
        {
            get
            {
                if (TotalFloat <= 0) return MetricStatus.Bad;
                if (TotalFloat <= 5) return MetricStatus.Warning;
                return MetricStatus.Good;
            }
        }

        /// <summary>
        /// The latest date this task can start without delaying the project.
        /// Calculated as StartDate + TotalFloat business days.
        /// Returns null for summary nodes (their dates are rolled up, not scheduled).
        /// </summary>
        public DateTime? LateStart
        {
            get
            {
                if (IsSummary || TotalFloat <= 0) return null;
                return WorkBreakdownItem.AddBusinessDays(StartDate, TotalFloat);
            }
        }
        public bool IsBaselined { get; set; }
        public DateTime? BaselineStartDate { get; set; }
        public DateTime? BaselineEndDate { get; set; }

        private double _actualWork;
        public double ActualWork
        {
            get => _actualWork;
            set { _actualWork = value; OnPropertyChanged(); }
        }
        private double _scheduleVariance;
        public double ScheduleVariance
        {
            get => _scheduleVariance;
            set
            {
                if (_scheduleVariance != value)
                {
                    _scheduleVariance = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ScheduleVariancePercent));
                }
            }
        }

        private double _costVariance;
        public double CostVariance
        {
            get => _costVariance;
            set
            {
                if (_costVariance != value)
                {
                    _costVariance = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CostVariancePercent));
                }
            }
        }




        // EVM Properties
        private double _bac;
        public double BAC { get => _bac; set { _bac = value; OnPropertyChanged(); } }

        private double? _bcws;
        public double? Bcws
        {
            get => _bcws;
            set
            {
                if (_bcws != value)
                {
                    _bcws = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ScheduleVariancePercent));
                }
            }
        }

        private double? _bcwp;
        public double? Bcwp
        {
            get => _bcwp;
            set
            {
                if (_bcwp != value)
                {
                    _bcwp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CostVariancePercent));
                }
            }
        }

        private double? _acwp;
        public double? Acwp
        {
            get => _acwp;
            set
            {
                if (_acwp != value)
                {
                    _acwp = value;
                    OnPropertyChanged();
                    // When Actual Cost changes, the Cost Variance percentage must be re-evaluated
                    OnPropertyChanged(nameof(CostVariancePercent));
                }
            }
        }

        private double _currentSpi;
        public double CurrentSpi
        {
            get => _currentSpi;
            set
            {
                _currentSpi = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpiColor));
            }
        }

        private double _currentCpi;
        public double CurrentCpi
        {
            get => _currentCpi;
            set
            {
                _currentCpi = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpiColor));
            }
        }

        public string SpiColor => CurrentSpi < 0.9 ? "#FF4D4D" : (CurrentSpi < 1.0 ? "#FFC107" : "#00C853");
        public string CpiColor => CurrentCpi < 1.0 ? "#FF4D4D" : "#00C853";

        public double ScheduleVariancePercent
        {
            get
            {
                // DoD Standard: SV / BCWS
                if (Bcws == null || Math.Abs(Bcws.Value) < 0.01) return 0.0;
                return ScheduleVariance / Bcws.Value;
            }
        }

        public double CostVariancePercent
        {
            get
            {
                // DoD Standard: CV / BCWP
                if (Bcwp == null || Math.Abs(Bcwp.Value) < 0.01) return 0.0;
                return CostVariance / Bcwp.Value;
            }
        }
        // Progress from 0.0 to 1.0 (e.g., 0.45 for 45%)
        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(StatusText)); }
        }

        private ProjectHealthStatus _healthStatus;
        public ProjectHealthStatus HealthStatus
        {
            get => _healthStatus;
            set { _healthStatus = value; OnPropertyChanged(); }
        }

        private MetricStatus _workHealth;
        public MetricStatus WorkHealth
        {
            get => _workHealth;
            set { _workHealth = value; OnPropertyChanged(); }
        }
        // This is the key for the hierarchy
        public ObservableCollection<WorkItem> Children { get; set; } = new ObservableCollection<WorkItem>();

        public WorkItemType ItemType { get; set; }
        public string SectionChiefName { get; set; }
        public string ProjectManagerName { get; set; }
        private string _developerName;
        public string DeveloperName
        {
            get => _developerName;
            set { _developerName = value; OnPropertyChanged(); }
        }
        public string AssignButtonText { get; set; } = "Assign"; // Default text

        // Helper properties for easy XAML binding
        public bool IsSystem => ItemType == WorkItemType.System;
        public bool IsSummary => Children != null && Children.Count > 0;
        public bool IsLeaf => !IsSummary && ItemType != WorkItemType.System;
        public bool IsSubProject => Level == 2;

        public ObservableCollection<ProgressBlock> ProgressBlocks { get; set; } = new ObservableCollection<ProgressBlock>();
        private ObservableCollection<ResourceAssignment> _assignments = new ObservableCollection<ResourceAssignment>();
        public ObservableCollection<ResourceAssignment> Assignments
        {
            get => _assignments;
            set { _assignments = value; OnPropertyChanged(); }
        }

        // NEW: Visibility toggle for Progress Blocks
        private bool _areProgressBlocksVisible;
        public bool AreProgressBlocksVisible
        {
            get => _areProgressBlocksVisible && IsLeaf; // Fail-safe: Only return true if it's a leaf
            set
            {
                // Only allow setting to true if it's actually a leaf
                _areProgressBlocksVisible = value && IsLeaf;
                OnPropertyChanged();
            }
        }
        // Commands for the new buttons (we will wire these up in GanttViewModel)
        public ICommand AssignSystemUsersCommand { get; set; }
        public ICommand AssignDeveloperCommand { get; set; }

        public WorkItem FindChild(string id)
        {
            return Children.FirstOrDefault(c => c.Id == id);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        private Models.WorkItemStatus _status = Models.WorkItemStatus.Active;
        public Models.WorkItemStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public void RecalculateRollup()
        {
            if (IsSummary)
            {
                // 1. Recurse into children first
                foreach (var child in Children)
                {
                    child.RecalculateRollup();
                }

                // 2. Rollup Dates
                if (Children.Any())
                {
                    StartDate = Children.Min(c => c.StartDate);
                    EndDate = Children.Max(c => c.EndDate);
                }

                // 3. Rollup Work and EVM Metrics
                Work = Math.Round(Children.Sum(c => c.Work), 2);
                ActualWork = Math.Round(Children.Sum(c => c.ActualWork), 2);
                BAC = Math.Round(Children.Sum(c => c.BAC), 2);
                Bcws = Math.Round(Children.Sum(c => c.Bcws ?? 0), 2);
                Bcwp = Math.Round(Children.Sum(c => c.Bcwp ?? 0), 2);
                Acwp = Math.Round(Children.Sum(c => c.Acwp ?? 0), 2);

                // 4. Update Progress (Weighted by BAC)
                if (BAC > 0)
                {
                    Progress = Math.Round((Bcwp ?? 0) / BAC, 4);
                }
                else if (Children.Any())
                {
                    Progress = Math.Round(Children.Average(c => c.Progress), 4);
                }
            }
            else
            {
                // LEAF LOGIC: Update Earned Value based on checklist progress
                Bcwp = Math.Round(BAC * Progress, 2);
                // ACWP/ActualWork are typically updated via CSV imports or direct entry
            }

            // Update UI helper properties
            OnPropertyChanged(nameof(WorkDisplay));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
