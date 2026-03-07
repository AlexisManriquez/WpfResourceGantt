using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels; // For ViewModelBase
using WpfResourceGantt.ProjectManagement; // For ViewModelBase class in Root namespace
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // For Point
using System.Windows.Input;
using System.Windows.Media; // For PointCollection

namespace WpfResourceGantt.ProjectManagement.Features.Dashboard
{
    public enum ProjectHealthStatus
    {
        OnTrack,
        Behind,
        Ahead
    }

    public class GraphAxisLabel
    {
        public string Text { get; set; }
        public double XOffset { get; set; } // Position from left
    }


    public class DashboardViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private readonly User _currentUser;

        private object _currentContextItem; // Can be SystemItem or WorkBreakdownItem

        public bool CanGoBack => _currentContextItem != null;

        // Breadcrumb text (e.g., "System A > Subsystem B")
        public string CurrentViewTitle
        {
            get
            {
                if (_currentContextItem == null) return "Portfolio Overview (All Projects)";
                if (_currentContextItem is SystemItem sys) return $"System: {sys.Name}";
                if (_currentContextItem is WorkBreakdownItem wbs) return $"Item: {wbs.Name}";
                return "Dashboard";
            }
        }

        // Stat Card Properties
        public string TotalProjects => _allProjectCards.Count.ToString();
        public string OnTrackProjects => _allProjectCards.Count(p => p.Status == ProjectHealthStatus.OnTrack && !p.IsAtRisk).ToString();
        public string AtRiskProjects => _allProjectCards.Count(p => p.IsAtRisk).ToString();
        public string OffTrackProjects => _allProjectCards.Count(p => p.Status == ProjectHealthStatus.Behind).ToString();

        // Collections
        public ObservableCollection<string> Sections { get; set; }
        public ObservableCollection<string> StatusFilters { get; set; }

        // Changed to Rows for Virtualization support
        public ObservableCollection<ProjectRowViewModel> ProjectRows { get; set; }

        // Keep a flat list for stats calculation only
        private List<ProjectCardViewModel> _allProjectCards;
        private TimeRange _currentTimeRange = TimeRange.All;


        public ICommand NavigateCommand { get; }
        public ICommand GoBackCommand { get; }

        public DashboardViewModel(DataService dataService, User currentUser)
        {
            _dataService = dataService;
            _currentUser = currentUser;

            // Initialize Filters
            Sections = new ObservableCollection<string> { "All Sections", "Section A", "Section B", "Section C" };
            StatusFilters = new ObservableCollection<string> { "All", "On Track", "At Risk", "Off Track" };

            ProjectRows = new ObservableCollection<ProjectRowViewModel>();
            _allProjectCards = new List<ProjectCardViewModel>();

            NavigateCommand = new RelayCommand<string>(Navigate); // Pass ID of item clicked
            GoBackCommand = new RelayCommand(GoBack);

            LoadView();
        }

        private void Navigate(string itemId)
        {
            // 1. Find the object in the data structure
            var item = FindObjectById(itemId);
            if (item == null) return;

            // 2. Logic for Developers starting at Leaf Nodes
            if (_currentUser.Role == Role.Developer && _currentContextItem == null)
            {
                // If Dev is at root and clicks a leaf, they want to see the PARENT context
                var parent = FindParent(itemId);
                if (parent != null)
                {
                    _currentContextItem = parent;
                }
            }
            else
            {
                // Standard Drill Down (Manager or Dev already inside hierarchy)
                // Only drill down if it's a container (System or Summary)
                if (item is SystemItem || (item is WorkBreakdownItem wbi && wbi.Children.Any()))
                {
                    _currentContextItem = item;
                }
            }

            LoadView();
        }

        private void GoBack()
        {
            if (_currentContextItem == null) return;

            // Find the parent of the current context
            string currentId = _currentContextItem is SystemItem s ? s.Id : ((WorkBreakdownItem)_currentContextItem).Id;

            // If current item is a System, parent is Root (null)
            if (_currentContextItem is SystemItem)
            {
                _currentContextItem = null;
            }
            else
            {
                // Find parent WBS or System
                var parent = FindParent(currentId);
                if (parent is SystemItem)
                {
                    _currentContextItem = null;
                }
                else
                {
                    _currentContextItem = parent; // If parent is null (top of tree), this correctly sets root
                }
            }

            LoadView();
        }

        public void ApplyTimeRange(TimeRange range)
        {
            if (_currentTimeRange != range)
            {
                _currentTimeRange = range;
                LoadView();
            }
        }

        private void LoadView()
        {
            _allProjectCards.Clear();
            ProjectRows.Clear();

            var systems = _dataService.GetSystemsForUser(_currentUser); // Get Authorized Systems

            // SCENARIO 1: ROOT VIEW (No context selected)
            if (_currentContextItem == null)
            {
                // Developer Root: Show Assigned Leaves
                if (_currentUser.Role == Role.Developer)
                {
                    foreach (var system in systems)
                    {
                        var userTasks = new List<WorkBreakdownItem>();
                        CollectUserLeaves(system.Children, _currentUser.Id, userTasks);

                        foreach (var task in userTasks)
                        {
                            _allProjectCards.Add(CreateProjectCard(task.Name, task.Id, task.WbsValue, task.StartDate ?? DateTime.Now, task.EndDate ?? DateTime.Now, new List<WorkBreakdownItem> { task }));
                        }
                    }
                }
                // Manager Root: Show Projects
                else
                {
                    foreach (var system in systems)
                    {
                        if (system.Children == null) continue;
                        foreach (var project in system.Children)
                        {
                            var allLeaves = new List<WorkBreakdownItem>();
                            CollectLeaves(project, allLeaves);

                            // Better date detection for summary cards
                            var realStart = allLeaves.Any() ? allLeaves.Min(l => l.StartDate ?? project.StartDate ?? DateTime.Now) : project.StartDate ?? DateTime.Now;
                            var realEnd = allLeaves.Any() ? allLeaves.Max(l => l.EndDate ?? project.EndDate ?? DateTime.Now) : project.EndDate ?? DateTime.Now;

                            _allProjectCards.Add(CreateProjectCard($"{system.Name} > {project.Name}", project.Id, project.WbsValue, realStart, realEnd, allLeaves));
                        }
                    }
                }
            }
            // SCENARIO 2: DRILLED DOWN VIEW
            else
            {
                IEnumerable<WorkBreakdownItem> children = null;

                if (_currentContextItem is SystemItem sys) children = sys.Children;
                else if (_currentContextItem is WorkBreakdownItem wbi) children = wbi.Children;

                if (children != null)
                {
                    foreach (var child in children)
                    {
                        var allLeaves = new List<WorkBreakdownItem>();
                        CollectLeaves(child, allLeaves); // Recursively get leaves for graph

                        // Aggregated dates for the card if it's a summary
                        var realStart = allLeaves.Any() ? allLeaves.Min(l => l.StartDate ?? child.StartDate ?? DateTime.Now) : child.StartDate ?? DateTime.Now;
                        var realEnd = allLeaves.Any() ? allLeaves.Max(l => l.EndDate ?? child.EndDate ?? DateTime.Now) : child.EndDate ?? DateTime.Now;

                        _allProjectCards.Add(CreateProjectCard(child.Name, child.Id, child.WbsValue, realStart, realEnd, allLeaves));
                    }
                }
            }

            // Initial Load - View will call UpdateCardLayout shortly after with correct width
            // But we init with a safe default (e.g. 3) to have something show up
            UpdateCardLayout(3);

            // Update UI State
            OnPropertyChanged(nameof(TotalProjects));
            OnPropertyChanged(nameof(OnTrackProjects));
            OnPropertyChanged(nameof(AtRiskProjects));
            OnPropertyChanged(nameof(OffTrackProjects));
            OnPropertyChanged(nameof(CurrentViewTitle));
            OnPropertyChanged(nameof(CanGoBack));
        }

        public void UpdateCardLayout(int columns)
        {
            if (columns < 1) columns = 1;

            // If we are already displaying this number of columns, do nothing (optimization)
            if (ProjectRows.Count > 0 && ProjectRows[0].Cards.Count == columns && _allProjectCards.Count > columns)
                return;

            ProjectRows.Clear();

            for (int i = 0; i < _allProjectCards.Count; i += columns)
            {
                var chunk = _allProjectCards.Skip(i).Take(columns).ToList();
                ProjectRows.Add(new ProjectRowViewModel { Cards = chunk });
            }
        }

        private object FindObjectById(string id)
        {
            var systems = _dataService.GetSystemsForUser(new User { Role = Role.FlightChief }); // Search all
            foreach (var sys in systems)
            {
                if (sys.Id == id) return sys;
                var found = FindRecursive(sys.Children, id);
                if (found != null) return found;
            }
            return null;
        }

        private WorkBreakdownItem FindRecursive(IEnumerable<WorkBreakdownItem> items, string id)
        {
            foreach (var item in items)
            {
                if (item.Id == id) return item;
                var found = FindRecursive(item.Children, id);
                if (found != null) return found;
            }
            return null;
        }

        private object FindParent(string childId)
        {
            var systems = _dataService.GetSystemsForUser(new User { Role = Role.FlightChief });
            foreach (var sys in systems)
            {
                // Check if system is direct parent
                if (sys.Children.Any(c => c.Id == childId)) return sys;

                // Recurse
                var foundParent = FindParentRecursive(sys.Children, childId);
                if (foundParent != null) return foundParent;
            }
            return null;
        }

        private WorkBreakdownItem FindParentRecursive(IEnumerable<WorkBreakdownItem> items, string childId)
        {
            foreach (var item in items)
            {
                if (item.Children.Any(c => c.Id == childId)) return item;
                var found = FindParentRecursive(item.Children, childId);
                if (found != null) return found;
            }
            return null;
        }

        private ProjectCardViewModel CreateProjectCard(string name, string internalId, string wbsValue, DateTime start, DateTime end, List<WorkBreakdownItem> leafNodes)
        {
            var expectedPoints = new PointCollection();
            var actualPoints = new PointCollection();

            bool isBehind = false;
            string behindAheadText = "On Track";

            DateTime chartStart = start;
            DateTime chartEnd = end;

            // 1. DYNAMIC TIMELINE SCALING
            // Find the latest date we have any data for (planned end, today, or latest history)
            double currentTotalWork = leafNodes.Sum(l => l.Work ?? 1);
            double currentWeightedProgress = leafNodes.Sum(l => (l.Work ?? 1) * l.Progress);
            double currentOverallProgress = currentTotalWork > 0 ? currentWeightedProgress / currentTotalWork : 0;

            DateTime latestHistoryDate = leafNodes
                .SelectMany(l => l.ProgressHistory ?? new List<ProgressHistoryItem>())
                .Select(h => h.Date)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            // Rule: Axis must at least cover the planned range
            // Extension Rule: If not finished, extend to Today. 
            // If finished but late, extend to the date it was finished (latest history date).
            if (currentOverallProgress < 0.99 && DateTime.Now > chartEnd)
            {
                chartEnd = DateTime.Now;
            }
            if (latestHistoryDate > chartEnd)
            {
                chartEnd = latestHistoryDate;
            }

            // --- TIME RANGE OVERRIDE ---
            if (_currentTimeRange != TimeRange.All)
            {
                DateTime anchor = DateTime.Today;
                if (chartStart > anchor) anchor = chartStart; // If project hasn't started, use start.

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
                // We force the chart to end at the specific range, 
                // even if the project is longer or shorter.
                chartEnd = rangeEnd;
            }


            // Graph Dimensions
            double graphWidth = 280;
            double graphHeight = 60;
            int pointsCount = 50;

            double totalDurationTicks = (chartEnd - chartStart).Ticks;
            if (totalDurationTicks <= 0) totalDurationTicks = 1;
            double stepTicks = totalDurationTicks / pointsCount;

            // The original duration is used to calculate "Expected" progress correctly
            double originalDurationTicks = (end - start).Ticks;
            if (originalDurationTicks <= 0) originalDurationTicks = 1;

            double lastExpected = 0;
            double lastActual = 0;

            if (leafNodes.Any() && totalDurationTicks > 0)
            {
                // Always start both lines at x=0
                actualPoints.Add(new Point(0, graphHeight));

                // OPTIMIZATION: Pre-process leaves to avoid sorting history 5000+ times
                // We create a lightweight context for each leaf containing the Sorted History
                var leafContexts = leafNodes.Select(leaf => new
                {
                    Leaf = leaf,
                    Work = leaf.Work ?? 1,
                    StartDate = leaf.StartDate ?? DateTime.MinValue,
                    // Optimistically sort once. If null, use empty.
                    SortedHistory = leaf.ProgressHistory?
                                    .OrderBy(h => h.Date)
                                    .ToList()
                                    ?? new List<ProgressHistoryItem>()
                }).ToList();

                // Helper lambda for calculating weighted progress at a specific date
                Func<DateTime, double> calculateProgressAt = (date) =>
                {
                    double totalWeightedActual = 0;
                    double nodeWorkSum = 0;

                    // Iterate over our optimized contexts
                    foreach (var ctx in leafContexts)
                    {
                        nodeWorkSum += ctx.Work;
                        double progressToUse = 0;

                        // Baseline: Use live progress if date is at or after start
                        if (date >= ctx.StartDate)
                            progressToUse = ctx.Leaf.Progress;

                        // Use binary search or simple iteration on the pre-sorted list?
                        // Since list is likely small ( < 20 items per task), simple iteration from end is fast enough.
                        // We need the Last record where h.Date <= date.

                        if (ctx.SortedHistory.Count > 0)
                        {
                            // Optimization: Check last item first (common case for 'current date')
                            if (ctx.SortedHistory[ctx.SortedHistory.Count - 1].Date <= date)
                            {
                                // If we are past the latest history record, check if we should stick to history or live
                                // The original logic said: "If we are past the latest history record, sync with live Progress"
                                progressToUse = ctx.Leaf.Progress;
                            }
                            else if (ctx.SortedHistory[0].Date > date)
                            {
                                // Before first record - use first record's value? Or 0?
                                // Original logic: "Before first record -> progressToUse = historyRecords.First().ActualProgress"
                                progressToUse = ctx.SortedHistory[0].ActualProgress;
                            }
                            else
                            {
                                // Binary search or linear scan for correct interval
                                // Linear scan backwards is fine for small N
                                for (int k = ctx.SortedHistory.Count - 1; k >= 0; k--)
                                {
                                    if (ctx.SortedHistory[k].Date <= date)
                                    {
                                        progressToUse = ctx.SortedHistory[k].ActualProgress;
                                        break;
                                    }
                                }
                            }
                        }

                        totalWeightedActual += ctx.Work * progressToUse;
                    }
                    return nodeWorkSum > 0 ? totalWeightedActual / nodeWorkSum : 0;
                };

                DateTime dataCutoff = DateTime.Now;

                for (int i = 0; i <= pointsCount; i++)
                {
                    DateTime currentDate = chartStart.AddTicks((long)(stepTicks * i));
                    double x = i * (graphWidth / pointsCount);

                    // 2. Expected (Based on ORIGINAL planned dates)
                    double ticksElapsedSincePlannedStart = (currentDate - start).Ticks;
                    double expectedProgress = Math.Clamp(ticksElapsedSincePlannedStart / originalDurationTicks, 0, 1);
                    double yExpected = graphHeight - (expectedProgress * graphHeight);
                    expectedPoints.Add(new Point(x, yExpected));

                    // 3. Actual
                    if (currentDate <= dataCutoff)
                    {
                        double avgActual = calculateProgressAt(currentDate);
                        double yActual = graphHeight - (avgActual * graphHeight);

                        if (x > 0) actualPoints.Add(new Point(x, yActual));

                        lastActual = avgActual;
                        lastExpected = expectedProgress;
                    }
                }

                // 4. FINAL POINT SYNC: Ensure the blue line ends exactly at "Today" with the LATEST data
                DateTime now = DateTime.Now;
                if (now > chartStart && now <= chartEnd)
                {
                    double xNow = ((now - chartStart).Ticks / totalDurationTicks) * graphWidth;
                    // Recalculate precisely for "Now" to catch any spikes that happened since the last loop step
                    double currentActual = calculateProgressAt(now);
                    double yNow = graphHeight - (currentActual * graphHeight);

                    actualPoints.Add(new Point(xNow, yNow));

                    lastActual = currentActual;
                    // Sync expected to the same "now" point for health calculation
                    double ticksNow = (now - start).Ticks;
                    lastExpected = Math.Clamp(ticksNow / originalDurationTicks, 0, 1);
                }
            }

            // Status Calculation 
            if (currentOverallProgress >= 0.99)
            {
                // Project is COMPLETED. Check if it was finished late.
                if (latestHistoryDate > end.AddHours(24))
                {
                    isBehind = true;
                    behindAheadText = "Delayed";
                }
                else
                {
                    isBehind = false;
                    behindAheadText = "On Track";
                }
            }
            else
            {
                // Project is IN PROGRESS. Compare expected vs actual percentage.
                double diff = lastExpected - lastActual;
                if (diff > 0.05)
                {
                    isBehind = true;
                    behindAheadText = $"{diff:P0} behind";
                }
                else if (diff < -0.05)
                {
                    isBehind = false;
                    behindAheadText = $"{-diff:P0} ahead";
                }
            }

            var axisLabels = GenerateAxisLabels(chartStart, chartEnd, graphWidth);

            return new ProjectCardViewModel
            {
                Name = name,
                Id = internalId,
                WbsDisplayValue = wbsValue,
                Status = isBehind ? ProjectHealthStatus.Behind : ProjectHealthStatus.OnTrack,
                IsAtRisk = isBehind,
                ExpectedPoints = expectedPoints,
                ActualPoints = actualPoints,
                StartDate = chartStart,
                EndDate = chartEnd,
                BehindAheadText = behindAheadText,
                IsBehind = isBehind,
                XAxisLabels = axisLabels
            };
        }

        private void CollectLeaves(WorkBreakdownItem item, List<WorkBreakdownItem> leaves)
        {
            if (item.Children == null || !item.Children.Any())
            {
                leaves.Add(item);
            }
            else
            {
                foreach (var child in item.Children)
                {
                    CollectLeaves(child, leaves);
                }
            }
        }

        private void CollectUserLeaves(IEnumerable<WorkBreakdownItem> items, string userId, List<WorkBreakdownItem> userLeaves)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                // If leaf and assigned to user
                if ((item.Children == null || !item.Children.Any()) && item.AssignedDeveloperId == userId)
                {
                    userLeaves.Add(item);
                }
                // Recurse
                else
                {
                    CollectUserLeaves(item.Children, userId, userLeaves);
                }
            }
        }

        private ObservableCollection<GraphAxisLabel> GenerateAxisLabels(DateTime start, DateTime end, double width)
        {
            var labels = new ObservableCollection<GraphAxisLabel>();
            double totalDays = (end - start).TotalDays;

            // Logic to determine granularity
            // We want roughly 3-5 labels to fit in the 280px width

            if (totalDays < 60) // Less than 2 months: Show Weeks
            {
                for (DateTime d = start; d <= end; d = d.AddDays(14)) // Every 2 weeks
                {
                    labels.Add(CreateLabel(d, start, totalDays, width, "MMM dd"));
                }
            }
            else if (totalDays < 365) // Less than a year: Show Months
            {
                DateTime iterator = new DateTime(start.Year, start.Month, 1);
                // If starts mid-month, jump to next month for first label
                if (start.Day > 15) iterator = iterator.AddMonths(1);

                while (iterator <= end)
                {
                    labels.Add(CreateLabel(iterator, start, totalDays, width, "MMM"));
                    iterator = iterator.AddMonths(2); // Every 2 months
                }
            }
            else // Years: Show Years
            {
                DateTime iterator = new DateTime(start.Year, 1, 1);
                if (start.Month > 6) iterator = iterator.AddYears(1);

                while (iterator <= end)
                {
                    labels.Add(CreateLabel(iterator, start, totalDays, width, "yyyy"));
                    iterator = iterator.AddYears(1);
                }
            }

            // Always ensure End Date is labeled if space permits (optional logic, usually simpler to stick to grid)
            return labels;
        }

        private GraphAxisLabel CreateLabel(DateTime current, DateTime start, double totalDays, double width, string format)
        {
            double daysFromStart = (current - start).TotalDays;
            double percent = daysFromStart / totalDays;
            double x = percent * width;

            // Simple boundary check
            if (x < 0) x = 0;
            if (x > width) x = width;

            return new GraphAxisLabel
            {
                Text = current.ToString(format),
                XOffset = x
            };
        }

    }

    public class ProjectCardViewModel
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string WbsDisplayValue { get; set; }
        public ProjectHealthStatus Status { get; set; }
        public bool IsAtRisk { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public PointCollection ExpectedPoints { get; set; }
        public PointCollection ActualPoints { get; set; }

        public string BehindAheadText { get; set; }
        public bool IsBehind { get; set; }

        public string StatusTextColor => IsBehind ? "#DC2626" : "#059669"; // Red if behind, Green if ahead/track
        public ObservableCollection<GraphAxisLabel> XAxisLabels { get; set; }
    }
    public class ProjectRowViewModel
    {
        public List<ProjectCardViewModel> Cards { get; set; }
    }
}
