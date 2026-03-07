using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.EVM
{
    public enum EVMDisplayMode
    {
        Hours,
        Dollars,
        Percent
    }

    public class FilterItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public object SourceItem { get; set; }
    }

    public class EVMViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        public Func<double, string> IndexFormatter { get; set; } = value => value.ToString("N1");
        public ObservableCollection<WorkItem> FlatWbsItems { get; set; } = new ObservableCollection<WorkItem>();
        //The KPI Data
        public double CurrentSPI { get; set; }
        public double CurrentCPI { get; set; }
        public double CurrentScheduleVariance { get; set; }
        public double CurrentCostVariance { get; set; }
        public double CurrentProgress { get; set; } // 0 to 1
        public double EAC { get; set; }
        public double VAC { get; set; }
        public double TCPI { get; set; }
        public double TotalBAC { get; set; } // Budget at Completion

        private DateTime _targetFinishDate;
        public DateTime TargetFinishDate
        {
            get => _targetFinishDate;
            set { _targetFinishDate = value; OnPropertyChanged(); }
        }

        public double RemainingProgress => 1.0 - CurrentProgress > 0 ? 1.0 - CurrentProgress : 0;


        // We need these for the Gauge Visuals (Red/Green colors)
        public string SpiColor => CurrentSPI < 0.9 ? "#FF4D4D" : (CurrentSPI < 1.0 ? "#FFC107" : "#00C853");
        public string CpiColor => CurrentCPI < 1.0 ? "#FF4D4D" : "#00C853"; // Simple Red/Green for Cost

        public string TcpiStatus => TCPI <= 1.0 ? "On track" : (TCPI <= 1.1 ? "Caution" : "At Risk");
        public string TcpiColor => TCPI <= 1.0 ? "#00C853" : (TCPI <= 1.1 ? "#FFC107" : "#FF4D4D");
        // The Chart Data
        private SeriesCollection _chartSeries = new SeriesCollection();
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }

        private SeriesCollection _trendSeries = new SeriesCollection();
        public SeriesCollection TrendSeries
        {
            get => _trendSeries;
            set { _trendSeries = value; OnPropertyChanged(); }
        }

        private SeriesCollection _progressSeries = new SeriesCollection();
        public SeriesCollection ProgressSeries
        {
            get => _progressSeries;
            set { _progressSeries = value; OnPropertyChanged(); }
        }

        private string[] _chartLabels = new string[] { };
        public string[] ChartLabels
        {
            get => _chartLabels;
            set { _chartLabels = value; OnPropertyChanged(); }
        }

        private double _labelsStep = 1;
        public double LabelsStep
        {
            get => _labelsStep;
            set { _labelsStep = value; OnPropertyChanged(); }
        }

        private Func<double, string> _yAxisFormatter;
        public Func<double, string> YAxisFormatter
        {
            get => _yAxisFormatter;
            set { _yAxisFormatter = value; OnPropertyChanged(); }
        }


        private string _yAxisTitle = "Cumulative Hours";
        public string YAxisTitle
        {
            get => _yAxisTitle;
            set { _yAxisTitle = value; OnPropertyChanged(); }
        }

        // Determine if we show Hours or Currency (using simple hours for now)
        private bool _showCurrency = false;

        private string _contextHeader;
        public string ContextHeader
        {
            get => _contextHeader;
            set { _contextHeader = value; OnPropertyChanged(); }
        }

        // 1. REPLACEMENT: Project Options for Combobox
        public ObservableCollection<FilterItem> ProjectOptions { get; set; } = new ObservableCollection<FilterItem>();

        private FilterItem _selectedProjectFilter;
        public FilterItem SelectedProjectFilter
        {
            get => _selectedProjectFilter;
            set
            {
                if (_selectedProjectFilter != value)
                {
                    _selectedProjectFilter = value;
                    OnPropertyChanged();
                    // Load the Data
                    if (value?.SourceItem != null)
                    {
                        LoadDataForContext(value.SourceItem);
                    }
                }
            }
        }

        // 2. SUPPORT FOR ZONE D SELECTION
        private WorkItem _selectedWbsItem;
        public WorkItem SelectedWbsItem
        {
            get => _selectedWbsItem;
            set
            {
                if (_selectedWbsItem != value)
                {
                    _selectedWbsItem = value;
                    OnPropertyChanged();

                    if (value != null)
                    {
                        HandleWbsSelection(value);
                    }
                }
            }
        }

        private EVMDisplayMode _displayMode = EVMDisplayMode.Hours;
        public EVMDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                OnPropertyChanged();
                // Update the Y-Axis label format
                UpdateFormatters();
                // Re-generate the graph with new math
                if (SelectedWbsItem != null) HandleWbsSelection(SelectedWbsItem);
                else if (SelectedProjectFilter != null) LoadDataForContext(SelectedProjectFilter.SourceItem);
            }
        }

        // Store the root items so we can re-flatten them without re-fetching
        private List<WorkItem> _currentRootItems = new List<WorkItem>();
        private TimeRange _currentTimeRange = TimeRange.All;

        public ICommand ToggleExpandCommand { get; set; }

        public EVMViewModel(DataService dataService, User currentUser)
        {
            _dataService = dataService;

            // Explicitly initialize collections
            ChartSeries = new SeriesCollection();
            TrendSeries = new SeriesCollection();
            ProgressSeries = new SeriesCollection();

            DisplayMode = dataService.IsEvmHoursBased ? EVMDisplayMode.Hours : EVMDisplayMode.Dollars;
            UpdateFormatters();
            ToggleExpandCommand = new RelayCommand<WorkItem>(ToggleRowExpansion);

            // Defer initial load to allow UI to bind first before properties fire notifications
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                PopulateFilters(currentUser);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void PopulateFilters(User currentUser)
        {
            ProjectOptions.Clear();
            var systems = _dataService.GetSystemsForUser(currentUser);

            foreach (var sys in systems)
            {
                if (sys.Children == null) continue;
                foreach (var child in sys.Children)
                {
                    ProjectOptions.Add(new FilterItem
                    {
                        Id = child.Id,
                        Name = $"{sys.Name} > {child.Name}",
                        SourceItem = child
                    });
                }
            }

            // Auto-select first if available
            if (ProjectOptions.Any())
            {
                SelectedProjectFilter = ProjectOptions.First();
            }
        }

        private void ToggleRowExpansion(WorkItem item)
        {
            if (item == null) return;
            item.IsExpanded = !item.IsExpanded;
            // If we collapse, we need to hide children. If expand, show them.
            // Simplest way is to rebuild the flat list from the roots.
            RebuildFlatList();
        }

        private void HandleWbsSelection(WorkItem selected)
        {
            // We need to re-generate the S-Curve for this specific item and its children.
            // WorkItem contains the 'Children' (ViewModels).
            // GenerateSCurve expects 'WorkBreakdownItem' (Models).
            // This is a mapping mismatch. I should have mapped Model to ViewModel better or have a way to get back.
            // Hack for now: I will map the ViewModel structure back to a temporary Model structure OR update GenerateSCurve to accept WorkItem (Cleaner).
            // I will Update GenerateSCurve to accept WorkItem ViewModels since that is what we are working with in the UI now.

            // Actually, let's keep GenerateSCurve logic but map 'Selected' to a set of Leaf VIEWMODELS then to Models? No.
            // We can find the original model if we kept a reference.
            // Let's modify WorkItem to hold the Source Model? 
            // Or easier: Just search for the Model in the System hierarchy using the ID.

            if (SelectedProjectFilter?.SourceItem is WorkBreakdownItem rootProject)
            {
                var model = FindModelById(rootProject, selected.Id);
                if (model != null)
                {
                    LoadDataForContext(model);
                }
            }
        }

        private WorkBreakdownItem FindModelById(Object root, string id)
        {
            if (root is SystemItem sys)
            {
                foreach (var child in sys.Children)
                {
                    if (child.Id == id) return child;
                    var found = FindModelById(child, id);
                    if (found != null) return found;
                }
            }
            else if (root is WorkBreakdownItem item)
            {
                if (item.Id == id) return item;
                foreach (var child in item.Children)
                {
                    var found = FindModelById(child, id);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public void LoadDataForContext(object selectedItem)
        {
            if (selectedItem is WorkBreakdownItem workItem)
            {
                // Identify Level (Level 1 is Project, 2 is Subproject, etc.)
                string levelType = workItem.Level == 1 ? "Project" : "Subproject";
                if (workItem.Level > 2) levelType = "Task";

                ContextHeader = $"{levelType} Performance: {workItem.Name}";

                var relevantItems = GetAllLeafTasks(new List<WorkBreakdownItem> { workItem });
                GenerateSCurve(relevantItems);

                RefreshWbsDetail(workItem);
            }
        }
        // 2. Update RefreshWbsDetail to "Flatten" the tree
        private void RefreshWbsDetail(object selectedItem)
        {
            _currentRootItems.Clear();

            if (selectedItem is WorkBreakdownItem item)
            {
                foreach (var child in item.Children) _currentRootItems.Add(CreateWorkItemViewModel(child));
            }

            RebuildFlatList();
        }

        private void RebuildFlatList()
        {
            FlatWbsItems.Clear();
            foreach (var root in _currentRootItems)
            {
                FlattenRecursive(root);
            }
        }

        private void FlattenRecursive(WorkItem item)
        {
            FlatWbsItems.Add(item);
            // Only add children if expanded
            if (item.IsExpanded)
            {
                foreach (var child in item.Children)
                {
                    FlattenRecursive(child);
                }
            }
        }
        private WorkItem CreateWorkItemViewModel(WorkBreakdownItem model)
        {
            // Map the model to the ViewModel
            var vm = new WorkItem
            {
                Id = model.Id,
                Name = model.Name,
                WbsValue = model.WbsValue,
                Level = model.Level,
                StartDate = model.StartDate ?? DateTime.Now,
                EndDate = model.EndDate ?? DateTime.Now,
                Progress = model.Progress,
                Work = model.Work ?? 0,
                ActualWork = model.ActualWork ?? 0,
                IsExpanded = true // Default to expanded
            };

            // --- RECURSIVE CALCULATION FOR THIS ROW (mode-aware) ---
            // WBS detail table must honour the active display mode so the values
            // shown here are consistent with the S-Curve and KPI cards above.

            var allLeaves = GetAllLeafTasks(new List<WorkBreakdownItem> { model });

            double modeRate = (_dataService.IsEvmHoursBased) ? 1.0 : 195.0;

            // BAC: planned hours (hours mode) OR planned hours × rate (dollars mode)
            double rowBac = allLeaves.Sum(t => (t.Work ?? 0.0) * modeRate);

            // ACWP: actual hours from timesheets (hours mode) OR actual hours × rate (dollars mode)
            // Always use ActualWork (raw hours) scaled by modeRate — this avoids double-conversion
            // since _dataService.ConvertEvmRecursive already scaled the stored Acwp field.
            double rowAcwp = allLeaves.Sum(t => (t.ActualWork ?? 0.0) * modeRate);

            double rowBcwp = 0;
            double rowBcws = 0;

            foreach (var leaf in allLeaves)
            {
                double leafBac = (leaf.Work ?? 0.0) * modeRate;
                // BCWS: planned progress × BAC-in-mode
                rowBcws += leafBac * CalculatePlannedProgress(leaf, DateTime.Now);

                // BCWP: earned progress × BAC-in-mode (use latest history entry)
                var latestHistory = leaf.ProgressHistory?.OrderByDescending(h => h.Date).FirstOrDefault();
                rowBcwp += leafBac * (latestHistory?.ActualProgress ?? 0);
            }

            // Set Properties (all in the same unit as the active mode)
            vm.Bcws = rowBcws;
            vm.Bcwp = rowBcwp;
            vm.Acwp = rowAcwp;
            vm.ScheduleVariance = rowBcwp - rowBcws;  // SV in hours or dollars
            vm.CostVariance = rowBcwp - rowAcwp;       // CV / Labor Efficiency Variance

            // SPI and CPI remain unitless ratios (same math regardless of mode)
            double spi = rowBcws > 0 ? rowBcwp / rowBcws : 1.0;
            double cpi = rowAcwp > 0 ? rowBcwp / rowAcwp : 1.0;
            vm.CurrentSpi = spi;
            vm.CurrentCpi = cpi;
            // Health Colors (Same logic as cards)
            vm.WorkHealth = spi < 0.9 ? MetricStatus.Bad : (spi < 1.0 ? MetricStatus.Warning : MetricStatus.Good);
            // You could add a CostHealth property to WorkItem as well

            // Forecast Finish Date Logic
            // If SPI is 0.8, the project will take 25% longer
            double totalDays = (vm.EndDate - vm.StartDate).TotalDays;
            double forecastedDays = spi > 0.1 ? totalDays / spi : totalDays;
            vm.ActualFinishDate = vm.StartDate.AddDays(forecastedDays);

            // Recursively add children
            foreach (var child in model.Children)
            {
                vm.Children.Add(CreateWorkItemViewModel(child));
            }

            return vm;
        }

        // Simple helper for planned progress
        private double CalculatePlannedProgress(WorkBreakdownItem task, DateTime date)
        {
            if (date >= task.EndDate) return 1.0;
            if (date <= task.StartDate) return 0.0;

            // --- FIX: Use Business Days instead of TotalDays ---
            double totalWorkingDays = WorkBreakdownItem.GetBusinessDaysSpan(task.StartDate.Value, task.EndDate.Value);
            double workingDaysElapsed = WorkBreakdownItem.GetBusinessDaysSpan(task.StartDate.Value, date);

            return totalWorkingDays > 0 ? workingDaysElapsed / totalWorkingDays : 0;
        }

        private List<WorkBreakdownItem> GetAllLeafTasks(IEnumerable<WorkBreakdownItem> items)
        {
            var leaves = new List<WorkBreakdownItem>();
            foreach (var item in items)
            {
                if (item.Children == null || !item.Children.Any())
                    leaves.Add(item);
                else
                    leaves.AddRange(GetAllLeafTasks(item.Children));
            }
            return leaves;
        }
        private void UpdateFormatters()
        {
            switch (DisplayMode)
            {
                case EVMDisplayMode.Hours:
                    YAxisTitle = "Cumulative Hours";
                    // Explicitly set the format
                    YAxisFormatter = value => value.ToString("N0") + " hrs";
                    break;

                case EVMDisplayMode.Dollars:
                    YAxisTitle = "Cumulative Cost ($)";
                    // C0 removes the "hrs" and adds the "$"
                    YAxisFormatter = value => value.ToString("C0");
                    break;

                case EVMDisplayMode.Percent:
                    YAxisTitle = "Overall Progress (%)";
                    // P0 converts 1.0 to 100% and 0.5 to 50%
                    YAxisFormatter = value => value.ToString("P0");
                    break;
            }
        }

        public void ApplyTimeRange(TimeRange range)
        {
            _currentTimeRange = range;
            // Always reload to ensure current data is refreshed with new range (or same range if data changed)
            if (SelectedWbsItem != null) HandleWbsSelection(SelectedWbsItem);
            else if (SelectedProjectFilter != null) LoadDataForContext(SelectedProjectFilter.SourceItem);
        }

        /// <summary>
        /// Called by MainViewModel after a Close Week operation to reload
        /// the EVM view with freshly captured snapshot data.
        /// </summary>
        public void Refresh()
        {
            if (SelectedProjectFilter != null)
                LoadDataForContext(SelectedProjectFilter.SourceItem);
        }

        private void GenerateSCurve(List<WorkBreakdownItem> tasks)
        {
            // Ensure this runs on UI thread to prevent LiveCharts race conditions
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tasks == null || !tasks.Any())
                {
                    ChartSeries = new SeriesCollection();
                    TrendSeries = new SeriesCollection();
                    ProgressSeries = new SeriesCollection();
                    return;
                }

                // 1. Setup Dates
                TargetFinishDate = tasks.Max(t => t.EndDate ?? DateTime.Now);
                DateTime minDate = tasks.Min(t => t.StartDate ?? DateTime.Now);
                DateTime maxDate = TargetFinishDate;
                if (maxDate <= minDate) maxDate = minDate.AddMonths(1);

                // --- TIME RANGE OVERRIDE ---
                if (_currentTimeRange != TimeRange.All)
                {
                    DateTime anchor = DateTime.Today;
                    if (minDate > anchor) anchor = minDate;

                    DateTime rangeEnd = anchor;
                    switch (_currentTimeRange)
                    {
                        case TimeRange.Days30: rangeEnd = anchor.AddDays(30); break;
                        case TimeRange.Days60: rangeEnd = anchor.AddDays(60); break;
                        case TimeRange.Days90: rangeEnd = anchor.AddDays(90); break;
                        case TimeRange.Days180: rangeEnd = anchor.AddDays(180); break;
                        case TimeRange.Year1: rangeEnd = anchor.AddYears(1); break;
                        case TimeRange.Year3: rangeEnd = anchor.AddYears(3); break;
                    }
                    maxDate = rangeEnd;
                }

                var timePoints = new List<DateTime>();
                DateTime cursor = minDate;
                while (cursor <= maxDate.AddDays(7)) { timePoints.Add(cursor); cursor = cursor.AddDays(7); }

                // 2. Prepare Collections (Local buffers)
                var bcwsValues = new ChartValues<double>();
                var bcwpValues = new ChartValues<double>();
                var acwpValues = new ChartValues<double>();

                double totalProjectHours = tasks.Sum(t => t.Work ?? 0.0);
                if (totalProjectHours == 0) totalProjectHours = 1.0;

                // TotalBAC must honour the active display mode.
                // In Hours mode: BAC = total planned hours (no rate multiplier).
                // In Dollars mode: BAC = total planned hours × $195/hr.
                double bacRate = (DisplayMode == EVMDisplayMode.Dollars) ? 195.0 : 1.0;
                TotalBAC = tasks.Sum(t => (t.Work ?? 0.0) * bacRate);

                // Fixed-dollar accumulators (always in $) used for unitless SPI/CPI ratios.
                double finalBcwsFixed = 0;
                double finalBcwpFixed = 0;
                double finalAcwpFixed = 0;

                var spiTrendValues = new ChartValues<double>();
                var cpiTrendValues = new ChartValues<double>();

                // 3. Populate Data Points
                foreach (var date in timePoints)
                {
                    bool isFuture = date > DateTime.Now;
                    double sumBcwsMode = 0, sumBcwpMode = 0, sumAcwpMode = 0;
                    double sumBcwsFixed = 0, sumBcwpFixed = 0, sumAcwpFixed = 0;

                    foreach (var task in tasks)
                    {
                        double taskHours = task.Work ?? 0.0;
                        DateTime sDate = task.StartDate ?? DateTime.Now;
                        DateTime eDate = task.EndDate ?? DateTime.Now;

                        double plannedProgress = 0;
                        if (date >= eDate) plannedProgress = 1.0;
                        else if (date >= sDate)
                        {
                            double totalWorkingDays = WorkBreakdownItem.GetBusinessDaysSpan(sDate, eDate);
                            double workingDaysElapsed = WorkBreakdownItem.GetBusinessDaysSpan(sDate, date);
                            if (totalWorkingDays > 0) plannedProgress = workingDaysElapsed / totalWorkingDays;
                        }

                        double actualProgress = 0;
                        double actualWorkHours = 0;
                        if (task.ProgressHistory != null)
                        {
                            var history = task.ProgressHistory.Where(h => h.Date <= date).OrderByDescending(h => h.Date).FirstOrDefault();
                            if (history != null)
                            {
                                actualProgress = history.ActualProgress;
                                actualWorkHours = history.ActualWork ?? 0.0;
                            }
                        }

                        // --- S-Curve data point for the active display mode ---
                        switch (DisplayMode)
                        {
                            case EVMDisplayMode.Hours:
                                // DoD Hours mode: pure effort — no rate multiplier (DAU standard)
                                sumBcwsMode += plannedProgress * taskHours;           // Planned Hours
                                sumBcwpMode += actualProgress * taskHours;            // Earned Hours
                                sumAcwpMode += actualWorkHours;                       // Actual Hours (timesheet)
                                break;
                            case EVMDisplayMode.Dollars:
                                sumBcwsMode += (plannedProgress * taskHours) * 195.0; // Planned Value ($)
                                sumBcwpMode += (actualProgress * taskHours) * 195.0;  // Earned Value ($)
                                sumAcwpMode += actualWorkHours * 195.0;               // Actual Cost ($)
                                break;
                            case EVMDisplayMode.Percent:
                                sumBcwsMode += (plannedProgress * taskHours) / totalProjectHours;
                                sumBcwpMode += (actualProgress * taskHours) / totalProjectHours;
                                sumAcwpMode += actualWorkHours / totalProjectHours;
                                break;
                        }

                        // Fixed-dollar accumulators — SPI/CPI are unitless ratios, compute
                        // from consistent dollar values regardless of display mode.
                        sumBcwsFixed += (plannedProgress * taskHours) * 195.0;
                        sumBcwpFixed += (actualProgress * taskHours) * 195.0;
                        sumAcwpFixed += actualWorkHours * 195.0;
                    }

                    bcwsValues.Add(sumBcwsMode);
                    if (!isFuture)
                    {
                        bcwpValues.Add(sumBcwpMode);
                        acwpValues.Add(sumAcwpMode);

                        double pointSpi = sumBcwsFixed > 1.0 ? sumBcwpFixed / sumBcwsFixed : 1.0;
                        double pointCpi = sumAcwpFixed > 1.0 ? sumBcwpFixed / sumAcwpFixed : 1.0;

                        if (sumBcwsFixed < 1.0 && sumBcwpFixed < 1.0) pointSpi = 1.0;
                        if (sumAcwpFixed < 1.0 && sumBcwpFixed < 1.0) pointCpi = 1.0;

                        if (double.IsNaN(pointSpi) || double.IsInfinity(pointSpi)) pointSpi = 1.0;
                        if (double.IsNaN(pointCpi) || double.IsInfinity(pointCpi)) pointCpi = 1.0;

                        if (pointSpi > 2.0) pointSpi = 2.0;
                        if (pointCpi > 2.0) pointCpi = 2.0;

                        spiTrendValues.Add(pointSpi);
                        cpiTrendValues.Add(pointCpi);
                    }

                    if (date <= DateTime.Now)
                    {
                        finalBcwsFixed = sumBcwsFixed;
                        finalBcwpFixed = sumBcwpFixed;
                        finalAcwpFixed = sumAcwpFixed;
                    }
                }

                // 4. Update KPI Cards
                // SPI and CPI are unitless ratios — computed from fixed-dollar accumulators
                // so the ratio is stable regardless of mode. (DoD EVMS: indices have no unit.)
                CurrentSPI = finalBcwsFixed > 0 ? finalBcwpFixed / finalBcwsFixed : 0;
                CurrentCPI = finalAcwpFixed > 0 ? finalBcwpFixed / finalAcwpFixed : 0;

                // SV, CV, EAC, VAC, TCPI must be expressed in the ACTIVE unit (hours or dollars).
                // Re-derive them from the mode-aware TotalBAC and final mode values.
                // bcwsValues.Last() / bcwpValues.Last() / acwpValues.Last() ARE mode-adjusted.
                double finalBcwsMode = bcwsValues.Count > 0 ? bcwsValues.Last() : 0;
                double finalBcwpMode = bcwpValues.Count > 0 ? bcwpValues.Last() : 0;
                double finalAcwpMode = acwpValues.Count > 0 ? acwpValues.Last() : 0;

                // SV = EV - PV  |  In Hours mode: "you are N hours ahead/behind schedule"
                //                   In Dollars mode: "you are $N ahead/behind schedule"
                CurrentScheduleVariance = finalBcwpMode - finalBcwsMode;

                // CV = EV - AC  |  In Hours mode: DoD calls this "Labor Efficiency Variance"
                //                   Positive = completed work in fewer hours than budgeted
                CurrentCostVariance = finalBcwpMode - finalAcwpMode;

                // EAC = BAC / CPI — TotalBAC is already mode-adjusted (hours or dollars)
                EAC = CurrentCPI > 0 ? TotalBAC / CurrentCPI : TotalBAC;
                VAC = TotalBAC - EAC;

                // TCPI uses mode-adjusted values throughout
                double workRemaining = TotalBAC - finalBcwpMode;
                double budgetRemaining = TotalBAC - finalAcwpMode;
                TCPI = budgetRemaining > 0 ? workRemaining / budgetRemaining : 0;

                // Progress is always 0..1 regardless of mode
                CurrentProgress = TotalBAC > 0 ? finalBcwpFixed / (tasks.Sum(t => (t.Work ?? 0.0) * 195.0)) : 0;

                if (double.IsNaN(TCPI) || double.IsInfinity(TCPI)) TCPI = 0;
                if (double.IsNaN(EAC) || double.IsInfinity(EAC)) EAC = TotalBAC;
                if (double.IsNaN(CurrentProgress) || double.IsInfinity(CurrentProgress)) CurrentProgress = 0;

                // --- BUILD NEW COLLECTIONS ATOMICALLY ---
                var newChartSeries = new SeriesCollection();
                var newTrendSeries = new SeriesCollection();
                var newProgressSeries = new SeriesCollection();

                // Progress Series (Pie)
                newProgressSeries.Add(new PieSeries
                {
                    Title = "Complete",
                    Values = new ChartValues<double> { CurrentProgress },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SpiColor)),
                    StrokeThickness = 0
                });
                newProgressSeries.Add(new PieSeries
                {
                    Title = "Remaining",
                    Values = new ChartValues<double> { Math.Max(0, 1.0 - CurrentProgress) },
                    Fill = new SolidColorBrush(Color.FromRgb(240, 243, 244)),
                    StrokeThickness = 0
                });

                string unit = DisplayMode == EVMDisplayMode.Hours ? "Hours" :
                  DisplayMode == EVMDisplayMode.Dollars ? "Cost" : "Progress";

                newChartSeries.Add(new LineSeries
                {
                    Title = $"Planned {unit} (BCWS)",
                    Values = bcwsValues,
                    Stroke = Brushes.DodgerBlue,
                    Fill = Brushes.Transparent,
                    PointGeometry = null,
                    StrokeThickness = 2,
                });

                newChartSeries.Add(new LineSeries
                {
                    Title = $"Earned {unit} (BCWP)",
                    Values = bcwpValues,
                    Stroke = Brushes.Black,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8,
                    StrokeThickness = 3,
                });

                newChartSeries.Add(new LineSeries
                {
                    Title = "ACWP (Actuals)",
                    Values = acwpValues,
                    Stroke = Brushes.Crimson,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                });

                if (bcwpValues.Count > 0 && acwpValues.Count > 0)
                {
                    double lastBcwp = bcwpValues.Last();
                    double lastAcwp = acwpValues.Last();

                    if (lastAcwp > 0 && lastBcwp > 0)
                    {
                        double cpi = lastBcwp / lastAcwp;
                        double targetTotal = (DisplayMode == EVMDisplayMode.Percent) ? 1.0 :
                                             (DisplayMode == EVMDisplayMode.Dollars) ? (tasks.Sum(t => t.Work ?? 0.0) * 195.0) :
                                             tasks.Sum(t => t.Work ?? 0.0);
                        double eac = targetTotal / cpi;
                        var forecastValues = new ChartValues<double>();

                        for (int i = 0; i < acwpValues.Count - 1; i++) forecastValues.Add(double.NaN);
                        forecastValues.Add(lastAcwp);

                        int totalPeriods = timePoints.Count;
                        int remainingPeriods = totalPeriods - acwpValues.Count;

                        if (remainingPeriods > 0)
                        {
                            double amountToBurn = eac - lastAcwp;
                            double incrementPerPeriod = amountToBurn / remainingPeriods;
                            for (int i = 1; i <= remainingPeriods; i++) forecastValues.Add(lastAcwp + (incrementPerPeriod * i));
                        }
                        else forecastValues.Add(eac);

                        newChartSeries.Add(new LineSeries
                        {
                            Title = "Forecast (EAC Path)",
                            Values = forecastValues,
                            Stroke = Brushes.Gray,
                            Fill = Brushes.Transparent,
                            StrokeDashArray = new DoubleCollection { 4, 2 },
                            PointGeometry = null,
                            StrokeThickness = 2,
                        });
                    }
                }

                newTrendSeries.Add(new ColumnSeries
                {
                    Title = "SPI (Schedule)",
                    Values = spiTrendValues,
                    Fill = Brushes.CornflowerBlue,
                    MaxColumnWidth = 15,
                });

                newTrendSeries.Add(new LineSeries
                {
                    Title = "CPI (Cost)",
                    Values = cpiTrendValues,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 200, 83)),
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    StrokeThickness = 3,

                });

                // ATOMIC SWAP
                ChartSeries = newChartSeries;
                TrendSeries = newTrendSeries;
                ProgressSeries = newProgressSeries;

                // Notify UI
                OnPropertyChanged(nameof(CurrentSPI));
                OnPropertyChanged(nameof(CurrentCPI));
                OnPropertyChanged(nameof(CurrentScheduleVariance));
                OnPropertyChanged(nameof(CurrentCostVariance));
                OnPropertyChanged(nameof(CurrentProgress));
                OnPropertyChanged(nameof(EAC));
                OnPropertyChanged(nameof(VAC));
                OnPropertyChanged(nameof(TCPI));
                OnPropertyChanged(nameof(TcpiStatus));
                OnPropertyChanged(nameof(TcpiColor));
                OnPropertyChanged(nameof(TotalBAC));
                OnPropertyChanged(nameof(SpiColor));
                OnPropertyChanged(nameof(CpiColor));

                ChartLabels = timePoints.Select(d => d.ToString("MMM dd")).ToArray();

                // Calculate step to avoid overlapping labels (aim for ~10 labels max)
                if (timePoints.Count > 10)
                {
                    LabelsStep = Math.Ceiling(timePoints.Count / 10.0);
                }
                else
                {
                    LabelsStep = 1;
                }
            }));
        }

    }
}

