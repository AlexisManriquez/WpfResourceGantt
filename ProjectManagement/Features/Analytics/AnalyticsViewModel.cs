using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using LiveCharts;
using LiveCharts.Wpf;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;
using WpfResourceGantt;
using System.Windows;
using LiveCharts.Defaults;
using TaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus;

namespace WpfResourceGantt.ProjectManagement.Features.Analytics
{
    public class AnalyticsViewModel : ViewModelBase
    {
        // Data Sources
        private ObservableCollection<ResourcePerson> _resources;
        public ObservableCollection<ResourcePerson> Resources
        {
            get => _resources;
            set { _resources = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ResourceTask> _unassignedTasks;
        public ObservableCollection<ResourceTask> UnassignedTasks
        {
            get => _unassignedTasks;
            set { _unassignedTasks = value; OnPropertyChanged(); }
        }

        // --- FILTERING ---
        // Analytics can have its own independent filters, 
        // or we can update these to match the Gantt's filters if we want them synced.

        private ObservableCollection<string> _analyticsRoleOptions;
        public ObservableCollection<string> AnalyticsRoleOptions
        {
            get => _analyticsRoleOptions;
            set { _analyticsRoleOptions = value; OnPropertyChanged(); }
        }

        private string _selectedRoleFilter = "All Roles";
        public string SelectedRoleFilter
        {
            get => _selectedRoleFilter;
            set
            {
                _selectedRoleFilter = value;
                OnPropertyChanged();
                CalculateAnalytics();
                CalculatePieChart();
            }
        }

        // Section Filter for Analytics
        private string _selectedSectionFilter = "All Sections";
        public string SelectedSectionFilter
        {
            get => _selectedSectionFilter;
            set
            {
                _selectedSectionFilter = value;
                OnPropertyChanged();
                CalculateAnalytics();
                CalculatePieChart();
            }
        }

        private ObservableCollection<string> _pieSectionOptions;
        public ObservableCollection<string> PieSectionOptions
        {
            get => _pieSectionOptions;
            set { _pieSectionOptions = value; OnPropertyChanged(); }
        }

        // --- RANGE ---
        private int _analyticsRangeDays = 180;
        public int AnalyticsRangeDays
        {
            get => _analyticsRangeDays;
            set
            {
                _analyticsRangeDays = value;
                OnPropertyChanged();
                CalculateAnalytics(); // Recalculate
            }
        }

        // --- CHARTS STATE ---
        public Func<double, string> YFormatter { get; set; } = value => value.ToString("N0");

        private SeriesCollection _availabilitySeries;
        public SeriesCollection AvailabilitySeries
        {
            get => _availabilitySeries;
            set { _availabilitySeries = value; OnPropertyChanged(); }
        }

        private SeriesCollection _pieChartSeries;
        public SeriesCollection PieChartSeries
        {
            get => _pieChartSeries;
            set { _pieChartSeries = value; OnPropertyChanged(); }
        }

        private List<string> _chartLabels;
        public List<string> ChartLabels
        {
            get => _chartLabels;
            set { _chartLabels = value; OnPropertyChanged(); }
        }

        private double _maxYAxis;
        public double MaxYAxis
        {
            get => _maxYAxis;
            set { _maxYAxis = value; OnPropertyChanged(); }
        }

        private double _axisStep = 1;
        public double AxisStep
        {
            get => _axisStep;
            set { _axisStep = value; OnPropertyChanged(); }
        }

        // KPI State
        private DateTime _kpiTargetDate = DateTime.Today;
        public DateTime KpiTargetDate
        {
            get => _kpiTargetDate;
            set { _kpiTargetDate = value; OnPropertyChanged(); }
        }

        private int _statUnassignedCount;
        public int StatUnassignedCount
        {
            get => _statUnassignedCount;
            set { _statUnassignedCount = value; OnPropertyChanged(); }
        }

        private int _statAvailableTodayCount;
        public int StatAvailableTodayCount
        {
            get => _statAvailableTodayCount;
            set { _statAvailableTodayCount = value; OnPropertyChanged(); }
        }

        private int _statTotalRelevantStaff;
        public int StatTotalRelevantStaff
        {
            get => _statTotalRelevantStaff;
            set { _statTotalRelevantStaff = value; OnPropertyChanged(); }
        }

        private int _filteredTotalStaffCount;
        public int FilteredTotalStaffCount
        {
            get => _filteredTotalStaffCount;
            set { _filteredTotalStaffCount = value; OnPropertyChanged(); }
        }

        // Titles
        private string _kpiAvailableTitle = "Available Today";
        public string KpiAvailableTitle
        {
            get => _kpiAvailableTitle;
            set { _kpiAvailableTitle = value; OnPropertyChanged(); }
        }

        private string _lineChartTitle = "Availability Projection";
        public string LineChartTitle
        {
            get => _lineChartTitle;
            set { _lineChartTitle = value; OnPropertyChanged(); }
        }

        private string _pieChartTitle = "Resource Utilization";
        public string PieChartTitle
        {
            get => _pieChartTitle;
            set { _pieChartTitle = value; OnPropertyChanged(); }
        }


        // Pie Controls
        private ObservableCollection<ResourcePerson> _pieAvailablePeople;
        public ObservableCollection<ResourcePerson> PieAvailablePeople
        {
            get => _pieAvailablePeople;
            set { _pieAvailablePeople = value; OnPropertyChanged(); }
        }

        private string _selectedPieSection = "All Sections";
        public string SelectedPieSection
        {
            get => _selectedPieSection;
            set
            {
                _selectedPieSection = value;
                OnPropertyChanged();
                // This seems redundant if SelectedSectionFilter covers it, but the original code had 
                // distinct controls for Pie Section vs Gantt Section.
                // We'll keep it for now or consolidate if requested.
                // Actually, let's map it to SelectedSectionFilter to be cleaner?
                // The original had separate lists: _pieSectionOptions vs SectionStatistics chips.
                // We'll keep it separate to match original behavior.
                CalculatePieChart();
            }
        }


        // Commands
        public ICommand SetAnalyticsRangeCommand { get; set; }

        public AnalyticsViewModel()
        {
            AvailabilitySeries = new SeriesCollection();
            SetAnalyticsRangeCommand = new RelayCommand<object>(param =>
            {
                if (int.TryParse(param?.ToString(), out int days))
                {
                    AnalyticsRangeDays = days;
                }
            });
        }

        public void LoadData(ObservableCollection<ResourcePerson> resources, ObservableCollection<ResourceTask> unassigned)
        {
            Resources = resources;
            UnassignedTasks = unassigned;

            UpdateAnalyticsRoleList();
            UpdatePieSectionList();

            // Initial Calculation
            CalculateAnalytics();
            CalculatePieChart();
        }

        private void UpdateAnalyticsRoleList()
        {
            var roles = new ObservableCollection<string> { "All Roles" };
            if (Resources != null)
            {
                var unique = Resources.Select(r => r.Role).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().OrderBy(r => r);
                foreach (var r in unique) roles.Add(r);
            }
            AnalyticsRoleOptions = roles;
        }

        private void UpdatePieSectionList()
        {
            var options = new ObservableCollection<string> { "All Sections" };
            if (Resources != null)
            {
                var sections = Resources.Select(r => r.Section).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s);
                foreach (var s in sections) options.Add(s);
            }
            PieSectionOptions = options;
        }

        public async void CalculateAnalytics()
        {
            if (Resources == null || !Resources.Any()) return;

            var allPeople = Resources.ToList();
            var allBacklog = UnassignedTasks != null ? UnassignedTasks.ToList() : new List<ResourceTask>();

            string roleFilter = SelectedRoleFilter;
            string sectionFilter = SelectedSectionFilter;

            int rangeDays = AnalyticsRangeDays;
            DateTime viewStart = DateTime.Today.AddMonths(-1);
            DateTime viewEnd = DateTime.Today.AddDays(rangeDays);

            // Update Titles
            LineChartTitle = $"Availability Projection ({rangeDays} DAY WINDOW)".ToUpper();
            KpiAvailableTitle = $"Available: {viewEnd:MMM dd, yyyy}".ToUpper();

            await Task.Run(() =>
            {
                // A. FILTERING
                IEnumerable<ResourcePerson> relevantPeople = allPeople;
                if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All Roles")
                    relevantPeople = relevantPeople.Where(r => string.Equals(r.Role, roleFilter, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(sectionFilter) && sectionFilter != "All Sections" && sectionFilter != "All")
                    relevantPeople = relevantPeople.Where(r => r.Section == sectionFilter);

                var filteredList = relevantPeople.ToList();
                int totalStaff = filteredList.Count;

                // B. KPI
                DateTime kpiTarget = viewEnd;
                if (rangeDays <= 1) kpiTarget = DateTime.Today;

                int kpiAvailable = filteredList.Count(p =>
                    !p.Tasks.Any(t => (t.Status == TaskStatus.InWork || t.Status == TaskStatus.Future) &&
                                      t.AssignmentRole == AssignmentRole.Primary &&
                                      kpiTarget >= t.StartDate &&
                                      kpiTarget <= t.EndDate));

                int kpiUnassigned = allBacklog.Count(t =>
                   (t.Status == TaskStatus.InWork || t.Status == TaskStatus.Future) &&
                   kpiTarget >= t.StartDate &&
                   kpiTarget <= t.EndDate);

                // C. CHART
                int calculationStep = 1;
                if (rangeDays > 3650) calculationStep = 30;
                else if (rangeDays > 730) calculationStep = 7;
                else if (rangeDays > 180) calculationStep = 2;

                var dateLabels = new List<string>();
                var availValues = new List<int>();
                var backlogValues = new List<int>();

                for (var date = viewStart; date <= viewEnd; date = date.AddDays(calculationStep))
                {
                    string label = rangeDays > 730 ? date.ToString("yyyy") : date.ToString("MMM dd");
                    dateLabels.Add(label);

                    int chartAvail = filteredList.Count(person =>
                    {
                        bool isBusy = person.Tasks.Any(t =>
                           (t.Status == TaskStatus.InWork || t.Status == TaskStatus.Future) &&
                           t.AssignmentRole == AssignmentRole.Primary &&
                           date >= t.StartDate &&
                           date <= t.EndDate);
                        return !isBusy;
                    });
                    availValues.Add(chartAvail);

                    int chartBacklog = allBacklog.Count(t =>
                       (t.Status == TaskStatus.InWork || t.Status == TaskStatus.Future) &&
                       date >= t.StartDate &&
                       date <= t.EndDate);
                    backlogValues.Add(chartBacklog);
                }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    StatTotalRelevantStaff = totalStaff;
                    FilteredTotalStaffCount = totalStaff;
                    StatAvailableTodayCount = kpiAvailable;
                    StatUnassignedCount = kpiUnassigned;

                    ChartLabels = dateLabels;
                    int maxVal = Math.Max(totalStaff, backlogValues.Any() ? backlogValues.Max() : 0);
                    MaxYAxis = maxVal + 2;
                    AxisStep = Math.Ceiling(rangeDays / 12.0);
                    if (AxisStep < 1) AxisStep = 1;

                    AvailabilitySeries = new SeriesCollection
                    {
                         new LineSeries
                         {
                             Title = "Available People",
                             Values = new ChartValues<int>(availValues),
                             PointGeometry = null,
                             LineSmoothness = 0,
                             Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                             Fill = Brushes.Transparent
                         },
                         new LineSeries
                         {
                             Title = "Unassigned Tasks",
                             Values = new ChartValues<int>(backlogValues),
                             PointGeometry = null,
                             LineSmoothness = 0,
                             Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                             Fill = Brushes.Transparent
                         }
                    };
                }));
            });
        }

        public void CalculatePieChart()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Resources == null) return;

                // 1. FILTERING
                IEnumerable<ResourcePerson> targetGroup = Resources;
                if (!string.IsNullOrEmpty(SelectedRoleFilter) && SelectedRoleFilter != "All Roles")
                    targetGroup = targetGroup.Where(r => string.Equals(r.Role, SelectedRoleFilter, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(SelectedPieSection) && SelectedPieSection != "All Sections")
                    targetGroup = targetGroup.Where(r => r.Section == SelectedPieSection);

                // 2. TARGET DATE (Use global range)
                int daysOffset = AnalyticsRangeDays;
                DateTime targetDate = DateTime.Today.AddDays(daysOffset);

                PieChartTitle = $"Utilization ({daysOffset} Day Horizon)";

                // 3. IDENTIFY AVAILABLE
                var availableList = targetGroup.Where(p =>
                    !p.Tasks.Any(t =>
                        t.Status == TaskStatus.InWork &&
                        t.AssignmentRole == AssignmentRole.Primary &&
                        targetDate >= t.StartDate &&
                        targetDate <= t.EndDate
                    )).OrderBy(p => p.Name).ToList();

                int total = targetGroup.Count();
                int availableCount = availableList.Count;
                int busyCount = total - availableCount;

                PieAvailablePeople = new ObservableCollection<ResourcePerson>(availableList);

                // 4. UPDATE CHART
                bool canUpdateExisting = PieChartSeries != null &&
                                         PieChartSeries.Count >= 2 &&
                                         PieChartSeries[0].Values != null &&
                                         PieChartSeries[0].Values.Count > 0 &&
                                         PieChartSeries[1].Values != null &&
                                         PieChartSeries[1].Values.Count > 0;

                if (canUpdateExisting)
                {
                    try
                    {
                        PieChartSeries[0].Values[0] = busyCount;
                        PieChartSeries[1].Values[0] = availableCount;

                        if (PieChartSeries[0] is PieSeries busySeries) busySeries.DataLabels = busyCount > 0;
                        if (PieChartSeries[1] is PieSeries availSeries) availSeries.DataLabels = availableCount > 0;
                    }
                    catch { canUpdateExisting = false; }
                }

                if (!canUpdateExisting)
                {
                    var busyBrush = Application.Current.TryFindResource("StatusBusyBrush") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    var availBrush = Application.Current.TryFindResource("StatusAvailableBrush") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(16, 185, 129));

                    PieChartSeries = new SeriesCollection
                    {
                        new PieSeries
                        {
                            Title = "Busy",
                            Values = new ChartValues<int> { busyCount },
                            DataLabels = busyCount > 0,
                            Fill = busyBrush,
                            StrokeThickness = 0,
                            LabelPoint = p => $"{p.Y} Busy"
                        },
                        new PieSeries
                        {
                            Title = "Available",
                            Values = new ChartValues<int> { availableCount },
                            DataLabels = availableCount > 0,
                            Fill = availBrush,
                            StrokeThickness = 0,
                            LabelPoint = p => $"{p.Y} Available"
                        }
                    };
                }
            }));
        }
    }
}
