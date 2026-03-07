using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;
using WpfResourceGantt;
using System.Windows;
using TaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus;

namespace WpfResourceGantt.ProjectManagement.Features.ResourceGantt
{
    public class SectionStats : INotifyPropertyChanged
    {
        public string SectionName { get; set; }
        public int AvailableCount { get; set; }
        public int TotalCount { get; set; }

        // Display: "B 1/2" or "All 5/10"
        public string DisplayText => SectionName == "All" ? "All Sections" : $"Section {SectionName}";

        public bool IsGreen => AvailableCount > 0;

        // --- NEW: SELECTION STATE ---
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        // Command to handle the click
        public RelayCommand SelectCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ResourceGanttViewModel : ViewModelBase
    {
        // Data Source
        private ObservableCollection<ResourcePerson> _resources;
        public ObservableCollection<ResourcePerson> Resources
        {
            get => _resources;
            set { _resources = value; OnPropertyChanged(); }
        }

        public ICollectionView ResourcesView { get; set; }

        // Timeline State
        private ObservableCollection<TimelineSegment> _timelineSegments;
        public ObservableCollection<TimelineSegment> TimelineSegments
        {
            get => _timelineSegments;
            set { _timelineSegments = value; OnPropertyChanged(); }
        }

        private DateTime _viewStartDate = DateTime.Today.AddMonths(-1);
        public DateTime ViewStartDate
        {
            get => _viewStartDate;
            set
            {
                if (_viewStartDate != value)
                {
                    _viewStartDate = value;
                    OnPropertyChanged();
                    GenerateTimeline();
                    RefreshViewAwareData();
                }
            }
        }

        private DateTime _viewEndDate = DateTime.Today.AddMonths(6);
        public DateTime ViewEndDate
        {
            get => _viewEndDate;
            set
            {
                if (_viewEndDate != value)
                {
                    _viewEndDate = value;
                    OnPropertyChanged();
                    GenerateTimeline();
                    RefreshViewAwareData();
                }
            }
        }

        public double TotalDaysInView => (ViewEndDate - ViewStartDate).TotalDays;

        private int _selectedRangeDays = 30;
        public int SelectedRangeDays
        {
            get => _selectedRangeDays;
            set { _selectedRangeDays = value; OnPropertyChanged(); }
        }

        // UI Commands/State
        private bool _isAllExpanded = false;
        public bool IsAllExpanded
        {
            get => _isAllExpanded;
            set { _isAllExpanded = value; OnPropertyChanged(); }
        }

        private bool _showGridLines = false;
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; OnPropertyChanged(); }
        }

        // Filtering State
        private ObservableCollection<SectionStats> _sectionStatistics;
        public ObservableCollection<SectionStats> SectionStatistics
        {
            get => _sectionStatistics;
            set { _sectionStatistics = value; OnPropertyChanged(); }
        }

        private string _selectedSectionFilter = "All Sections";
        public string SelectedSectionFilter
        {
            get => _selectedSectionFilter;
            set
            {
                _selectedSectionFilter = value;
                OnPropertyChanged();
                ResourcesView?.Refresh();

                // Notify logic that filter changed (if needed by parent)
                // For now, we handle local refresh.
            }
        }

        // --- NEW: TODAY LINE PROPERTIES ---
        public double TodayLinePosition { get; private set; }
        public bool IsTodayLineVisible { get; private set; }
        private double _timelineWidth;
        public double TimelineWidth
        {
            get => _timelineWidth;
            set { _timelineWidth = value; OnPropertyChanged(); UpdateTodayLinePosition(); }
        }

        private void UpdateTodayLinePosition()
        {
            var today = DateTime.Now;
            if (today >= ViewStartDate && today <= ViewEndDate)
            {
                IsTodayLineVisible = true;
                // Use +1 for the denominator to match inclusive duration of segments
                double totalDays = (ViewEndDate - ViewStartDate).TotalDays + 1;
                double daysFromStart = (today - ViewStartDate).TotalDays;
                TodayLinePosition = (totalDays > 0) ? (daysFromStart / totalDays) * TimelineWidth : 0;
            }
            else
            {
                IsTodayLineVisible = false;
                TodayLinePosition = 0;
            }
            OnPropertyChanged(nameof(TodayLinePosition));
            OnPropertyChanged(nameof(IsTodayLineVisible));
        }

        private ObservableCollection<string> _roleFilterOptions;
        public ObservableCollection<string> RoleFilterOptions
        {
            get => _roleFilterOptions;
            set { _roleFilterOptions = value; OnPropertyChanged(); }
        }

        private string _selectedRoleFilter = "All Roles";
        public string SelectedRoleFilter
        {
            get => _selectedRoleFilter;
            set
            {
                _selectedRoleFilter = value;
                OnPropertyChanged();
                ResourcesView?.Refresh();
            }
        }

        // Commands
        public ICommand ToggleExpandAllCommand { get; set; }
        public ICommand ToggleGridLinesCommand { get; set; }
        public ICommand SetGlobalRangeCommand { get; set; }
        public ICommand SetTimeRangeCommand { get; set; }


        public ResourceGanttViewModel()
        {
            ToggleExpandAllCommand = new RelayCommand(() => ToggleExpandAll());
            ToggleGridLinesCommand = new RelayCommand(() => ShowGridLines = !ShowGridLines);

            SetGlobalRangeCommand = new RelayCommand<object>(param =>
            {
                if (int.TryParse(param?.ToString(), out int days))
                {
                    ApplyGlobalRange(days);
                }
            });

            SetTimeRangeCommand = new RelayCommand<object>(param =>
            {
                if (param == null) return;
                string p = param.ToString();
                int days = 30; // default

                switch (p)
                {
                    case "Days30": days = 30; break;
                    case "Days60": days = 60; break;
                    case "Days90": days = 90; break;
                    case "Days180": days = 180; break;
                    case "Year1": days = 365; break;
                    case "Year3": days = 1095; break;
                    case "All": days = 3650; break; // 10 years
                    default:
                        if (!int.TryParse(p, out days)) days = 30;
                        break;
                }

                ApplyGlobalRange(days);
            });
        }

        public void LoadData(ObservableCollection<ResourcePerson> resources)
        {
            Resources = resources;
            SetupResourcesView();
            UpdateRoleFilterList();
            CalculateSectionStats();
            GenerateTimeline();
            RefreshViewAwareData();
        }

        private void SetupResourcesView()
        {
            if (Resources == null) return;

            ResourcesView = CollectionViewSource.GetDefaultView(Resources);
            ResourcesView.GroupDescriptions.Clear();
            ResourcesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ResourcePerson.SectionGroupKey)));

            ResourcesView.SortDescriptions.Clear();
            // 1. Primary Sort: By Group Name
            ResourcesView.SortDescriptions.Add(new SortDescription(nameof(ResourcePerson.SectionGroupKey), ListSortDirection.Ascending));
            // 2. Secondary Sort: By Availability Date
            ResourcesView.SortDescriptions.Add(new SortDescription(nameof(ResourcePerson.AvailabilityDate), ListSortDirection.Ascending));

            ResourcesView.Filter = FilterResources;
            OnPropertyChanged(nameof(ResourcesView));
        }

        private bool FilterResources(object item)
        {
            if (item is ResourcePerson person)
            {
                // A. Check Section
                bool matchSection = true;
                if (!string.IsNullOrEmpty(SelectedSectionFilter) && SelectedSectionFilter != "All Sections")
                {
                    if (SelectedSectionFilter == "Unassigned")
                        matchSection = string.IsNullOrWhiteSpace(person.Section);
                    else
                        matchSection = (person.Section == SelectedSectionFilter);
                }

                // B. Check Role
                bool matchRole = true;
                if (!string.IsNullOrEmpty(SelectedRoleFilter) && SelectedRoleFilter != "All Roles")
                {
                    matchRole = string.Equals(person.Role, SelectedRoleFilter, StringComparison.OrdinalIgnoreCase);
                }

                return matchSection && matchRole;
            }
            return false;
        }

        public void RefreshViewAwareData()
        {
            if (Resources == null) return;

            // Changed from Parallel.ForEach to standard foreach to prevent thread violations
            // during PropertyChanged events that affect CollectionViews/Grouping.
            foreach (var person in Resources)
            {
                person.RefreshViewVisibility(ViewStartDate, ViewEndDate);
            }

            SortResources();
            CalculateSectionStats();

            Application.Current.Dispatcher.Invoke(() => ResourcesView?.Refresh());
        }

        private void SortResources()
        {
            // Enforce Sort order (Earliest Start Date) for tasks within person
            // Note: ObservableCollection is not thread safe for modification, but sorting the internal list logic might need care.
            // Actually original code did this in LoadResourcesIntoView/ParseCsv. 
            // Here we assume tasks are sorted or we sort them conservatively.
        }

        private void GenerateTimeline()
        {
            if (ViewEndDate <= ViewStartDate) ViewEndDate = ViewStartDate.AddDays(1);

            // Notify properties
            OnPropertyChanged(nameof(ViewStartDate));
            OnPropertyChanged(nameof(ViewEndDate));

            var segments = new ObservableCollection<TimelineSegment>();
            double totalDays = (ViewEndDate - ViewStartDate).TotalDays;

            bool useYearlyMode = totalDays > 730;
            var current = ViewStartDate;

            if (useYearlyMode)
            {
                while (current < ViewEndDate)
                {
                    var endOfYear = new DateTime(current.Year, 12, 31);
                    var actualEnd = endOfYear > ViewEndDate ? ViewEndDate : endOfYear;

                    segments.Add(new TimelineSegment
                    {
                        Name = current.Year.ToString(),
                        StartDate = current,
                        EndDate = actualEnd
                    });
                    current = endOfYear.AddDays(1);
                }
            }
            else
            {
                while (current < ViewEndDate)
                {
                    var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                    var endOfMonth = new DateTime(current.Year, current.Month, daysInMonth);
                    var actualEnd = endOfMonth > ViewEndDate ? ViewEndDate : endOfMonth;

                    segments.Add(new TimelineSegment
                    {
                        Name = totalDays < 180 ? current.ToString("MMMM yyyy") : current.ToString("MMM yyyy"),
                        StartDate = current,
                        EndDate = actualEnd
                    });
                    current = endOfMonth.AddDays(1);
                }
            }

            TimelineSegments = segments;
            UpdateTodayLinePosition();
        }

        private void ToggleExpandAll()
        {
            if (Resources == null || !Resources.Any()) return;

            bool targetState = !IsAllExpanded;
            foreach (var person in Resources)
            {
                person.IsExpanded = targetState;
            }
            IsAllExpanded = targetState;
        }

        private void CalculateSectionStats()
        {
            if (Resources == null) return;

            var statsList = new List<SectionStats>();
            var uniqueSections = Resources.Select(r => r.Section).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();

            // A. All Sections
            int totalAvail = 0;
            int totalStaff = 0;

            foreach (var p in Resources)
            {
                bool isBusy = p.Tasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary && t.StartDate <= ViewEndDate && t.EndDate >= ViewStartDate);
                if (!isBusy) totalAvail++;
                totalStaff++;
            }

            statsList.Add(new SectionStats
            {
                SectionName = "All",
                AvailableCount = totalAvail,
                TotalCount = totalStaff,
                IsSelected = (SelectedSectionFilter == "All Sections" || string.IsNullOrEmpty(SelectedSectionFilter)),
                SelectCommand = new RelayCommand(() => SelectSectionFilter("All Sections"))
            });

            // B. Individual Sections
            foreach (var sec in uniqueSections)
            {
                var people = Resources.Where(r => r.Section == sec).ToList();
                int count = people.Count;
                int avail = people.Count(p => !p.Tasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary && t.StartDate <= ViewEndDate && t.EndDate >= ViewStartDate));

                statsList.Add(new SectionStats
                {
                    SectionName = sec,
                    AvailableCount = avail,
                    TotalCount = count,
                    IsSelected = (SelectedSectionFilter == sec),
                    SelectCommand = new RelayCommand(() => SelectSectionFilter(sec)) // Fixed Delegate (0-arg)
                });
            }

            SectionStatistics = new ObservableCollection<SectionStats>(statsList);
        }

        private void SelectSectionFilter(string sectionName)
        {
            SelectedSectionFilter = sectionName;

            // Update visual state
            if (SectionStatistics != null)
            {
                foreach (var chip in SectionStatistics)
                {
                    chip.IsSelected = (chip.SectionName == sectionName) ||
                                      (sectionName == "All Sections" && chip.SectionName == "All");
                }
            }
        }

        private void UpdateRoleFilterList()
        {
            var roles = new ObservableCollection<string> { "All Roles" };
            if (Resources != null)
            {
                var unique = Resources.Select(r => r.Role).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().OrderBy(r => r);
                foreach (var r in unique) roles.Add(r);
            }
            RoleFilterOptions = roles;
        }

        public void ApplyGlobalRange(int days)
        {
            SelectedRangeDays = days;
            _viewStartDate = DateTime.Today.AddMonths(-1);
            _viewEndDate = DateTime.Today.AddDays(days);

            OnPropertyChanged(nameof(ViewStartDate));
            OnPropertyChanged(nameof(ViewEndDate));
            GenerateTimeline();
            RefreshViewAwareData();
        }
    }
}
