using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Services;
using WpfResourceGantt.ProjectManagement.ViewModels;

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

                    RecalculateSandbox();
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
                OnPropertyChanged();
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

        private double _simTotalBcws;
        public double SimTotalBcws { get => _simTotalBcws; set { _simTotalBcws = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalSv)); OnPropertyChanged(nameof(ImpactSv)); } }

        private double _simTotalBcwp;
        public double SimTotalBcwp { get => _simTotalBcwp; set { _simTotalBcwp = value; OnPropertyChanged(); OnPropertyChanged(nameof(SimTotalSv)); OnPropertyChanged(nameof(ImpactSv)); } }

        public double SimTotalSv => SimTotalBcwp - SimTotalBcws;
        public double ImpactSv => SimTotalSv - LiveTotalSv;

        public ICommand ResetSimulationCommand { get; }
        public ICommand FlatlineFutureCommand { get; }
        public ICommand RecalculateCommand { get; }

        public SimulationViewModel(MainViewModel mainViewModel, DataService dataService)
        {
            _mainViewModel = mainViewModel;
            _dataService = dataService;
            _evmService = new EvmCalculationService(); // Instantiate clean engines for the sandbox
            _scheduleService = new ScheduleCalculationService();

            ResetSimulationCommand = new RelayCommand(InitializeSandbox);
            FlatlineFutureCommand = new RelayCommand(ExecuteFlatline);
            RecalculateCommand = new RelayCommand(RecalculateSandbox);
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

            // 2. Clone the entire hierarchy
            var clonedList = CloneHelper.DeepClone(liveSystems);
            SimulatedSystems = new ObservableCollection<SystemItem>(clonedList);
            OnPropertyChanged(nameof(SimulatedSystems));
            // --- INJECT CLONED DATA INTO GANTT CHART ---
            SandboxGanttContext = new Gantt.GanttViewModel(_mainViewModel, _dataService, SimulatedSystems);
            OnPropertyChanged(nameof(SandboxGanttContext));
            // 3. Reset Time
            _simulationProfiles.Clear();
            SimulatedDate = DateTime.Today;

            OnPropertyChanged(nameof(LiveTotalBcws));
            OnPropertyChanged(nameof(LiveTotalBcwp));
            OnPropertyChanged(nameof(LiveTotalSv));
        }

        private void RecalculateSandbox()
        {
            if (SimulatedSystems == null) return;

            // Run the cloned data through the unmodified schedule and EVM engines, 
            // but pass our time-travel date!
            _scheduleService.CalculateSchedule(SimulatedSystems, SimulatedDate);
            _evmService.RecalculateAll(SimulatedSystems, SimulatedDate);
            // --- FORCE THE GANTT CHART TO REDRAW WITH NEW DATES ---
            SandboxGanttContext?.RefreshAndPreserveState();

            // Update Sandbox Metrics
            SimTotalBcws = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => c.Bcws ?? 0) ?? 0), 2);
            SimTotalBcwp = Math.Round(SimulatedSystems.Sum(s => s.Children?.Sum(c => c.Bcwp ?? 0) ?? 0), 2);
        }
        private void GenerateInteractiveTimeline()
        {
            // 1. Unsubscribe current points
            foreach (var p in InteractiveDataPoints) p.PropertyChanged -= DataPoint_PropertyChanged;
            InteractiveDataPoints.Clear();

            if (SelectedItem == null || !SelectedItem.StartDate.HasValue) return;

            // 2. CHECK CACHE: Do we already have a profile for this item?
            if (_simulationProfiles.ContainsKey(SelectedItem.Id))
            {
                var cachedPoints = _simulationProfiles[SelectedItem.Id];
                foreach (var p in cachedPoints)
                {
                    p.PropertyChanged += DataPoint_PropertyChanged;
                    InteractiveDataPoints.Add(p);
                }
                return; // Exit early, we restored the curve
            }

            // 3. GENERATE NEW: (Only runs the first time you click an item)
            DateTime start = SelectedItem.StartDate.Value;
            DateTime itemEnd = SelectedItem.EndDate.GetValueOrDefault(start.AddMonths(1));
            DateTime end = itemEnd > SimulatedDate ? itemEnd : SimulatedDate;
            if ((end - start).TotalDays < 60) end = start.AddDays(60);

            var newPoints = new List<SimulationDataPoint>();
            int week = 1;
            DateTime current = start;

            while (current <= end)
            {
                var point = new SimulationDataPoint
                {
                    WeekNumber = week++,
                    Date = current,
                    Progress = SelectedItem.Progress,
                    ActualHours = SelectedItem.ActualWork ?? 0
                };
                newPoints.Add(point);
                current = current.AddDays(7);
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
            if (SelectedItem != null && InteractiveDataPoints.Any())
            {
                // Logic: Find the point that is exactly on or just before the SimulatedDate
                // This ensures dragging "past" dots works for history, and "future" dots works for projection
                var relevantPoint = InteractiveDataPoints
                    .Where(p => p.Date <= SimulatedDate)
                    .OrderByDescending(p => p.Date)
                    .FirstOrDefault() ?? InteractiveDataPoints.First();

                if (e.PropertyName == nameof(SimulationDataPoint.Progress))
                {
                    SelectedItem.Progress = relevantPoint.Progress;
                }
                else if (e.PropertyName == nameof(SimulationDataPoint.ActualHours))
                {
                    SelectedItem.ActualWork = relevantPoint.ActualHours;
                    SelectedItem.Acwp = relevantPoint.ActualHours * 195.0;
                }

                RecalculateSandbox();
            }
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
    }
}
