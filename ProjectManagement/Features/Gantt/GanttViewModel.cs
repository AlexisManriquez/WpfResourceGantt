using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading; // For the Dispatcher
using WpfResourceGantt.ProjectManagement.Models;
namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    using System.Data.Common;
    using WpfResourceGantt.ProjectManagement;

    public class TimelineSegment
    {
        public string Name { get; set; }       // "Jan 2025" or "2025"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Width { get; set; }
    }
    public class FilterItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public object SourceItem { get; set; } // Keeps reference to the real object
    }
    public class GanttViewModel : ViewModelBase
    {
        public MainViewModel MainViewModel { get; }
        // CHANGE 1: Add a private field to hold the data service.
        private readonly DataService _dataService;
        private readonly IEnumerable<SystemItem> _simulatedData;
        public bool IsSimulationMode => _simulatedData != null;
        public DateTime? SimulatedDate { get; set; }
        private User _selectedUser;

        private CancellationTokenSource _expandCts;


        // --- Properties for the View ---
        private const double SvColumnWidth = 80;
        private const double CvColumnWidth = 80;
        // --- DYNAMIC COLUMN PROPERTIES ---

        // The columns currently shown on the grid
        public ObservableCollection<GanttColumn> VisibleColumns { get; private set; }

        // The list of all possible columns user can add
        public ObservableCollection<GanttColumn> AvailableColumns { get; private set; }

        // Command to add a column
        public ICommand AddColumnCommand { get; }

        // Command to remove a column
        public ICommand RemoveColumnCommand { get; }

        public ICommand OpenGateProgressCommand { get; }

        public ICommand DeleteSystemCommand { get; }
        // Constants
        private const double TaskColumnWidth = 300;

        // Calculated Property: Sum of Task Column + Dynamic Columns
        // This replaces the old hardcoded (300 + 70 + 80 + 80) logic
        private double FixedColumnsWidth => TaskColumnWidth + VisibleColumns.Sum(c => c.Width);
        //private const double MonthColumnWidth = 120;
        private double CurrentMonthColumnWidth => ZoomLevel;

        private DateTime _projectStartDate;
        public DateTime ProjectStartDate
        {
            get => _projectStartDate;
            private set { _projectStartDate = value; OnPropertyChanged(); }
        }

        private DateTime _dataMinDate = DateTime.Today;
        private DateTime _dataMaxDate = DateTime.Today.AddMonths(6);

        private DateTime _projectEndDate;
        public DateTime ProjectEndDate
        {
            get => _projectEndDate;
            private set { _projectEndDate = value; OnPropertyChanged(); UpdateTodayLinePosition(); }
        }

        // NEW: Add a property to control the total width of the timeline grid
        private double _timelineWidth;
        public double TimelineWidth
        {
            get => _timelineWidth;
            private set { _timelineWidth = value; OnPropertyChanged(); }
        }

        private double _totalGridWidth;
        public double TotalGridWidth
        {
            get => _totalGridWidth;
            private set { _totalGridWidth = value; OnPropertyChanged(); }
        }

        private double _zoomLevel = 120; // Set a default zoom level
        private const double ZoomThreshold = 50;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (_zoomLevel != value)
                {
                    _zoomLevel = value;
                    OnPropertyChanged(); // Notifies the UI about "ZoomLevel" (for the slider)

                    OnPropertyChanged(nameof(IsMonthlyViewVisible));

                    // Now, when RecalculateTimelineWidths runs, the UI already knows
                    // which set of headers it should be displaying.
                    RecalculateTimelineWidths();
                    UpdateTodayLinePosition();
                }
            }
        }

        private ICollectionView _groupedUsers;
        public ICollectionView GroupedUsers
        {
            get => _groupedUsers;
            set { _groupedUsers = value; OnPropertyChanged(); }
        }

        public double TodayLinePosition { get; private set; }
        public bool IsTodayLineVisible { get; private set; }

        public bool HasAssignments => WorkItems != null && WorkItems.Count > 0;

        public ObservableCollection<WorkItem> WorkItems { get; set; }
        public List<User> AllUsers => MainViewModel.AllUsers;
        public List<DateTime> Months { get; private set; }
        public List<TimelineHeaderItem> YearHeaders { get; private set; }
        public List<TimelineHeaderItem> HalfYearHeaders { get; private set; }
        public bool IsMonthlyViewVisible => ZoomLevel >= ZoomThreshold;

        private ObservableCollection<TimelineSegment> _timelineSegments;
        public ObservableCollection<TimelineSegment> TimelineSegments
        {
            get => _timelineSegments;
            set { _timelineSegments = value; OnPropertyChanged(); }
        }
        private double _lastAvailableWidth;
        public ICommand FitToScreenCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }

        public ICommand ImportHoursCommand { get; }


        private CancellationTokenSource _searchCancellationToken;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();

                    _searchCancellationToken?.Cancel();
                    _searchCancellationToken = new CancellationTokenSource();
                    var token = _searchCancellationToken.Token;

                    Task.Delay(300, token).ContinueWith(async _ =>
                    {
                        if (token.IsCancellationRequested) return;
                        // Search does NOT force an auto-fit zoom
                        await Application.Current.Dispatcher.InvokeAsync(() => ApplyFilter(zoomToFit: false));
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }
        public User SelectedUser
        {
            get => MainViewModel.CurrentUser;
            set
            {
                if (MainViewModel.CurrentUser != value)
                {
                    MainViewModel.CurrentUser = value;
                    OnPropertyChanged();
                    LoadDataForCurrentUser();
                }
            }
        }
        // --- FILTERING LOGIC ---

        private async void ApplyFilter(bool zoomToFit = false)
        {
            // Capture state
            string searchText = SearchText;
            string systemId = SelectedSystemFilter?.Id;
            string projectId = SelectedProjectFilter?.Id;
            string subProjectId = SelectedSubProjectFilter?.Id;

            bool hasSearchText = !string.IsNullOrWhiteSpace(searchText);
            string filterId = subProjectId ?? projectId ?? systemId; // Pick most specific
            bool hasDropdown = !string.IsNullOrEmpty(filterId);

            // Optimization: Clear filters
            if (!hasSearchText && !hasDropdown)
            {
                foreach (var item in WorkItems) SetVisibilityRecursive(item, true);
                CalculateTimeline(WorkItems.ToList());
                if (zoomToFit && _lastAvailableWidth > 0)
                {
                    PerformFit(_lastAvailableWidth);
                }
                return;
            }

            await Task.Run(() =>
            {
                var visibleIds = new HashSet<string>();
                var expandedIds = new HashSet<string>();

                foreach (var item in WorkItems)
                {
                    // Call the new logic
                    CalculateVisibility(item, searchText, filterId, visibleIds, expandedIds);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ApplyVisibilityResults(WorkItems, visibleIds, expandedIds);

                    // ONLY resize the timeline X-Axis if the user explicitly requested it
                    if (zoomToFit)
                    {
                        RecalculateTimelineBasedOnFilter();
                    }
                });
            });
        }

        // Return type helper to carry state up the recursion
        private struct VisibilityResult
        {
            public bool IsVisible;
            public bool RequestParentExpansion;
        }

        private VisibilityResult CalculateVisibility(WorkItem item, string text, string filterId, HashSet<string> visibleIds, HashSet<string> expandedIds)
        {
            bool hasSearchText = !string.IsNullOrWhiteSpace(text);
            bool hasFilterId = !string.IsNullOrEmpty(filterId);

            // 1. Check Self Status (Text Match)
            bool isTextMatch = hasSearchText && (item.Name != null && item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

            // --- FIXED ID MATCHING LOGIC ---
            bool isFilterTarget = false;
            bool isFilterPath = false;
            bool isDescendant = false;

            if (hasFilterId && !string.IsNullOrEmpty(item.Id))
            {
                // A) Exact Match (Target)
                if (item.Id.Equals(filterId, StringComparison.OrdinalIgnoreCase))
                {
                    isFilterTarget = true;
                }
                else
                {
                    // Prepare IDs with trailing dot to ensure we match full segments
                    // Example: Filter="Sys.1" -> "Sys.1."
                    // Item="Sys.10" -> "Sys.10." -> Does NOT start with "Sys.1." (Fixes the bug)
                    string filterIdPipe = filterId + "|";
                    string itemIdPipe = item.Id + "|";

                    // B) Ancestor (Item is a parent of the selected filter)
                    // Example: Item="Sys.1.", Filter="Sys.1.2" -> StartsWith is True.
                    isFilterPath = filterId.StartsWith(itemIdPipe, StringComparison.OrdinalIgnoreCase);

                    // C) Descendant (Item is inside the selected filter)
                    // Example: Item="Sys.1.2", Filter="Sys.1." -> StartsWith is True.
                    isDescendant = item.Id.StartsWith(filterIdPipe, StringComparison.OrdinalIgnoreCase);
                }
            }

            // 2. Recurse Children
            bool anyChildVisible = false;
            bool anyChildRequestsExpansion = false;

            foreach (var child in item.Children)
            {
                var childResult = CalculateVisibility(child, text, filterId, visibleIds, expandedIds);

                if (childResult.IsVisible) anyChildVisible = true;
                if (childResult.RequestParentExpansion) anyChildRequestsExpansion = true;
            }

            // 3. Determine Visibility
            // Visible if: Matched Text OR Matched Filter OR Child is Visible
            bool isVisible = isTextMatch || isFilterTarget || isFilterPath || isDescendant || anyChildVisible;

            if (isVisible) visibleIds.Add(item.Id);

            // 4. Determine Expansion
            // Expand THIS item if:
            // A. User specifically requested to expand (Search Match in child OR Filter Target below)
            // B. THIS item is the Filter Target (Auto-expand the selected System/Project to show immediate children)
            if (anyChildRequestsExpansion || isFilterTarget)
            {
                expandedIds.Add(item.Id);
            }

            // 5. Determine if we should ask OUR Parent to expand
            // Ask parent to expand if:
            // A. We are a direct Text Match
            // B. We are on the path to the Filter Target (Ancestor or Target)
            // C. One of our children requested it (Chain reaction for deep text matches)
            // NOTE: We do NOT ask parent to expand if we are just a 'Descendant'. 
            bool requestParentExpansion = isTextMatch || isFilterTarget || isFilterPath || anyChildRequestsExpansion;

            return new VisibilityResult
            {
                IsVisible = isVisible,
                RequestParentExpansion = requestParentExpansion
            };
        }

        // UI Update Method
        private void ApplyVisibilityResults(IEnumerable<WorkItem> items, HashSet<string> visibleIds, HashSet<string> expandedIds)
        {
            foreach (var item in items)
            {
                bool shouldBeVisible = visibleIds.Contains(item.Id) && !item.IsMilestone;
                if (item.IsVisible != shouldBeVisible) item.IsVisible = shouldBeVisible;

                // Only expand if necessary (don't collapse if user manually expanded)
                if (expandedIds.Contains(item.Id) && !item.IsExpanded)
                {
                    item.IsExpanded = true;
                }

                ApplyVisibilityResults(item.Children, visibleIds, expandedIds);
            }
        }

        private bool FilterRecursive(WorkItem item, string searchText)
        {
            // --- CONDITION 1: TEXT SEARCH ---
            bool matchesText = string.IsNullOrWhiteSpace(searchText) ||
                               (item.Name != null && item.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            // --- CONDITION 2: DROPDOWN SELECTION ---
            bool matchesDropdown = true;
            string filterId = null;

            // Determine the most specific filter ID selected
            if (SelectedSubProjectFilter != null) filterId = SelectedSubProjectFilter.Id;
            else if (SelectedProjectFilter != null) filterId = SelectedProjectFilter.Id;
            else if (SelectedSystemFilter != null) filterId = SelectedSystemFilter.Id;

            if (!string.IsNullOrEmpty(filterId) && !string.IsNullOrEmpty(item.Id))
            {
                // Match if:
                // A) This item is the selected item or deeper (e.g. Filter="1.1", Item="1.1.2")
                bool isSelfOrDescendant = item.Id.StartsWith(filterId, StringComparison.OrdinalIgnoreCase);

                // B) This item is an ancestor of the selected item (e.g. Filter="1.1", Item="1")
                // We must keep ancestors visible so we can traverse down to the selected item.
                bool isAncestor = filterId.StartsWith(item.Id, StringComparison.OrdinalIgnoreCase);

                matchesDropdown = isSelfOrDescendant || isAncestor;
            }

            // Combine: Must match BOTH text search (if any) AND dropdown filter (if any)
            bool matchesSelf = matchesText && matchesDropdown;

            // --- RECURSION ---
            // Check all children. If ANY child matches, this parent must remain visible.
            bool childMatches = false;
            foreach (var child in item.Children)
            {
                if (FilterRecursive(child, searchText))
                {
                    childMatches = true;
                }
            }

            // Final Visibility Decision - Milestones are never shown as rows
            item.IsVisible = (matchesSelf || childMatches) && !item.IsMilestone;

            // Auto-Expand if a child matches so the user sees the result
            if (childMatches)
            {
                item.IsExpanded = true;
            }

            return item.IsVisible;
        }

        private void SetVisibilityRecursive(WorkItem item, bool isVisible)
        {
            item.IsVisible = isVisible && !item.IsMilestone;
            foreach (var child in item.Children)
            {
                SetVisibilityRecursive(child, isVisible);
            }
        }

        // --- CASCADING FILTER PROPERTIES ---

        public ObservableCollection<FilterItem> SystemOptions { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> ProjectOptions { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> SubProjectOptions { get; } = new ObservableCollection<FilterItem>();


        private FilterItem _selectedSystemFilter;
        public FilterItem SelectedSystemFilter
        {
            get => _selectedSystemFilter;
            set
            {
                if (_selectedSystemFilter != value)
                {
                    _selectedSystemFilter = value;
                    OnPropertyChanged();

                    _selectedProjectFilter = null;
                    _selectedSubProjectFilter = null;
                    OnPropertyChanged(nameof(SelectedProjectFilter));
                    OnPropertyChanged(nameof(SelectedSubProjectFilter));

                    LoadProjectOptions();
                    ApplyFilter(zoomToFit: true); // Explicitly fit to this filter
                }
            }
        }

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

                    _selectedSubProjectFilter = null;
                    OnPropertyChanged(nameof(SelectedSubProjectFilter));

                    LoadSubProjectOptions();
                    ApplyFilter(zoomToFit: true);
                }
            }
        }

        private FilterItem _selectedSubProjectFilter;
        public FilterItem SelectedSubProjectFilter
        {
            get => _selectedSubProjectFilter;
            set
            {
                if (_selectedSubProjectFilter != value)
                {
                    _selectedSubProjectFilter = value;
                    OnPropertyChanged();
                    ApplyFilter(zoomToFit: true);
                }
            }
        }

        public ICommand ClearFiltersCommand { get; }

        public void RefreshAndPreserveState()
        {
            // 1. PRESERVE STATE
            var expandedIds = new HashSet<string>();
            var visibleBlocksIds = new HashSet<string>();
            GetStateRecursive(WorkItems, expandedIds, visibleBlocksIds);

            // 2. RELOAD DATA (CHECK FOR SIMULATION MODE)
            var userSystems = IsSimulationMode
                ? _simulatedData.ToList()
                : _dataService.GetSystemsForUser(MainViewModel.CurrentUser);
            var workItemsToShow = ConvertToWorkItems(userSystems);


            foreach (var system in workItemsToShow)
            {
                foreach (var project in system.Children)
                {
                    // EVM values (BCWS/BCWP/ACWP etc.) are authoritative from EvmCalculationService.
                    // The Gantt only needs to derive the visual health traffic-light colors.
                    CalculateOverallHealthRecursively(project);
                }
            }

            // 3. RESTORE STATE
            SetStateRecursive(workItemsToShow, expandedIds, visibleBlocksIds);

            // 4. UPDATE UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                MergeWorkItems(WorkItems, workItemsToShow);

                OnPropertyChanged(nameof(HasAssignments));
                ApplyCurrentFiltersSync();
            });
        }

        private void GetStateRecursive(IEnumerable<WorkItem> items, HashSet<string> expandedIds, HashSet<string> visibleBlocksIds)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.IsExpanded) expandedIds.Add(item.Id);
                if (item.AreProgressBlocksVisible) visibleBlocksIds.Add(item.Id);
                GetStateRecursive(item.Children, expandedIds, visibleBlocksIds);
            }
        }

        private void SetStateRecursive(IEnumerable<WorkItem> items, HashSet<string> expandedIds, HashSet<string> visibleBlocksIds)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                // Always restore row expansion (Summary items)
                if (expandedIds.Contains(item.Id)) item.IsExpanded = true;

                // ONLY restore checklist expansion if the item is a Leaf
                // This prevents summary items from showing the "No checklist items defined" area
                if (item.IsLeaf && visibleBlocksIds.Contains(item.Id))
                {
                    item.AreProgressBlocksVisible = true;
                }
                else
                {
                    item.AreProgressBlocksVisible = false;
                }

                SetStateRecursive(item.Children, expandedIds, visibleBlocksIds);
            }
        }
        public GanttViewModel(MainViewModel mainViewModel, DataService dataService, IEnumerable<SystemItem> simulatedData = null)
        {
            MainViewModel = mainViewModel;
            _dataService = dataService;
            _simulatedData = simulatedData;
            // Initialize collections and commands
            WorkItems = new ObservableCollection<WorkItem>();
            FitToScreenCommand = new RelayCommand<object>(FitToScreen);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            ImportHoursCommand = new RelayCommand(ImportHours);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            DeleteSystemCommand = new RelayCommand<WorkItem>(DeleteSystem);

            OpenGateProgressCommand = new RelayCommand<WorkItem>(item =>
            {
                if (item != null && item.Level == 2)
                {
                    MainViewModel.GoToGateProgress(item);
                }
            });
            // Populate the months header
            Months = new List<DateTime>();
            InitializeUserGrouping();
            // CHANGE 3: Use the injected service to get the data.
            // We no longer need to load it, just access it.
            LoadDataForCurrentUser();

            // Initialize Columns
            InitializeColumns();

            AddColumnCommand = new RelayCommand<GanttColumn>(AddColumn);
            RemoveColumnCommand = new RelayCommand<GanttColumn>(RemoveColumn);
            // PREVENT LIVE DATA UPDATES FROM REFRESHING THE SANDBOX
            if (!IsSimulationMode)
            {
                _dataService.DataChanged += OnDataChanged;
            }
        }
        private void OnDataChanged(object sender, EventArgs e)
        {
            // Use the Dispatcher to ensure we update the UI collection on the correct thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Use your existing method that reloads data while keeping 
                // the user's current expansion/collapse state!
                RefreshAndPreserveState();
            });
        }
        private void InitializeColumns()
        {
            VisibleColumns = new ObservableCollection<GanttColumn>
            {
                // Default visible columns
                new GanttColumn { Id = "health", Header = "Health", Width = 70, Type = ColumnType.Health },
                new GanttColumn { Id = "sv", Header = "SV", Width = 80, Type = ColumnType.SV },
                new GanttColumn { Id = "cv", Header = "CV", Width = 80, Type = ColumnType.CV },
                CreatePlaceholderColumn()
            };

            // Define what can be added. 
            // In a real app, you might filter this list based on what is already in VisibleColumns
            // For now, we list types so user can add them back if deleted.
            AvailableColumns = new ObservableCollection<GanttColumn>
            {
                new GanttColumn { Id = "health", Header = "Health", Width = 70, Type = ColumnType.Health },
                new GanttColumn { Id = "sv", Header = "SV", Width = 80, Type = ColumnType.SV },
                new GanttColumn { Id = "cv", Header = "CV", Width = 80, Type = ColumnType.CV },
                new GanttColumn { Id = "predecessors", Header = "Predecessors", Width = 100, Type = ColumnType.Predecessors },
                new GanttColumn { Id = "float", Header = "Float", Width = 60, Type = ColumnType.Float }
            };
        }

        private GanttColumn CreatePlaceholderColumn()
        {
            return new GanttColumn
            {
                Id = "new",
                Header = "Add New Column",
                Width = 120, // Wider for text
                Type = (ColumnType)99, // Dummy type
                IsPlaceholder = true
            };
        }

        // Updated Add Method
        private void AddColumn(GanttColumn columnTemplate)
        {
            if (columnTemplate == null) return;

            // 1. Find the placeholder
            var placeholder = VisibleColumns.FirstOrDefault(c => c.IsPlaceholder);
            int index = placeholder != null ? VisibleColumns.IndexOf(placeholder) : VisibleColumns.Count;

            // 2. Insert the new real column BEFORE the placeholder
            var newCol = new GanttColumn
            {
                Id = columnTemplate.Id,
                Header = columnTemplate.Header,
                Width = columnTemplate.Width,
                Type = columnTemplate.Type
            };

            VisibleColumns.Insert(index, newCol);

            // 3. Recalculate Layout
            if (_lastAvailableWidth > 0) PerformFit(_lastAvailableWidth);
        }

        // Updated Remove Method
        private void RemoveColumn(GanttColumn column)
        {
            if (column == null || column.IsPlaceholder) return; // Can't delete the placeholder

            VisibleColumns.Remove(column);
            if (_lastAvailableWidth > 0) PerformFit(_lastAvailableWidth);
        }



        // --- NEW: Expand/Collapse Logic ---
        private async void ExpandAll()
        {
            // Cancel any currently running expansion to prevent fighting
            _expandCts?.Cancel();
            _expandCts = new CancellationTokenSource();
            var token = _expandCts.Token;

            try
            {
                await Task.Run(async () =>
                {
                    foreach (var item in WorkItems)
                    {
                        if (token.IsCancellationRequested) break;
                        await ExpandRecursiveAsync(item, token);
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                // Operation was stopped safely
            }
        }

        private async void DeleteSystem(WorkItem workItem)
        {
            if (IsSimulationMode) { MessageBox.Show("Disabled in Sandbox Mode.", "Simulation"); return; }
            if (workItem == null || !workItem.IsSystem) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the system '{workItem.DisplayName}'?\n\nThis will permanently remove all associated tasks, progress history, and checklist items from the database.",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 1. Call DataService to remove from DB and memory
                await _dataService.DeleteSystemAsync(workItem.Id);

                // 2. Refresh the UI
                RefreshGanttView();

                MessageBox.Show("System deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private async Task ExpandRecursiveAsync(WorkItem item, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            if (item.IsSummary && !item.IsExpanded)
            {
                // Use Dispatcher to update UI, but CHECK TOKEN inside the action
                // so we don't expand if we've already requested a cancel.
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                        item.IsExpanded = true;
                }, DispatcherPriority.Background);
            }

            foreach (var child in item.Children)
            {
                if (token.IsCancellationRequested) return;
                await ExpandRecursiveAsync(child, token);
            }
        }
        private void CollapseAll()
        {
            // CRITICAL: Stop the Expand loop if it's still running
            _expandCts?.Cancel();

            // Now it is safe to collapse everything
            foreach (var item in WorkItems)
            {
                CollapseRecursive(item);
            }
        }

        private async void ImportHours()
        {
            if (IsSimulationMode) { MessageBox.Show("Disabled in Sandbox Mode.", "Simulation"); return; }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select Timesheet CSV"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var importService = new CsvImportService(_dataService);

                // Show a generic wait cursor or loading indicator here if desired
                Application.Current.MainWindow.Cursor = Cursors.Wait;

                var result = await importService.ImportHoursFromCsvAsync(openFileDialog.FileName);

                Application.Current.MainWindow.Cursor = Cursors.Arrow;

                // Show results
                string message = $"Processed {result.RecordsProcessed} rows.\nUpdated {result.MatchesFound} tasks.";
                if (result.Errors.Any())
                {
                    message += $"\n\n{result.Errors.Count} warnings (first 5):\n" +
                               string.Join("\n", result.Errors.Take(5));
                }

                MessageBox.Show(message, "Import Complete", MessageBoxButton.OK,
                    result.Errors.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);

                // Refresh the view to see new Actual Work numbers
                RefreshGanttView();
            }
        }

        // Call this inside LoadDataForCurrentUser() (at the end)
        private void PopulateFilters()
        {
            // Save IDs
            string sysId = SelectedSystemFilter?.Id;
            string projId = SelectedProjectFilter?.Id;
            string subId = SelectedSubProjectFilter?.Id;

            SystemOptions.Clear();
            var systems = _dataService.GetSystemsForUser(MainViewModel.CurrentUser);

            foreach (var sys in systems)
            {
                SystemOptions.Add(new FilterItem
                {
                    Id = sys.Id,
                    Name = FormatDisplayName(sys.Name, 0),
                    SourceItem = sys
                });
            }

            // Restore Selections
            if (sysId != null)
            {
                _selectedSystemFilter = SystemOptions.FirstOrDefault(x => x.Id == sysId);
                OnPropertyChanged(nameof(SelectedSystemFilter));

                LoadProjectOptions();
                if (projId != null)
                {
                    _selectedProjectFilter = ProjectOptions.FirstOrDefault(x => x.Id == projId);
                    OnPropertyChanged(nameof(SelectedProjectFilter));

                    LoadSubProjectOptions();
                    if (subId != null)
                    {
                        _selectedSubProjectFilter = SubProjectOptions.FirstOrDefault(x => x.Id == subId);
                        OnPropertyChanged(nameof(SelectedSubProjectFilter));
                    }
                }
            }
        }

        private void LoadProjectOptions()
        {
            ProjectOptions.Clear();
            SubProjectOptions.Clear();

            if (SelectedSystemFilter?.SourceItem is SystemItem system)
            {
                foreach (var child in system.Children)
                {
                    ProjectOptions.Add(new FilterItem
                    {
                        Id = child.Id,
                        Name = FormatDisplayName(child.Name, 1),
                        SourceItem = child
                    });
                }
            }
        }

        private void LoadSubProjectOptions()
        {
            SubProjectOptions.Clear();

            if (SelectedProjectFilter?.SourceItem is WorkBreakdownItem project)
            {
                foreach (var child in project.Children)
                {
                    SubProjectOptions.Add(new FilterItem
                    {
                        Id = child.Id,
                        Name = FormatDisplayName(child.Name, 2),
                        SourceItem = child
                    });
                }
            }
        }

        private void ClearFilters()
        {
            _selectedSystemFilter = null;
            _selectedProjectFilter = null;
            _selectedSubProjectFilter = null;

            OnPropertyChanged(nameof(SelectedSystemFilter));
            OnPropertyChanged(nameof(SelectedProjectFilter));
            OnPropertyChanged(nameof(SelectedSubProjectFilter));

            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            ApplyFilter(zoomToFit: true); // Refit to global view
        }

        private void CollapseRecursive(WorkItem item)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseRecursive(child);
            }
        }
        private (ObservableCollection<WorkItem> parentCollection, WorkItem draggedItem, WorkItem targetItem) FindWorkItemFamily(string draggedId, string targetId)
        {
            // Check top-level items (Systems)
            if (WorkItems.Any(w => w.Id == draggedId) && WorkItems.Any(w => w.Id == targetId))
            {
                return (WorkItems, WorkItems.First(w => w.Id == draggedId), WorkItems.First(w => w.Id == targetId));
            }

            // Recurse through the tree
            foreach (var system in WorkItems)
            {
                var result = FindWorkItemFamilyRecursive(system.Children, draggedId, targetId);
                if (result.parentCollection != null) return result;
            }

            return (null, null, null);
        }

        private (ObservableCollection<WorkItem> parentCollection, WorkItem draggedItem, WorkItem targetItem) FindWorkItemFamilyRecursive(ObservableCollection<WorkItem> collection, string draggedId, string targetId)
        {
            if (collection.Any(w => w.Id == draggedId) && collection.Any(w => w.Id == targetId))
            {
                return (collection, collection.First(w => w.Id == draggedId), collection.First(w => w.Id == targetId));
            }

            foreach (var item in collection)
            {
                var result = FindWorkItemFamilyRecursive(item.Children, draggedId, targetId);
                if (result.parentCollection != null) return result;
            }

            return (null, null, null);
        }


        // --- REPLACE your ReorderItems method with this "SURGICAL UPDATE" version ---
        public async Task ReorderItems(string draggedId, string targetId, string position)
        {
            if (IsSimulationMode) return;

            // 1. Reorder the backend data list.
            bool success = _dataService.ReorderWorkItem(draggedId, targetId, position);
            if (!success)
            {
                // If it failed, don't refresh, just bail.
                return;
            }

            // 2. Perform a full, state-preserving refresh.
            // This reloads from the newly reordered backend.
            RefreshAndPreserveState();

            // 3. Save the fully updated data model to the JSON file.
            // This is now safe because the backend list is reordered and re-ID'd.
            string reorderSystemId = draggedId.Contains("|") ? draggedId.Split('|')[0] : draggedId;
            _dataService.MarkSystemDirty(reorderSystemId);
            await _dataService.SaveDataAsync();
        }
        public void OnUserChanged(User newUser)
        {
            OnPropertyChanged(nameof(SelectedUser)); // Make sure the ComboBox UI updates
            LoadDataForCurrentUser();
        }
        private void InitializeUserGrouping()
        {
            TimelineSegments = new ObservableCollection<TimelineSegment>();
            // Create a default view based on the AllUsers list
            _groupedUsers = CollectionViewSource.GetDefaultView(AllUsers);

            // 1. Clear existing definitions to be safe
            _groupedUsers.GroupDescriptions.Clear();
            _groupedUsers.SortDescriptions.Clear();

            // 2. Add Sort Description (Sort by the Integer Order first, then Name)
            // This forces Flight Chiefs (0) to the top and Developers (3) to the bottom.
            _groupedUsers.SortDescriptions.Add(new SortDescription("GroupOrder", ListSortDirection.Ascending));
            _groupedUsers.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // 3. Apply the GroupDescription
            _groupedUsers.GroupDescriptions.Add(new PropertyGroupDescription("GroupHeader"));
        }

        // Update the Refresh method to ensure the list stays fresh if users were added
        public void RefreshGanttView()
        {
            // Re-initialize grouping in case the AllUsers list instance changed (e.g. new user added)
            InitializeUserGrouping();
            OnPropertyChanged(nameof(GroupedUsers));

            LoadDataForCurrentUser();
        }
        private void LoadDataForCurrentUser()
        {
            if (MainViewModel?.CurrentUser == null && !IsSimulationMode) return;
            var expandedIds = new HashSet<string>();
            var visibleBlocksIds = new HashSet<string>();
            GetStateRecursive(WorkItems, expandedIds, visibleBlocksIds);

            var userSystems = IsSimulationMode
                ? _simulatedData.ToList()
                : _dataService.GetSystemsForUser(MainViewModel.CurrentUser);

            var workItemsToShow = ConvertToWorkItems(userSystems);

            // Only force a hard recalculation of the whole timeline if it's completely empty
            if (ProjectStartDate == DateTime.MinValue || ProjectEndDate == DateTime.MinValue || !WorkItems.Any())
            {
                CalculateTimeline(workItemsToShow);
            }

            foreach (var system in workItemsToShow)
            {
                foreach (var project in system.Children)
                {
                    CalculateOverallHealthRecursively(project);
                }
            }

            SetStateRecursive(workItemsToShow, expandedIds, visibleBlocksIds);

            MergeWorkItems(WorkItems, workItemsToShow);
            OnPropertyChanged(nameof(HasAssignments));
            ApplyCurrentFiltersSync();
            PopulateFilters();
        }

        // Places the start/end dates on logical boundaries (Start of Year vs Start of Month)
        // based on the total duration of the project.
        private void SetSmartTimelineBounds(DateTime min, DateTime max)
        {
            _dataMinDate = min;
            _dataMaxDate = max;

            // Calculate raw duration to decide the mode
            double rawDays = (max - min).TotalDays;

            // Threshold: If > 2 years (approx 730 days), we likely want Yearly headers.
            if (rawDays > 730) // Long term projects (> 2 years)
            {
                // Snap to Jan 1st of the start year
                ProjectStartDate = new DateTime(min.Year, 1, 1);

                // FIX: Reduce the massive 10-year buffer. 
                // Add just enough padding (e.g., 1 year) so the bars aren't 
                // crammed against the right edge, but remain visible.
                ProjectEndDate = new DateTime(max.Year, 12, 31).AddYears(1);
            }
            else
            {
                // Monthly Mode
                ProjectStartDate = new DateTime(min.Year, min.Month, 1);

                // Add a smaller 6-month buffer
                ProjectEndDate = new DateTime(max.Year, max.Month, 1).AddMonths(6).AddDays(-1);
            }

            // Safety check
            if (ProjectEndDate <= ProjectStartDate)
                ProjectEndDate = ProjectStartDate.AddMonths(12);
        }

        private void CalculateTimeline(List<WorkItem> items)
        {
            var minDate = DateTime.MaxValue;
            var maxDate = DateTime.MinValue;

            // This helper function will now find the absolute earliest start and latest end date
            // from ALL items (tasks and summaries).
            FindAbsoluteDateRange(items, ref minDate, ref maxDate);

            // If no dates were found at all, create a default one-year timeline.
            if (minDate == DateTime.MaxValue)
            {
                _dataMinDate = DateTime.Today;
                _dataMaxDate = DateTime.Today.AddMonths(6);
                ProjectStartDate = _dataMinDate;
                ProjectEndDate = _dataMaxDate.AddMonths(12);
            }
            else
            {
                // --- CHANGED: Use helper ---
                SetSmartTimelineBounds(minDate, maxDate);
            }

            GenerateTimelineHeaders();
            RecalculateTimelineWidths();
        }

        private void RecalculateTimelineBasedOnFilter()
        {
            DateTime min = DateTime.MaxValue;
            DateTime max = DateTime.MinValue;
            bool foundDates = false;

            // A. PRIORITY: Dropdown Filter
            // If a dropdown is selected, we focus exactly on that item's date range.
            string filterId = null;
            if (SelectedSubProjectFilter != null) filterId = SelectedSubProjectFilter.Id;
            else if (SelectedProjectFilter != null) filterId = SelectedProjectFilter.Id;
            else if (SelectedSystemFilter != null) filterId = SelectedSystemFilter.Id;

            if (!string.IsNullOrEmpty(filterId))
            {
                // Find the specific WorkItem in the tree that matches the filter
                var targetItem = FindWorkItemById(WorkItems, filterId);
                if (targetItem != null && targetItem.StartDate != DateTime.MinValue && targetItem.EndDate != DateTime.MinValue)
                {
                    min = targetItem.StartDate;
                    max = targetItem.EndDate;
                    foundDates = true;
                }
            }
            // B. SECONDARY: Text Search
            // If no dropdown, but text exists, find the range of all text matches.
            else if (!string.IsNullOrEmpty(SearchText))
            {
                FindDatesForTextMatch(WorkItems, SearchText, ref min, ref max, ref foundDates);
            }

            // C. APPLY UPDATES
            if (foundDates)
            {
                // 1. Update the dates to focus on the filtered system
                SetSmartTimelineBounds(min, max);

                // 2. TRIGGER A STRETCH: 
                // Since you want it to "span the whole window," we call PerformFit.
                // This will calculate the exact ZoomLevel needed to make this specific 
                // system fill the available screen width.
                if (_lastAvailableWidth > 0)
                {
                    PerformFit(_lastAvailableWidth);
                }
                else
                {
                    RecalculateTimelineWidths();
                }
            }
            else
            {
                CalculateTimeline(WorkItems.ToList());
            }
        }

        private void FindDatesForTextMatch(IEnumerable<WorkItem> items, string text, ref DateTime min, ref DateTime max, ref bool found)
        {
            foreach (var item in items)
            {
                // We only care about visible items
                if (item.IsVisible)
                {
                    // Logic: Only expand the date range if this specific item matches the text.
                    // (We ignore parent containers that are only visible because they hold the child)
                    if (item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (item.StartDate != DateTime.MinValue)
                        {
                            if (item.StartDate < min) min = item.StartDate;
                            found = true;
                        }
                        if (item.EndDate != DateTime.MinValue)
                        {
                            if (item.EndDate > max) max = item.EndDate;
                            found = true;
                        }
                    }

                    // Recurse
                    if (item.Children.Any())
                    {
                        FindDatesForTextMatch(item.Children, text, ref min, ref max, ref found);
                    }
                }
            }
        }
        private void CalculateOverallHealthRecursively(WorkItem item)
        {
            DateTime statusDate = (IsSimulationMode && SimulatedDate.HasValue)
                ? SimulatedDate.Value
                : DateTime.Today;
            // --- RECURSIVE STEP: First, ensure all children have their health calculated. ---
            // We process from the bottom-up to ensure we have the correct child health to roll up.
            foreach (var child in item.Children)
            {
                CalculateOverallHealthRecursively(child);
            }

            // --- METRIC 1: Date-Based Schedule Health (for this item) ---
            // This logic only applies if the item is a Task, otherwise it's Good by default.
            MetricStatus dateHealth = MetricStatus.Good;
            if (!item.Children.Any()) // Leaf Task
            {
                if (item.Progress >= 1.0)
                {
                    dateHealth = (item.ActualFinishDate ?? DateTime.MaxValue) > item.EndDate
                        ? MetricStatus.Bad
                        : MetricStatus.Good;
                }
                else if (statusDate < item.StartDate)
                {
                    dateHealth = MetricStatus.Good;
                }
                else if (statusDate > item.EndDate)
                {
                    dateHealth = MetricStatus.Bad;
                }
                else if (item.EndDate > item.StartDate)
                {
                    // FIX: Use WorkBreakdownItem.GetBusinessDaysSpan to match the EVM Engine
                    int totalWorkDays = Models.WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate, item.EndDate);
                    int elapsedWorkDays = Models.WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate, statusDate);

                    double expected = totalWorkDays > 0 ? (double)elapsedWorkDays / totalWorkDays : 0;

                    // Now 'expected' will match the logic that drives SV/CV
                    if (item.Progress < expected - 0.05)
                        dateHealth = MetricStatus.Bad;
                    else if (item.Progress < expected - 0.001) // Tiny epsilon to prevent floating point jitter
                        dateHealth = MetricStatus.Warning;
                    else
                        dateHealth = MetricStatus.Good;
                }
            }

            // --- METRIC 2 & 3: This item's OWN SV and CV Health ---
            // We use our new helper method to calculate health from this item's direct data.
            var (ownSvHealth, ownCvHealth) = CalculateEVMHealth(item);

            // --- METRIC 4: The Worst Health Rolled Up from Children ---
            MetricStatus worstChildHealth = MetricStatus.Good;
            if (item.Children.Any())
            {
                // Use LINQ to find the "worst" health status among all direct children.
                if (item.Children.Any(c => c.WorkHealth == MetricStatus.Bad))
                    worstChildHealth = MetricStatus.Bad;
                else if (item.Children.Any(c => c.WorkHealth == MetricStatus.Warning))
                    worstChildHealth = MetricStatus.Warning;
            }

            // --- FINAL HEALTH ROLL-UP (for this item) ---
            // The final health is the WORST of all calculated metrics.
            if (dateHealth == MetricStatus.Bad || ownSvHealth == MetricStatus.Bad || ownCvHealth == MetricStatus.Bad || worstChildHealth == MetricStatus.Bad)
                item.WorkHealth = MetricStatus.Bad;
            else if (dateHealth == MetricStatus.Warning || ownSvHealth == MetricStatus.Warning || ownCvHealth == MetricStatus.Warning || worstChildHealth == MetricStatus.Warning)
                item.WorkHealth = MetricStatus.Warning;
            else
                item.WorkHealth = MetricStatus.Good;
        }

        private (MetricStatus svHealth, MetricStatus cvHealth) CalculateEVMHealth(WorkItem item)
        {
            MetricStatus svHealth = MetricStatus.Good;
            MetricStatus cvHealth = MetricStatus.Good;

            double bcws = item.Bcws ?? 0;
            double bcwp = item.Bcwp ?? 0;
            double acwp = item.Acwp ?? 0;

            // --- SV Health ---
            // Legend Rules:
            // Green: -10% to +10%
            // Yellow: +10% to +20% OR -10% to -20%
            // Red/Orange (Bad): > +20% OR < -20%
            if (bcws > 0)
            {
                double svPct = (bcwp - bcws) / bcws;

                // Check for severe deviation (Orange or Red)
                if (svPct > 0.20 || svPct < -0.20)
                {
                    svHealth = MetricStatus.Bad;
                }
                // Check for warning deviation (Yellow)
                else if (svPct > 0.10 || svPct < -0.10)
                {
                    svHealth = MetricStatus.Warning;
                }
                // Otherwise, it remains Good (Green)
            }

            // --- CV Health ---
            // Same Legend Rules apply.
            // Note: CV = 100% (1.0) will now trigger the > 0.20 check, resulting in Bad.

            if (bcwp == 0)
            {
                // If we have spent money but have 0 Earned Value, that is Bad.
                if (acwp > 0)
                {
                    cvHealth = MetricStatus.Bad;
                }
            }
            else
            {
                double cvPct = (bcwp - acwp) / bcwp;

                // Check for severe deviation (Orange or Red)
                if (cvPct > 0.20 || cvPct < -0.20)
                {
                    cvHealth = MetricStatus.Bad;
                }
                // Check for warning deviation (Yellow)
                else if (cvPct > 0.10 || cvPct < -0.10)
                {
                    cvHealth = MetricStatus.Warning;
                }
                // Otherwise, it remains Good (Green)
            }

            return (svHealth, cvHealth);
        }
        /// <summary>
        /// Recursively generates and SETS the hierarchical ID (WBS Code) for an item and all its children.
        /// </summary>
        private void GenerateWbsIdsRecursively(WorkItem item, string parentId)
        {
            // The parent's ID is already set. We just need to process its children.
            int childCounter = 1;
            foreach (var child in item.Children)
            {
                // The child's new ID is the parent's ID, plus a dot, plus its own number.
                string childId = $"{parentId}.{childCounter}";

                // Set the child's ID. The UI will update automatically.
                child.Id = childId;
                child.WbsValue = childId; // FIX: Also update the display value!

                // Recurse into the child's own children.
                GenerateWbsIdsRecursively(child, childId);
                childCounter++;
            }
        }

        private WorkItem FindWorkItemById(IEnumerable<WorkItem> items, string id)
        {
            foreach (var item in items)
            {
                if (item.Id == id) return item;
                var foundChild = FindWorkItemById(item.Children, id);
                if (foundChild != null) return foundChild;
            }
            return null;
        }

        private WorkItem GetRootSystemForWorkItem(WorkItem item)
        {
            if (item == null) return null;
            // Find which top-level System contains this item's ID in its hierarchy.
            return WorkItems.FirstOrDefault(system => FindWorkItemById(new[] { system }, item.Id) != null);
        }
        private void FindAbsoluteDateRange(IEnumerable<WorkItem> items, ref DateTime minDate, ref DateTime maxDate)
        {
            foreach (var item in items)
            {
                // Check every item that has a valid start date.
                if (item.StartDate != DateTime.MinValue)
                {
                    if (item.StartDate < minDate) minDate = item.StartDate;
                }
                if (item.EndDate != DateTime.MinValue)
                {
                    if (item.EndDate > maxDate) maxDate = item.EndDate;
                }

                // Recurse into children.
                if (item.Children.Any())
                {
                    FindAbsoluteDateRange(item.Children, ref minDate, ref maxDate);
                }
            }
        }
        private void FindDateRange(IEnumerable<WorkItem> items, ref DateTime minDate, ref DateTime maxDate)
        {
            foreach (var item in items)
            {
                // We only care about items that are actual tasks (not summaries)
                if (item.ItemType == WorkItemType.Leaf && item.StartDate != DateTime.MinValue)
                {
                    if (item.StartDate < minDate) minDate = item.StartDate;
                    if (item.EndDate > maxDate) maxDate = item.EndDate;
                }

                // Recurse into children
                if (item.Children.Any())
                {
                    FindDateRange(item.Children, ref minDate, ref maxDate);
                }
            }
        }

        private void GenerateTimelineHeaders()
        {
            var segments = new ObservableCollection<TimelineSegment>();
            if (ProjectEndDate < ProjectStartDate) return;

            double totalDays = (ProjectEndDate.AddDays(1) - ProjectStartDate).TotalDays;

            // DYNAMIC RESOLUTION:
            // If range < 90 days: Show Weeks
            // If range < 730 days: Show Months
            // If range > 730 days: Show Years

            var current = ProjectStartDate;

            // DYNAMIC RESOLUTION BASED ON ZOOM:
            // High Zoom (> 300): Show Weeks
            // Low Zoom (< 35) AND long project (> 2 years): Show Years
            // Otherwise: Show Months

            bool showWeeks = (ZoomLevel > 300) || (totalDays < 60);
            bool showYears = (ZoomLevel < 35) && (totalDays > 730);

            if (showWeeks)
            {
                // Weekly Headers
                while (current <= ProjectEndDate)
                {
                    var endOfWeek = current.AddDays(6);
                    var actualEnd = endOfWeek > ProjectEndDate ? ProjectEndDate : endOfWeek;
                    segments.Add(new TimelineSegment { Name = "Wk: " + current.ToString("MMM dd"), StartDate = current, EndDate = actualEnd });
                    current = current.AddDays(7);
                }
            }
            else if (showYears)
            {
                // Yearly Headers
                while (current <= ProjectEndDate)
                {
                    var endOfYear = new DateTime(current.Year, 12, 31);
                    var actualEnd = endOfYear > ProjectEndDate ? ProjectEndDate : endOfYear;
                    segments.Add(new TimelineSegment { Name = current.Year.ToString(), StartDate = current, EndDate = actualEnd });
                    current = endOfYear.AddDays(1);
                }
            }
            else
            {
                // Monthly Headers (Default)
                while (current <= ProjectEndDate)
                {
                    var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                    var endOfMonth = new DateTime(current.Year, current.Month, daysInMonth);
                    var actualEnd = endOfMonth > ProjectEndDate ? ProjectEndDate : endOfMonth;

                    // Use long name if zoom is high enough
                    string format = ZoomLevel > 150 ? "MMMM yyyy" : "MMM yy";
                    segments.Add(new TimelineSegment { Name = current.ToString(format), StartDate = current, EndDate = actualEnd });
                    current = endOfMonth.AddDays(1);
                }
            }

            TimelineSegments = segments;
        }

        // This method contains all the logic that needs to be re-run when the zoom level changes.
        private void RecalculateTimelineWidths()
        {
            // FIX: Calculate total days instead of months for pixel-perfect accuracy
            double totalDays = (ProjectEndDate.AddDays(1) - ProjectStartDate).TotalDays;

            // Calculate pixels per day based on the current ZoomLevel 
            // (ZoomLevel acts as the width of a standard 30-day month)
            double pixelsPerDay = ZoomLevel / 30.0;

            // Set the absolute width based on days
            TimelineWidth = totalDays * pixelsPerDay;

            OnPropertyChanged(nameof(TimelineWidth));
            GenerateTimelineHeaders();
            UpdateTodayLinePosition();
        }

        // CalculateDateRollup has been REMOVED.
        // Date rollup (StartDate / EndDate on summary nodes) is now handled by
        // EvmCalculationService.RecalculateSummary() in the data model layer.

        private void FitToScreen(object parameter)
        {
            if (parameter is not double availableWidth) return;

            // Cache the width so we can re-use it when filtering
            _lastAvailableWidth = availableWidth;

            PerformFit(availableWidth);
        }

        // 3. EXTRACT the logic into a helper method
        public void PerformFit(double availableWidth)
        {
            _lastAvailableWidth = availableWidth;
            double timelineAreaWidth = availableWidth - FixedColumnsWidth;
            double buffer = 40;

            if (timelineAreaWidth > buffer)
            {
                DateTime start = ProjectStartDate;
                DateTime end = ProjectEndDate.AddDays(1);
                double totalDays = (end - start).TotalDays;

                if (totalDays > 0)
                {
                    // FIX: Calculate zoom based on days to ensure it spans the exact pixel width
                    double pixelsPerDay = (timelineAreaWidth - buffer) / totalDays;

                    // Convert pixels-per-day back to ZoomLevel (which is pixels-per-month)
                    this.ZoomLevel = pixelsPerDay * 30.0;
                }

                // Force widths to update
                RecalculateTimelineWidths();
            }
        }

        private string FormatDisplayName(string fullName, int level)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;

            // Level 0: System (Display Full Name: "000 System")
            if (level == 0) return fullName;

            // Find the split between the Code (e.g., "000-000-0000") and the Name
            int spaceIndex = fullName.IndexOf(' ');
            if (spaceIndex == -1) return fullName;

            string codePart = fullName.Substring(0, spaceIndex);
            string namePart = fullName.Substring(spaceIndex + 1);

            // For Level 1 (Project) and Level 2 (Subproject):
            // We want the part after the LAST dash, plus the name.

            // Example Level 1: "000-111 Project" -> codePart="000-111" -> lastDash finds "111"
            // Example Level 2: "000-111-2222 Subproject" -> codePart="000-111-2222" -> lastDash finds "2222"

            int lastDashIndex = codePart.LastIndexOf('-');

            if (lastDashIndex != -1 && lastDashIndex < codePart.Length - 1)
            {
                string specificNumber = codePart.Substring(lastDashIndex + 1);
                return $"{specificNumber} {namePart}";
            }

            // Fallback: If there is no dash (e.g. "GATE 1" or "000 System" passed as Level 1)
            // Return the full name to be safe.
            return fullName;
        }

        // The ConvertToWorkItems mapper function remains the same
        private List<WorkItem> ConvertToWorkItems(List<SystemItem> systems)
        {
            var result = new List<WorkItem>();
            var sectionChiefs = _dataService.AllUsers.Where(u => u.Role == Role.SectionChief).ToList();

            foreach (var system in systems)
            {
                // Systems are containers — no ProjectManagerId. PM info is per-project now.
                var systemWorkItem = new WorkItem
                {
                    ItemType = WorkItemType.System,
                    Id = system.Id,
                    Level = 0,
                    Name = system.Name,
                    DisplayName = FormatDisplayName(system.Name, 0),

                    // System level no longer has a single PM or Section Chief
                    ProjectManagerName = null,
                    SectionChiefName = null,
                    Status = system.Status
                };

                // Start the recursion for its children
                foreach (var child in system.Children)
                {
                    systemWorkItem.Children.Add(ConvertWorkBreakdownItemToWorkItem(child, 1));
                }
                result.Add(systemWorkItem);
            }
            return result;
        }
        private WorkItem ConvertWorkBreakdownItemToWorkItem(WorkBreakdownItem item, int level)
        {
            bool hasChildren = item.Children != null && item.Children.Any();

            // LOGIC: Level 0 (System) and Level 1 (Project) are ALWAYS summaries.
            // Level 2+ are only summaries if they actually have children.
            bool isActuallySummary = (level <= 2) || hasChildren;

            var assignedDev = _dataService.AllUsers.FirstOrDefault(u => u.Id == item.AssignedDeveloperId);

            var workItem = new WorkItem
            {
                Id = item.Id,
                Level = level,
                Name = item.Name,
                WbsValue = item.WbsValue,
                DisplayName = FormatDisplayName(item.Name, level),
                StartDate = item.StartDate ?? DateTime.MinValue,
                EndDate = item.EndDate ?? DateTime.MinValue,
                Progress = item.Progress,
                Work = item.Work ?? 0,
                ActualWork = item.ActualWork ?? 0,
                Bcws = item.Bcws,
                Bcwp = item.Bcwp,
                Acwp = item.Acwp,
                ActualFinishDate = item.ActualFinishDate,
                ScheduleVariance = (item.Bcwp ?? 0) - (item.Bcws ?? 0),
                CostVariance = (item.Bcwp ?? 0) - (item.Acwp ?? 0),

                Predecessors = item.Predecessors,
                IsCritical = item.IsCritical,
                TotalFloat = item.TotalFloat,
                IsBaselined = item.IsBaselined,
                BaselineStartDate = item.BaselineStartDate,
                BaselineEndDate = item.BaselineEndDate,

                DeveloperName = GetAssignedDeveloperNames(item),
                // Map type based on our logic above, but preserve Milestones
                ItemType = item.ItemType == WorkItemType.Milestone ? WorkItemType.Milestone : (isActuallySummary ? WorkItemType.Summary : item.ItemType),
                AssignDeveloperCommand = MainViewModel.AssignDeveloperCommand,
                Status = item.Status,
                IsVisible = item.ItemType != WorkItemType.Milestone
            };

            workItem.PropertyChanged += OnWorkItemPropertyChanged;
            // Only allow progress blocks (checklists) on true Leaf tasks (items with no children)
            if (!hasChildren && item.ProgressBlocks != null)
            {
                foreach (var block in item.ProgressBlocks)
                {
                    workItem.ProgressBlocks.Add(block);
                }
            }

            if (hasChildren)
            {
                foreach (var child in item.Children)
                {
                    workItem.Children.Add(ConvertWorkBreakdownItemToWorkItem(child, level + 1));
                }
            }

            return workItem;
        }

        private string GetAssignedDeveloperNames(WorkBreakdownItem item)
        {
            var assignedIds = item.Assignments?.Select(a => a.DeveloperId).Distinct().ToList() ?? new List<string>();
            if (!assignedIds.Any() && !string.IsNullOrEmpty(item.AssignedDeveloperId)) assignedIds.Add(item.AssignedDeveloperId);

            if (!assignedIds.Any()) return "Unassigned";

            var names = _dataService.AllUsers
                .Where(u => assignedIds.Contains(u.Id))
                .Select(u => u.Name);

            var namesList = names.ToList();
            return namesList.Any() ? string.Join(", ", namesList) : "Unassigned";
        }
        // CalculateRollupProgress, CalculateAllRollups, and CalculateDateRollup
        // have been REMOVED. All EVM rollup is now performed exclusively by
        // EvmCalculationService in DataService.LoadDataAsync().
        // The GanttViewModel reads these pre-calculated values from the model.
        // This ensures all screens show identical, authoritative EVM data.


        private void UpdateTodayLinePosition()
        {
            var today = (IsSimulationMode && SimulatedDate.HasValue)
               ? SimulatedDate.Value
               : DateTime.Today;

            // FIX: Tighten the visibility check. 
            // If today is even one second outside the visible range, hide the line.
            if (today >= ProjectStartDate && today <= ProjectEndDate)
            {
                IsTodayLineVisible = true;
                double daysFromStart = (today - ProjectStartDate).TotalDays;
                double totalDays = (ProjectEndDate.AddDays(1) - ProjectStartDate).TotalDays;

                // Ensure we don't divide by zero and calculate position relative to current width
                TodayLinePosition = (totalDays > 0) ? (daysFromStart / totalDays) * TimelineWidth : 0;
            }
            else
            {
                IsTodayLineVisible = false;
                TodayLinePosition = 0; // Reset to 0 so it doesn't leak into other columns
            }

            OnPropertyChanged(nameof(IsTodayLineVisible));
            OnPropertyChanged(nameof(TodayLinePosition));
        }

        private void MergeWorkItems(ObservableCollection<WorkItem> targetList, List<WorkItem> sourceList)
        {
            // Check if the structure (number of items and IDs) is identical
            bool structuralMatch = targetList.Count == sourceList.Count;
            if (structuralMatch)
            {
                for (int i = 0; i < targetList.Count; i++)
                {
                    if (targetList[i].Id != sourceList[i].Id)
                    {
                        structuralMatch = false;
                        break;
                    }
                }
            }

            if (structuralMatch)
            {
                // Structure matches: update properties in-place so UI containers aren't destroyed
                for (int i = 0; i < targetList.Count; i++)
                {
                    UpdateWorkItemInPlace(targetList[i], sourceList[i]);
                }
            }
            else
            {
                // Structure changed: 
                // BEFORE CLEARING: Detach handlers to prevent memory leaks
                foreach (var item in targetList) DetachHandlersRecursive(item);

                targetList.Clear();
                foreach (var item in sourceList)
                {
                    targetList.Add(item);
                }
            }
        }

// Helper method
        private void DetachHandlersRecursive(WorkItem item)
        {
            item.PropertyChanged -= OnWorkItemPropertyChanged;
            foreach (var child in item.Children) DetachHandlersRecursive(child);
        }

        private void UpdateWorkItemInPlace(WorkItem target, WorkItem source)
        {
            // Sync all viewable properties that affect the Gantt chart and grid
            if (target.Name != source.Name) target.Name = source.Name;
            if (target.WbsValue != source.WbsValue) target.WbsValue = source.WbsValue;
            if (target.DisplayName != source.DisplayName) target.DisplayName = source.DisplayName;
            if (target.StartDate != source.StartDate) target.StartDate = source.StartDate;
            if (target.EndDate != source.EndDate) target.EndDate = source.EndDate;
            if (target.Progress != source.Progress) target.Progress = source.Progress;
            if (target.Work != source.Work) target.Work = source.Work;
            if (target.ActualWork != source.ActualWork) target.ActualWork = source.ActualWork;
            if (target.Bcws != source.Bcws) target.Bcws = source.Bcws;
            if (target.Bcwp != source.Bcwp) target.Bcwp = source.Bcwp;
            if (target.Acwp != source.Acwp) target.Acwp = source.Acwp;
            if (target.ActualFinishDate != source.ActualFinishDate) target.ActualFinishDate = source.ActualFinishDate;
            if (target.ScheduleVariance != source.ScheduleVariance) target.ScheduleVariance = source.ScheduleVariance;
            if (target.CostVariance != source.CostVariance) target.CostVariance = source.CostVariance;
            if (target.Predecessors != source.Predecessors) target.Predecessors = source.Predecessors;
            if (target.IsCritical != source.IsCritical) target.IsCritical = source.IsCritical;
            if (target.TotalFloat != source.TotalFloat) target.TotalFloat = source.TotalFloat;
            if (target.IsBaselined != source.IsBaselined) target.IsBaselined = source.IsBaselined;
            if (target.BaselineStartDate != source.BaselineStartDate) target.BaselineStartDate = source.BaselineStartDate;
            if (target.BaselineEndDate != source.BaselineEndDate) target.BaselineEndDate = source.BaselineEndDate;
            if (target.DeveloperName != source.DeveloperName) target.DeveloperName = source.DeveloperName;
            if (target.Status != source.Status) target.Status = source.Status;
            if (target.WorkHealth != source.WorkHealth) target.WorkHealth = source.WorkHealth;
            if (target.Level != source.Level) target.Level = source.Level;
            if (target.ItemType != source.ItemType) target.ItemType = source.ItemType;

            // Recurse to sync all children
            MergeWorkItems(target.Children, source.Children.ToList());
        }

        private void EnsureTimelineEncompassesData(List<WorkItem> items)
        {
            var minDate = DateTime.MaxValue;
            var maxDate = DateTime.MinValue;
            FindAbsoluteDateRange(items, ref minDate, ref maxDate);

            if (minDate == DateTime.MaxValue) return;

            bool boundsChanged = false;

            // Expand outward safely, never shrink inward (which would ruin current zoom focus)
            if (minDate < ProjectStartDate)
            {
                ProjectStartDate = new DateTime(minDate.Year, minDate.Month, 1);
                boundsChanged = true;
            }

            if (maxDate > ProjectEndDate)
            {
                ProjectEndDate = new DateTime(maxDate.Year, maxDate.Month, 1).AddMonths(6).AddDays(-1);
                boundsChanged = true;
            }

            if (boundsChanged)
            {
                RecalculateTimelineWidths(); // Re-calculates total width based on EXISTING ZoomLevel
            }
        }

        private void ApplyCurrentFiltersSync()
        {
            string searchText = SearchText;
            string systemId = _selectedSystemFilter?.Id;
            string projectId = _selectedProjectFilter?.Id;
            string subProjectId = _selectedSubProjectFilter?.Id;

            bool hasSearchText = !string.IsNullOrWhiteSpace(searchText);
            string filterId = subProjectId ?? projectId ?? systemId;
            bool hasDropdown = !string.IsNullOrEmpty(filterId);

            if (!hasSearchText && !hasDropdown)
            {
                // No filters active, make sure everything is visible
                foreach (var item in WorkItems) SetVisibilityRecursive(item, true);
                return;
            }

            var visibleIds = new HashSet<string>();
            var expandedIds = new HashSet<string>();

            foreach (var item in WorkItems)
            {
                CalculateVisibility(item, searchText, filterId, visibleIds, expandedIds);
            }

            ApplyVisibilityResults(WorkItems, visibleIds, expandedIds);
        }

        private void OnWorkItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkItem.IsExpanded))
            {
                var item = sender as WorkItem;
                if (item == null || item.Level != 1) return; // Only trigger for Project level

                if (item.IsExpanded)
                {
                    // Focus the timeline on this specific project
                    FocusTimelineOnItem(item);
                }
                else
                {
                    // When collapsing, check if we should reset to the full view
                    ResetTimelineToFullView();
                }
            }
        }

        private void FocusTimelineOnItem(WorkItem item)
        {
            if (item.StartDate == DateTime.MinValue || item.EndDate == DateTime.MinValue) return;

            // 1. Set the timeline bounds to the project's dates (plus a little padding)
            ProjectStartDate = item.StartDate.AddDays(-7); // 1 week lead-in
            ProjectEndDate = item.EndDate.AddMonths(1);    // 1 month tail

            // 2. Trigger the fit calculation using the last known width
            if (_lastAvailableWidth > 0)
            {
                PerformFit(_lastAvailableWidth);
            }
            else
            {
                RecalculateTimelineWidths();
            }
        }

        private void ResetTimelineToFullView()
        {
            // Check if any OTHER project is still expanded
            var otherExpandedProject = WorkItems
                .SelectMany(s => s.Children)
                .FirstOrDefault(p => p.Level == 1 && p.IsExpanded);

            if (otherExpandedProject != null)
            {
                // If another project is open, focus on that one instead of resetting fully
                FocusTimelineOnItem(otherExpandedProject);
            }
            else
            {
                // Otherwise, reset to the full system-wide date range
                CalculateTimeline(WorkItems.ToList());

                if (_lastAvailableWidth > 0)
                {
                    PerformFit(_lastAvailableWidth);
                }
            }
        }

    }
}
