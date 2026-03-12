using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Services;
using WpfResourceGantt.ProjectManagement.ViewModels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    public class SimulationViewModel : ViewModelBase
    {
        private readonly Dictionary<string, List<SimulationDataPoint>> _simulationProfiles =
            new Dictionary<string, List<SimulationDataPoint>>();
        private readonly MainViewModel _mainViewModel;
        private readonly DataService _dataService;
        private readonly IEvmCalculationService _evmService;
        private readonly IScheduleCalculationService _scheduleService;

        // Stores original task durations from the clone so we can restore them
        // before each recalculation (simulation adjustments are temporary)
        private Dictionary<string, int> _originalDurations = new Dictionary<string, int>();

        // The disconnected sandbox copy
        public ObservableCollection<SystemItem> SimulatedSystems { get; private set; }
        // The embedded read-only Gantt Chart
        public Gantt.GanttViewModel SandboxGanttContext { get; private set; }
        private DateTime _simulatedDate = DateTime.Today;
        public DateTime SimulatedDate
        {
            get => _simulatedDate;
            set
            {
                if (_simulatedDate != value)
                {
                    _simulatedDate = value;
                    OnPropertyChanged();

                    // Push date to Gantt Context
                    if (SandboxGanttContext != null)
                        SandboxGanttContext.SimulatedDate = value;

                    // ──── FIX: SYNC PROGRESS FROM GRAPH PROFILES TO THE NEW DATE ────
                    SyncProfiledItemsProgress();

                    RecalculateSandbox();

                    // Regenerate the interactive timeline so dots automatically extend to the new date
                    if (IsItemSelected)
                    {
                        GenerateInteractiveTimeline(true);
                    }
                }
            }
        }

        private DateTime? _customGraphStartDate;
        public DateTime? CustomGraphStartDate
        {
            get => _customGraphStartDate;
            set
            {
                if (_customGraphStartDate != value)
                {
                    _customGraphStartDate = value;
                    OnPropertyChanged();
                    if (SelectedItem != null) GenerateInteractiveTimeline(true); // Force regenerate
                }
            }
        }

        private DateTime? _customGraphEndDate;
        public DateTime? CustomGraphEndDate
        {
            get => _customGraphEndDate;
            set
            {
                if (_customGraphEndDate != value)
                {
                    _customGraphEndDate = value;
                    OnPropertyChanged();
                    if (SelectedItem != null) GenerateInteractiveTimeline(true); // Force regenerate
                }
            }
        }

        private WorkBreakdownItem _selectedItem;
        public WorkBreakdownItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;

                // Reset the custom view range so new items auto-fit to their data initially
                _customGraphStartDate = null;
                _customGraphEndDate = null;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomGraphStartDate));
                OnPropertyChanged(nameof(CustomGraphEndDate));
                OnPropertyChanged(nameof(IsItemSelected));

                GenerateInteractiveTimeline();
            }
        }
        public bool IsItemSelected => SelectedItem != null;

        public ObservableCollection<SimulationDataPoint> InteractiveDataPoints { get; } = new ObservableCollection<SimulationDataPoint>();

        private GraphEditMode _currentEditMode = GraphEditMode.Progress;
        public GraphEditMode CurrentEditMode
        {
            get => _currentEditMode;
            set { _currentEditMode = value; OnPropertyChanged(); }
        }

        public ICommand ToggleEditModeCommand { get; }


        // Live vs Simulated Metrics
        public double LiveTotalBcws { get; private set; }
        public double LiveTotalBcwp { get; private set; }
        public double LiveTotalSv => LiveTotalBcwp - LiveTotalBcws;

        // NEW Live Properties
        public double LiveTotalAcwp { get; private set; }
        public double LiveTotalCv => LiveTotalBcwp - LiveTotalAcwp;
        public double LiveTotalSpi => LiveTotalBcws != 0 ? Math.Round(LiveTotalBcwp / LiveTotalBcws, 2) : 0;
        public double LiveTotalCpi => LiveTotalAcwp != 0 ? Math.Round(LiveTotalBcwp / LiveTotalAcwp, 2) : 0;
        public double LiveTotalBac { get; private set; }
        public double LiveTotalEac => LiveTotalCpi != 0 ? Math.Round(LiveTotalBac / LiveTotalCpi, 2) : LiveTotalBac;
        public double LiveTotalTcpi { get; private set; }

        private double _simTotalBcws;
        public double SimTotalBcws { get => _simTotalBcws; set { _simTotalBcws = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalSv)); OnPropertyChanged(nameof(ImpactSv)); OnPropertyChanged(nameof(SimTotalSpi)); OnPropertyChanged(nameof(ImpactSpi)); } }

        private double _simTotalBcwp;
        public double SimTotalBcwp { get => _simTotalBcwp; set { _simTotalBcwp = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalSv)); OnPropertyChanged(nameof(ImpactSv)); OnPropertyChanged(nameof(SimTotalSpi)); OnPropertyChanged(nameof(ImpactSpi)); OnPropertyChanged(nameof(SimTotalCpi)); OnPropertyChanged(nameof(ImpactCpi)); OnPropertyChanged(nameof(SimTotalEac)); OnPropertyChanged(nameof(ImpactEac)); OnPropertyChanged(nameof(SimTotalTcpi)); } }

        public double SimTotalSv => SimTotalBcwp - SimTotalBcws;
        public double ImpactSv => SimTotalSv - LiveTotalSv;

        // NEW Simulated Properties
        private double _simTotalAcwp;
        public double SimTotalAcwp { get => _simTotalAcwp; set { _simTotalAcwp = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalCv)); OnPropertyChanged(nameof(ImpactCv)); OnPropertyChanged(nameof(SimTotalCpi)); OnPropertyChanged(nameof(ImpactCpi)); OnPropertyChanged(nameof(SimTotalEac)); OnPropertyChanged(nameof(ImpactEac)); OnPropertyChanged(nameof(SimTotalTcpi)); } }

        public double SimTotalCv => SimTotalBcwp - SimTotalAcwp;
        public double SimTotalSpi => SimTotalBcws != 0 ? Math.Round(SimTotalBcwp / SimTotalBcws, 2) : 0;
        public double SimTotalCpi => SimTotalAcwp != 0 ? Math.Round(SimTotalBcwp / SimTotalAcwp, 2) : 0;

        private double _simTotalBac;
        public double SimTotalBac { get => _simTotalBac; set { _simTotalBac = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalEac)); OnPropertyChanged(nameof(ImpactEac)); OnPropertyChanged(nameof(SimTotalTcpi)); } }

        public double SimTotalEac => SimTotalCpi != 0 ? Math.Round(SimTotalBac / SimTotalCpi, 2) : SimTotalBac;

        private double _simTotalTcpi;
        public double SimTotalTcpi { get => _simTotalTcpi; set { _simTotalTcpi = value; OnPropertyChanged(); } }

        // NEW Impact (Delta) Properties
        public double ImpactCv => SimTotalCv - LiveTotalCv;
        public double ImpactSpi => Math.Round(SimTotalSpi - LiveTotalSpi, 2);
        public double ImpactCpi => Math.Round(SimTotalCpi - LiveTotalCpi, 2);
        public double ImpactEac => SimTotalEac - LiveTotalEac;

        // --- Critical Path Shift Detection ---
        private string _criticalPathAlert;
        public string CriticalPathAlert
        {
            get => _criticalPathAlert;
            set { _criticalPathAlert = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCriticalPathAlert)); }
        }
        public bool HasCriticalPathAlert => !string.IsNullOrEmpty(CriticalPathAlert);

        private int _criticalPathCount;
        public int CriticalPathCount
        {
            get => _criticalPathCount;
            set { _criticalPathCount = value; OnPropertyChanged(); }
        }

        // --- Stress Test Scenarios ---
        public ObservableCollection<StressTestScenario> StressTestScenarios { get; } = new ObservableCollection<StressTestScenario>
        {
            new StressTestScenario { Id = "cp_slip",    Name = "Critical Path Slip (+2 Weeks)",   Icon = "⚠", Description = "Adds 10 business days to all critical path task durations" },
            new StressTestScenario { Id = "res_loss",   Name = "Resource Loss (20% Reduction)",   Icon = "👤", Description = "Reduces progress by 20% on all in-progress tasks" },
            new StressTestScenario { Id = "flat_30",    Name = "Flat Progress (30 Days)",         Icon = "📉", Description = "Advances the date by 30 days with zero progress" },
            new StressTestScenario { Id = "recovery",   Name = "Optimistic Recovery",             Icon = "🚀", Description = "Sets all behind-schedule tasks to their expected progress" },
            new StressTestScenario { Id = "shutdown",   Name = "Government Shutdown (15 Days)",   Icon = "🏛", Description = "Extends all incomplete task durations by 15 business days" },
        };

        private StressTestScenario _selectedScenario;
        public StressTestScenario SelectedScenario
        {
            get => _selectedScenario;
            set { _selectedScenario = value; OnPropertyChanged(); }
        }

        public ICommand ResetSimulationCommand { get; }
        public ICommand FlatlineFutureCommand { get; }
        public ICommand RecalculateCommand { get; }
        public ICommand ExecuteScenarioCommand { get; }

        public SimulationViewModel(MainViewModel mainViewModel, DataService dataService)
        {
            _mainViewModel = mainViewModel;
            _dataService = dataService;
            _evmService = new EvmCalculationService(); // Instantiate clean engines for the sandbox
            _scheduleService = new ScheduleCalculationService();

            ResetSimulationCommand = new RelayCommand(InitializeSandbox);
            FlatlineFutureCommand = new RelayCommand(ExecuteFlatline);
            RecalculateCommand = new RelayCommand(RecalculateSandbox);
            ExecuteScenarioCommand = new RelayCommand(RunSelectedScenario);
            ToggleEditModeCommand = new RelayCommand(() =>
            {
                CurrentEditMode = CurrentEditMode == GraphEditMode.Progress ? GraphEditMode.ActualHours : GraphEditMode.Progress;
            });
            InitializeSandbox();
        }

        private void InitializeSandbox()
        {
            // 1. Calculate Live Baselines for comparison
            var liveSystems = _dataService.AllSystems.ToList();
            LiveTotalBcws = Math.Round(liveSystems.Sum(s => s.Children?.Sum(c => c.Bcws ?? 0) ?? 0), 2);
            LiveTotalBcwp = Math.Round(liveSystems.Sum(s => s.Children?.Sum(c => c.Bcwp ?? 0) ?? 0), 2);
            LiveTotalAcwp = Math.Round(liveSystems.Sum(s => s.Children?.Sum(c => c.Acwp ?? 0) ?? 0), 2);
            LiveTotalBac = Math.Round(liveSystems.Sum(s => s.Children?.Sum(c => (double)(c.BAC ?? 0)) ?? 0), 2);

            // Compute Live TCPI
            double liveDenominator = LiveTotalBac - LiveTotalAcwp;
            LiveTotalTcpi = liveDenominator > 0 ? Math.Round((LiveTotalBac - LiveTotalBcwp) / liveDenominator, 2) : double.PositiveInfinity;

            // 2. Clone the entire hierarchy
            var clonedList = CloneHelper.DeepClone(liveSystems);
            SimulatedSystems = new ObservableCollection<SystemItem>(clonedList);
            OnPropertyChanged(nameof(SimulatedSystems));
            // --- INJECT CLONED DATA INTO GANTT CHART ---
            SandboxGanttContext = new Gantt.GanttViewModel(_mainViewModel, _dataService, SimulatedSystems);
            OnPropertyChanged(nameof(SandboxGanttContext));

            // 3. Capture original durations from the fresh clone (used for restore before each recalculation)
            _originalDurations = CaptureOriginalDurations(SimulatedSystems);

            // 4. Reset Time
            _simulationProfiles.Clear();
            SimulatedDate = DateTime.Today;

            // Clear critical path alert on reset
            CriticalPathAlert = null;
            CriticalPathCount = 0;

            OnPropertyChanged(nameof(LiveTotalBcws));
            OnPropertyChanged(nameof(LiveTotalBcwp));
            OnPropertyChanged(nameof(LiveTotalSv));
            OnPropertyChanged(nameof(LiveTotalAcwp));
            OnPropertyChanged(nameof(LiveTotalCv));
            OnPropertyChanged(nameof(LiveTotalSpi));
            OnPropertyChanged(nameof(LiveTotalCpi));
            OnPropertyChanged(nameof(LiveTotalBac));
            OnPropertyChanged(nameof(LiveTotalEac));
            OnPropertyChanged(nameof(LiveTotalTcpi));
        }

        private void RecalculateSandbox()
        {
            if (SimulatedSystems == null) return;

            // STEP 1: Capture BEFORE snapshot of critical path
            var beforeCriticalIds = GetAllCriticalLeafIds(SimulatedSystems);

            // STEP 2: SIMULATION-AWARE SCHEDULING
            // Restore original durations first (undo any previous adjustments)
            RestoreOriginalDurations(SimulatedSystems, _originalDurations);
            // Apply simulation adjustments: completed tasks shrink, overdue tasks extend
            AdjustDurationsForSimulation(SimulatedSystems);

            // STEP 3: Run the schedule and EVM engines with adjusted durations
            _scheduleService.CalculateSchedule(SimulatedSystems, SimulatedDate);
            foreach (var system in SimulatedSystems)
            {
                system.RecalculateRollup(SimulatedDate);
            }
            _evmService.RecalculateAll(SimulatedSystems, SimulatedDate);
            // --- FORCE THE GANTT CHART TO REDRAW WITH NEW DATES ---
            SandboxGanttContext?.RefreshAndPreserveState();

            // STEP 4: Capture AFTER snapshot and detect critical path shifts
            var afterCriticalIds = GetAllCriticalLeafIds(SimulatedSystems);
            DetectCriticalPathChanges(beforeCriticalIds, afterCriticalIds);
            CriticalPathCount = afterCriticalIds.Count;

            // Update Sandbox Metrics
            SimTotalBcws = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => c.Bcws ?? 0) ?? 0), 2);
            SimTotalBcwp = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => c.Bcwp ?? 0) ?? 0), 2);
            SimTotalAcwp = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => c.Acwp ?? 0) ?? 0), 2);
            SimTotalBac = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => (double)(c.BAC ?? 0)) ?? 0), 2);

            // Compute Sim TCPI
            double simDenominator = SimTotalBac - SimTotalAcwp;
            SimTotalTcpi = simDenominator > 0 ? Math.Round((SimTotalBac - SimTotalBcwp) / simDenominator, 2) : double.PositiveInfinity;
        }
        private void GenerateInteractiveTimeline(bool forceRegenerate = false)
        {
            // 1. Unsubscribe current points
            foreach (var p in InteractiveDataPoints) p.PropertyChanged -= DataPoint_PropertyChanged;
            InteractiveDataPoints.Clear();

            if (SelectedItem == null || !SelectedItem.StartDate.HasValue) return;

            List<SimulationDataPoint> oldProfile = null;

            // 2. CHECK CACHE: Do we already have a profile for this item?
            if (_simulationProfiles.ContainsKey(SelectedItem.Id))
            {
                if (!forceRegenerate)
                {
                    var cachedPoints = _simulationProfiles[SelectedItem.Id];
                    foreach (var p in cachedPoints)
                    {
                        p.PropertyChanged += DataPoint_PropertyChanged;
                        InteractiveDataPoints.Add(p);
                    }
                    return; // Exit early, we restored the curve
                }
                else
                {
                    // Save the old profile so we can interpolate the user's manual drags into the new dot density
                    oldProfile = _simulationProfiles[SelectedItem.Id];
                }
            }

            // 3. GENERATE NEW:
            // Bound the timeline explicitly to the user's custom dates if they provided them
            DateTime start = CustomGraphStartDate ?? SelectedItem.StartDate.Value;
            DateTime plannedEnd = CustomGraphEndDate ?? SelectedItem.EndDate.GetValueOrDefault(SelectedItem.StartDate.Value.AddMonths(1));

            DateTime end = plannedEnd;

            var sortedHistory = SelectedItem.ProgressHistory?
                                .OrderBy(h => h.Date)
                                .ToList() ?? new List<ProgressHistoryItem>();

            DateTime latestHistoryDate = sortedHistory.Any() ? sortedHistory.Last().Date : DateTime.MinValue;

            // Only auto-extend the graph if the user hasn't explicitly locked the End Date
            if (!CustomGraphEndDate.HasValue)
            {
                // Always ensure dots are available to manipulate all the way up to the simulated date
                if (SimulatedDate > end)
                    end = SimulatedDate;

                // Also extend to the actual completion date if it finished later than planned
                if (SelectedItem.ActualFinishDate.HasValue && SelectedItem.ActualFinishDate.Value > end)
                    end = SelectedItem.ActualFinishDate.Value;

                if (latestHistoryDate > end)
                    end = latestHistoryDate;
            }

            // Failsafe against backwards date entry
            if (start >= end) end = start.AddDays(1);

            var newPoints = new List<SimulationDataPoint>();
            int pointIndex = 1;
            DateTime current = start;

            // Determine interval based on the CURRENT VIEW bounds, not the whole project
            double totalGraphDays = (end - start).TotalDays;
            int stepDays = 7;

            if (totalGraphDays > 365 * 2) stepDays = 90;
            else if (totalGraphDays > 365) stepDays = 30;
            else if (totalGraphDays > 180) stepDays = 14;
            else if (totalGraphDays <= 30) stepDays = 1;       // Zoomed in tight -> Daily dots

            Func<DateTime, (double Progress, double Hours)> calculateMetricsAt = (date) =>
            {
                // 1st Priority: If we are regenerating, sample the old curve so user tweaks survive the zoom!
                if (oldProfile != null && oldProfile.Any())
                {
                    double prog = GetInterpolatedValue(oldProfile, date, p => p.Progress);
                    double hrs = GetInterpolatedValue(oldProfile, date, p => p.ActualHours);
                    return (prog, hrs);
                }

                // 2nd Priority: History Logs
                if (sortedHistory.Count > 0)
                {
                    if (sortedHistory[sortedHistory.Count - 1].Date <= date)
                        return (SelectedItem.Progress, SelectedItem.ActualWork ?? 0);
                    else if (sortedHistory[0].Date > date)
                        return (sortedHistory[0].ActualProgress, sortedHistory[0].ActualWork ?? 0);
                    else
                    {
                        for (int k = sortedHistory.Count - 1; k >= 0; k--)
                        {
                            if (sortedHistory[k].Date <= date)
                                return (sortedHistory[k].ActualProgress, sortedHistory[k].ActualWork ?? 0);
                        }
                    }
                }

                // 3rd Priority: Base Ramp-up
                DateTime targetEndDate = (SelectedItem.Progress >= 0.99)
                    ? (SelectedItem.ActualFinishDate ?? plannedEnd) : SimulatedDate;

                if (date < start) return (0, 0);
                if (date >= targetEndDate) return (SelectedItem.Progress, SelectedItem.ActualWork ?? 0);

                double totalDays = (targetEndDate - SelectedItem.StartDate.Value).TotalDays;
                if (totalDays <= 0) return (SelectedItem.Progress, SelectedItem.ActualWork ?? 0);

                double elapsed = (date - SelectedItem.StartDate.Value).TotalDays;
                double inferredProg = Math.Max(0, Math.Min(SelectedItem.Progress, SelectedItem.Progress * (elapsed / totalDays)));

                double inferredHours = SelectedItem.Progress > 0
                    ? (SelectedItem.ActualWork ?? 0) * (inferredProg / SelectedItem.Progress) : 0;

                return (inferredProg, inferredHours);
            };

            while (current < end)
            {
                var metrics = calculateMetricsAt(current);
                newPoints.Add(new SimulationDataPoint
                {
                    WeekNumber = pointIndex++,
                    Date = current,
                    Progress = Math.Round(metrics.Progress, 3),
                    ActualHours = Math.Round(metrics.Hours, 1)
                });

                if (stepDays == 30) current = current.AddMonths(1);
                else if (stepDays == 90) current = current.AddMonths(3);
                else current = current.AddDays(stepDays);
            }

            if (!newPoints.Any())
            {
                var metrics = calculateMetricsAt(start);
                newPoints.Add(new SimulationDataPoint
                {
                    WeekNumber = pointIndex++,
                    Date = start,
                    Progress = Math.Round(metrics.Progress, 3),
                    ActualHours = Math.Round(metrics.Hours, 1)
                });
            }

            if (newPoints.Last().Date < end)
            {
                var metrics = calculateMetricsAt(end);
                newPoints.Add(new SimulationDataPoint
                {
                    WeekNumber = pointIndex++,
                    Date = end,
                    Progress = Math.Round(metrics.Progress, 3),
                    ActualHours = Math.Round(metrics.Hours, 1)
                });
            }

            // Save to cache and populate UI
            _simulationProfiles[SelectedItem.Id] = newPoints;
            foreach (var p in newPoints)
            {
                p.PropertyChanged += DataPoint_PropertyChanged;
                InteractiveDataPoints.Add(p);
            }
        }
        private void DataPoint_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (SelectedItem == null || !InteractiveDataPoints.Any()) return;

            var changedPoint = sender as SimulationDataPoint;
            if (changedPoint == null) return;

            int changedIndex = InteractiveDataPoints.IndexOf(changedPoint);

            if (e.PropertyName == nameof(SimulationDataPoint.Progress))
            {
                // ──── ENFORCE MONOTONICITY FORWARD ────
                // When moving a dot UP, push future dots UP to maintain the floor.
                // When moving a dot DOWN, future dots stay where they are (retaining their positions).
                for (int i = changedIndex + 1; i < InteractiveDataPoints.Count; i++)
                {
                    if (InteractiveDataPoints[i].Progress < changedPoint.Progress)
                    {
                        InteractiveDataPoints[i].PropertyChanged -= DataPoint_PropertyChanged;
                        InteractiveDataPoints[i].Progress = changedPoint.Progress;
                        InteractiveDataPoints[i].PropertyChanged += DataPoint_PropertyChanged;
                    }
                    else break; // Successor is already higher than or equal to current point
                }

                // ──── ENFORCE MONOTONICITY BACKWARD ────
                // When moving a dot DOWN, pull previous dots DOWN to maintain the ceiling.
                for (int i = changedIndex - 1; i >= 0; i--)
                {
                    if (InteractiveDataPoints[i].Progress > changedPoint.Progress)
                    {
                        InteractiveDataPoints[i].PropertyChanged -= DataPoint_PropertyChanged;
                        InteractiveDataPoints[i].Progress = changedPoint.Progress;
                        InteractiveDataPoints[i].PropertyChanged += DataPoint_PropertyChanged;
                    }
                    else break; // Predecessor is already lower than or equal to current point
                }

                // ──── APPLY TO MODEL ────
                // Read the value at the simulated status date
                var relevantPoint = InteractiveDataPoints
                .Where(p => p.Date <= SimulatedDate)
                .OrderByDescending(p => p.Date)
                .FirstOrDefault() ?? InteractiveDataPoints.First();

                SelectedItem.Progress = GetInterpolatedValue(InteractiveDataPoints, SimulatedDate, p => p.Progress);

                // ──── SET / CLEAR ACTUAL FINISH DATE ────
                // Find the earliest dot where progress first hits 100%
                var completionPoint = InteractiveDataPoints
                     .FirstOrDefault(p => p.Progress >= 1.0);

                if (completionPoint != null && SelectedItem.Progress >= 1.0)
                {
                    // Task is complete at the simulated date — lock finish date
                    SelectedItem.ActualFinishDate = completionPoint.Date;
                }
                else
                {
                    // Task is NOT complete at the simulated date — clear finish date
                    SelectedItem.ActualFinishDate = null;
                }
            }
            else if (e.PropertyName == nameof(SimulationDataPoint.ActualHours))
            {
                // ──── ENFORCE MONOTONICITY FORWARD (Hours) ────
                for (int i = changedIndex + 1; i < InteractiveDataPoints.Count; i++)
                {
                    if (InteractiveDataPoints[i].ActualHours < changedPoint.ActualHours)
                    {
                        InteractiveDataPoints[i].PropertyChanged -= DataPoint_PropertyChanged;
                        InteractiveDataPoints[i].ActualHours = changedPoint.ActualHours;
                        InteractiveDataPoints[i].PropertyChanged += DataPoint_PropertyChanged;
                    }
                    else break;
                }

                // ──── ENFORCE MONOTONICITY BACKWARD (Hours) ────
                for (int i = changedIndex - 1; i >= 0; i--)
                {
                    if (InteractiveDataPoints[i].ActualHours > changedPoint.ActualHours)
                    {
                        InteractiveDataPoints[i].PropertyChanged -= DataPoint_PropertyChanged;
                        InteractiveDataPoints[i].ActualHours = changedPoint.ActualHours;
                        InteractiveDataPoints[i].PropertyChanged += DataPoint_PropertyChanged;
                    }
                    else break;
                }

                var relevantPoint = InteractiveDataPoints
                .Where(p => p.Date <= SimulatedDate)
                .OrderByDescending(p => p.Date)
                .FirstOrDefault() ?? InteractiveDataPoints.First();

                double interpolatedHours = GetInterpolatedValue(InteractiveDataPoints, SimulatedDate, p => p.ActualHours);
                SelectedItem.ActualWork = interpolatedHours;
                SelectedItem.Acwp = interpolatedHours * 195.0;
            }

            RecalculateSandbox();
        }

        private void ExecuteFlatline()
        {
            if (SelectedItem == null || !InteractiveDataPoints.Any()) return;

            // Scenario: 3 weeks flatline. Visually drop the final dots to 0 progress.
            // This triggers the DataPoint_PropertyChanged loop which handles the rest.
            foreach (var point in InteractiveDataPoints.Skip(Math.Max(0, InteractiveDataPoints.Count - 3)))
            {
                point.Progress = 0;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  STRESS TEST SCENARIO ENGINE
        // ═══════════════════════════════════════════════════════════

        private void RunSelectedScenario()
        {
            if (SelectedScenario == null || SimulatedSystems == null) return;

            switch (SelectedScenario.Id)
            {
                case "cp_slip": ExecuteCriticalPathSlip(); break;
                case "res_loss": ExecuteResourceLoss(); break;
                case "flat_30": ExecuteFlatProgress30Days(); break;
                case "recovery": ExecuteOptimisticRecovery(); break;
                case "shutdown": ExecuteGovernmentShutdown(); break;
            }
        }

        /// <summary>
        /// Critical Path Slip (+2 Weeks): Adds 10 business days to the original duration
        /// of every critical path leaf task. The schedule engine then ripples this delay
        /// through all successors.
        /// </summary>
        private void ExecuteCriticalPathSlip()
        {
            foreach (var system in SimulatedSystems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    SlipCriticalLeaves(child, 10);
            }
            RecalculateSandbox();
        }

        private void SlipCriticalLeaves(WorkBreakdownItem item, int additionalDays)
        {
            if (item.Children == null || !item.Children.Any())
            {
                // Only slip incomplete critical tasks — finished tasks don't need extension
                if (item.IsCritical && item.Progress < 1.0 && _originalDurations.ContainsKey(item.Id))
                    _originalDurations[item.Id] += additionalDays;
                return;
            }
            foreach (var child in item.Children)
                SlipCriticalLeaves(child, additionalDays);
        }

        /// <summary>
        /// Resource Loss (20%): Reduces progress by 20% on all in-progress leaf tasks.
        /// Simulates losing a fifth of the workforce. Behind-schedule tasks fall further behind,
        /// causing EVM metrics to degrade and potentially shifting the critical path.
        /// </summary>
        private void ExecuteResourceLoss()
        {
            double scaleFactor = 1.0 - 0.20; // 0.80

            // 1. Reduce progress on all in-progress leaf tasks in the data model
            foreach (var system in SimulatedSystems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    ReduceProgressOnLeaves(child, 0.20);
            }

            // 2. Scale FUTURE dots in cached profiles (past progress is locked — can't un-do work)
            // e.g. SimDate at week 3: [0%, 10%, 20%, 20%, 20%] → [0%, 10%, 20%, 16%, 16%]
            ScaleFutureCachedProfiles(scaleFactor);

            // NOTE: Do NOT call SyncProfiledItemsProgress here — ReduceProgressOnLeaves
            // already set item.Progress correctly. The sync would overwrite it with the
            // locked dot's value (which wasn't scaled), undoing the reduction.

            // 3. Force the graph to refresh visually
            GenerateInteractiveTimeline();

            RecalculateSandbox();
        }

        /// <summary>
        /// Scales cached profile dots that are at or after the SimulatedDate.
        /// Past dots (before SimulatedDate) are locked — work already completed can't be undone.
        /// </summary>
        private void ScaleFutureCachedProfiles(double scaleFactor)
        {
            foreach (var profile in _simulationProfiles.Values)
            {
                foreach (var point in profile)
                {
                    // Past progress is locked — dot at SimulatedDate is already-reported work
                    if (point.Date <= SimulatedDate) continue;

                    point.PropertyChanged -= DataPoint_PropertyChanged;
                    point.Progress = Math.Round(point.Progress * scaleFactor, 3);
                    point.ActualHours = Math.Round(point.ActualHours * scaleFactor, 1);
                    point.PropertyChanged += DataPoint_PropertyChanged;
                }
            }
        }

        private void ReduceProgressOnLeaves(WorkBreakdownItem item, double reductionFactor)
        {
            if (item.Children == null || !item.Children.Any())
            {
                if (item.Progress > 0 && item.Progress < 1.0)
                {
                    item.Progress = Math.Round(item.Progress * (1.0 - reductionFactor), 3);
                    item.ActualFinishDate = null; // No longer complete
                }
                return;
            }
            foreach (var child in item.Children)
                ReduceProgressOnLeaves(child, reductionFactor);
        }

        /// <summary>
        /// Flat Progress (30 Days): Freezes all graph profiles at current progress,
        /// then advances the SimulatedDate by 30 calendar days. Completed work stays,
        /// but no new work happens — tasks become overdue and impact metrics degrade.
        /// </summary>
        private void ExecuteFlatProgress30Days()
        {
            // 1. Freeze all future dots at their current value (no work happens)
            FreezeProgressInProfiles();

            // 2. Sync item.Progress from frozen profile dots
            SyncProfiledItemsProgress();

            // 3. Refresh graph to show the flat line
            GenerateInteractiveTimeline();

            // 4. Advance the clock — overdue detection extends durations
            SimulatedDate = SimulatedDate.AddDays(30);
        }

        /// <summary>
        /// Optimistic Recovery: For all behind-schedule leaf tasks, bumps progress
        /// to the expected % based on elapsed time. Simulates a "best case" catch-up
        /// where every task is exactly on track.
        /// </summary>
        private void ExecuteOptimisticRecovery()
        {
            var recoveredIds = new HashSet<string>();
            foreach (var system in SimulatedSystems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    RecoverBehindScheduleLeaves(child, recoveredIds);
            }

            // Only clear profiles for items whose progress was actually changed.
            // For Recovery, a flat line at the expected % IS correct — it means "caught up."
            foreach (var id in recoveredIds)
                _simulationProfiles.Remove(id);

            // Always refresh the graph to reflect changes
            GenerateInteractiveTimeline();

            RecalculateSandbox();
        }

        private void RecoverBehindScheduleLeaves(WorkBreakdownItem item, HashSet<string> recoveredIds)
        {
            if (item.Children == null || !item.Children.Any())
            {
                if (item.StartDate.HasValue && item.EndDate.HasValue && item.Progress < 1.0)
                {
                    int totalDays = WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate.Value, item.EndDate.Value);
                    int elapsedDays = WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate.Value, SimulatedDate);

                    double expectedProgress = totalDays > 0 ? Math.Min(1.0, (double)elapsedDays / totalDays) : 0;

                    if (item.Progress < expectedProgress)
                    {
                        item.Progress = Math.Round(expectedProgress, 3);
                        if (item.Progress >= 1.0)
                            item.ActualFinishDate = item.EndDate; // Finished exactly on time
                        recoveredIds.Add(item.Id);
                    }
                }
                return;
            }
            foreach (var child in item.Children)
                RecoverBehindScheduleLeaves(child, recoveredIds);
        }

        /// <summary>
        /// Government Shutdown (15 Days): Freezes all graph profiles, extends the
        /// original duration of ALL incomplete leaf tasks by 15 business days,
        /// then advances the clock by 15 business days. Full work stoppage.
        /// </summary>
        private void ExecuteGovernmentShutdown()
        {
            // 1. Freeze all future dots — no work happens during the shutdown
            FreezeProgressInProfiles();

            // 2. Extend all incomplete task durations by 15 business days
            foreach (var system in SimulatedSystems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    AddShutdownDelay(child, 15);
            }

            // 3. Sync item.Progress from frozen profile dots
            SyncProfiledItemsProgress();

            // 4. Refresh graph to show the flat line
            GenerateInteractiveTimeline();

            // 5. Advance the clock — triggers overdue detection and shows full EVM impact
            SimulatedDate = WorkBreakdownItem.AddBusinessDays(SimulatedDate, 15);
        }

        /// <summary>
        /// Freezes all future dots in every cached graph profile at their current value.
        /// For each profile, finds the last dot before SimulatedDate and sets all dots
        /// from SimulatedDate onward to that value — creating a visible flat line.
        /// </summary>
        private void FreezeProgressInProfiles()
        {
            foreach (var profile in _simulationProfiles.Values)
            {
                // Find the last known progress at or before the simulated date
                // (dot AT SimulatedDate = already-reported work, must be locked)
                var referencePoint = profile
                    .Where(p => p.Date <= SimulatedDate)
                    .OrderByDescending(p => p.Date)
                    .FirstOrDefault();

                double frozenProgress = referencePoint?.Progress ?? profile.First().Progress;
                double frozenHours = referencePoint?.ActualHours ?? profile.First().ActualHours;

                // Set all FUTURE dots to the frozen value
                foreach (var point in profile)
                {
                    if (point.Date <= SimulatedDate) continue;

                    point.PropertyChanged -= DataPoint_PropertyChanged;
                    point.Progress = frozenProgress;
                    point.ActualHours = frozenHours;
                    point.PropertyChanged += DataPoint_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// After any scenario modifies profile dots, this syncs each profiled item's
        /// data model (Progress, ActualWork, ActualFinishDate) from the relevant dot
        /// at SimulatedDate. Without this, the Gantt would show stale values.
        /// </summary>
        private void SyncProfiledItemsProgress()
        {
            foreach (var kvp in _simulationProfiles)
            {
                string itemId = kvp.Key;
                var profile = kvp.Value;

                // Find the item in the hierarchy
                var item = FindLeafItemById(SimulatedSystems, itemId);
                if (item == null) continue;

                // Use Interpolation to get exact progress at the new simulated date
                item.Progress = GetInterpolatedValue(profile, SimulatedDate, p => p.Progress);
                double interpolatedHours = GetInterpolatedValue(profile, SimulatedDate, p => p.ActualHours);
                item.ActualWork = interpolatedHours;
                item.Acwp = interpolatedHours * 195.0;

                // Update ActualFinishDate
                if (item.Progress >= 1.0)
                {
                    var completionPoint = profile.FirstOrDefault(p => p.Progress >= 1.0);
                    item.ActualFinishDate = completionPoint?.Date;
                }
                else
                {
                    item.ActualFinishDate = null;
                }
            }
        }
        private double GetInterpolatedValue(IEnumerable<SimulationDataPoint> points, DateTime targetDate, Func<SimulationDataPoint, double> valueSelector)
        {
            var sorted = points.OrderBy(p => p.Date).ToList();
            if (!sorted.Any()) return 0;

            if (targetDate <= sorted.First().Date) return valueSelector(sorted.First());
            if (targetDate >= sorted.Last().Date) return valueSelector(sorted.Last());

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var p1 = sorted[i];
                var p2 = sorted[i + 1];

                if (targetDate >= p1.Date && targetDate <= p2.Date)
                {
                    // ──── BUSINESS-DAY AWARE INTERPOLATION ────
                    // Map the calendar segment to business day progression
                    int segmentBusinessDays = WorkBreakdownItem.GetBusinessDaysSpan(p1.Date, p2.Date);
                    int elapsedBusinessDays = WorkBreakdownItem.GetBusinessDaysSpan(p1.Date, targetDate);

                    double t;
                    if (segmentBusinessDays > 0)
                    {
                        t = (double)elapsedBusinessDays / segmentBusinessDays;
                    }
                    else
                    {
                        // Weekend segment: progress is stagnant (0.0) or complete (1.0)
                        t = targetDate >= p2.Date ? 1.0 : 0.0;
                    }

                    double v1 = valueSelector(p1);
                    double v2 = valueSelector(p2);

                    return Math.Round(v1 + (v2 - v1) * t, 3);
                }
            }
            return valueSelector(sorted.Last());
        }

        private WorkBreakdownItem FindLeafItemById(IEnumerable<SystemItem> systems, string id)
        {
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                {
                    var found = FindLeafRecursive(child, id);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private WorkBreakdownItem FindLeafRecursive(WorkBreakdownItem item, string id)
        {
            if (item.Id == id) return item;
            if (item.Children == null) return null;
            foreach (var child in item.Children)
            {
                var found = FindLeafRecursive(child, id);
                if (found != null) return found;
            }
            return null;
        }

        private void AddShutdownDelay(WorkBreakdownItem item, int shutdownDays)
        {
            if (item.Children == null || !item.Children.Any())
            {
                // Only delay tasks that haven't finished yet
                if (item.Progress < 1.0 && _originalDurations.ContainsKey(item.Id))
                    _originalDurations[item.Id] += shutdownDays;
                return;
            }
            foreach (var child in item.Children)
                AddShutdownDelay(child, shutdownDays);
        }

        // --- Simulation-Aware Duration Adjustment ---

        private Dictionary<string, int> CaptureOriginalDurations(IEnumerable<SystemItem> systems)
        {
            var originals = new Dictionary<string, int>();
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    CaptureOriginalDurationsRecursive(child, originals);
            }
            return originals;
        }

        private void CaptureOriginalDurationsRecursive(WorkBreakdownItem item, Dictionary<string, int> originals)
        {
            if (item.Children == null || !item.Children.Any())
            {
                originals[item.Id] = item.DurationDays;
                return;
            }
            foreach (var child in item.Children)
                CaptureOriginalDurationsRecursive(child, originals);
        }

        private void RestoreOriginalDurations(IEnumerable<SystemItem> systems, Dictionary<string, int> originals)
        {
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    RestoreOriginalDurationsRecursive(child, originals);
            }
        }

        private void RestoreOriginalDurationsRecursive(WorkBreakdownItem item, Dictionary<string, int> originals)
        {
            if (item.Children == null || !item.Children.Any())
            {
                if (originals.TryGetValue(item.Id, out int originalDuration))
                    item.DurationDays = originalDuration;
                return;
            }
            foreach (var child in item.Children)
                RestoreOriginalDurationsRecursive(child, originals);
        }

        private void AdjustDurationsForSimulation(IEnumerable<SystemItem> systems)
        {
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    AdjustDurationsRecursive(child);
            }
        }

        private void AdjustDurationsRecursive(WorkBreakdownItem item)
        {
            if (item.Children == null || !item.Children.Any())
            {
                // LEAF TASK: Adjust duration based on progress status
                if (item.Progress >= 1.0 && item.ActualFinishDate.HasValue && item.StartDate.HasValue)
                {
                    // COMPLETED EARLY: Shrink duration so EndDate = ActualFinishDate
                    // This frees successors to start sooner.
                    int actualDuration = WorkBreakdownItem.GetBusinessDaysSpan(
                        item.StartDate.Value, item.ActualFinishDate.Value);
                    if (actualDuration > 0)
                        item.DurationDays = actualDuration;
                }
                else if (item.Progress < 1.0 && item.EndDate.HasValue
                         && SimulatedDate > item.EndDate.Value && item.StartDate.HasValue)
                {
                    // OVERDUE & INCOMPLETE: Extend duration to reach SimulatedDate
                    // The task is still in progress, so its end date can't be in the past.
                    // This delays all successors realistically.
                    int overdueDuration = WorkBreakdownItem.GetBusinessDaysSpan(
                        item.StartDate.Value, SimulatedDate);
                    if (overdueDuration > item.DurationDays)
                        item.DurationDays = overdueDuration;
                }
                return;
            }
            foreach (var child in item.Children)
                AdjustDurationsRecursive(child);
        }

        // --- Critical Path Detection Helpers ---

        private HashSet<string> GetAllCriticalLeafIds(IEnumerable<SystemItem> systems)
        {
            var ids = new HashSet<string>();
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                    CollectCriticalIds(child, ids);
            }
            return ids;
        }

        private void CollectCriticalIds(WorkBreakdownItem item, HashSet<string> ids)
        {
            if (item.Children == null || !item.Children.Any())
            {
                if (item.IsCritical) ids.Add(item.Id);
                return;
            }
            foreach (var child in item.Children)
                CollectCriticalIds(child, ids);
        }

        private void DetectCriticalPathChanges(HashSet<string> before, HashSet<string> after)
        {
            var newlyCritical = after.Except(before).ToList();
            var noLongerCritical = before.Except(after).ToList();

            if (newlyCritical.Any() || noLongerCritical.Any())
            {
                var parts = new List<string>();
                if (newlyCritical.Any())
                    parts.Add($"⚠ {newlyCritical.Count} task(s) became CRITICAL");
                if (noLongerCritical.Any())
                    parts.Add($"✓ {noLongerCritical.Count} task(s) left the critical path");

                CriticalPathAlert = string.Join("  |  ", parts);
            }
            else
            {
                CriticalPathAlert = null;
            }
        }
    }
}
