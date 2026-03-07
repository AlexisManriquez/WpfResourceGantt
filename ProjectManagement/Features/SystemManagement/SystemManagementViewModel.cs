using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Features.ApplyTemplate;
using WpfResourceGantt.ProjectManagement.Features.AssignDeveloper;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Services;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.SystemManagement
{
    public class FilterItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public object SourceItem { get; set; }
    }
    public class SystemManagementViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private readonly User _currentUser;
        private readonly MainViewModel _mainViewModel;
        private readonly TemplateService _templateService;
        private object _clipboardItem;
        private SystemHierarchyItemViewModel _lastClickedItem;
        private bool _isInternalSave;
        private ObservableCollection<SystemHierarchyItemViewModel> _hierarchicalSystems;
        public ObservableCollection<SystemHierarchyItemViewModel> HierarchicalSystems
        {
            get => _hierarchicalSystems;
            set { _hierarchicalSystems = value; OnPropertyChanged(); }
        }

        private bool _isDraggingSelection = false;
        private bool _dragTargetState = false;

        private bool _isAddingSystem;
        public bool IsAddingSystem
        {
            get => _isAddingSystem;
            set { _isAddingSystem = value; OnPropertyChanged(); }
        }

        public bool IsEvmHoursBased => _dataService.IsEvmHoursBased;
        public string EvmDisplayMode => _dataService.IsEvmHoursBased ? "Hours" : "Dollars";

        // Top-level adding fields
        // Top-level adding fields
        private string _newSystemNumber;
        public string NewSystemNumber { get => _newSystemNumber; set { _newSystemNumber = value; OnPropertyChanged(); } }

        private string _newSystemName;
        public string NewSystemName { get => _newSystemName; set { _newSystemName = value; OnPropertyChanged(); } }

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

                    // Cascade Logic
                    _selectedProjectFilter = null;
                    _selectedSubProjectFilter = null;
                    OnPropertyChanged(nameof(SelectedProjectFilter));
                    OnPropertyChanged(nameof(SelectedSubProjectFilter));

                    LoadProjectOptions();
                    LoadSystems(); // Trigger Filter
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

                    // Cascade Logic
                    _selectedSubProjectFilter = null;
                    OnPropertyChanged(nameof(SelectedSubProjectFilter));

                    LoadSubProjectOptions();
                    LoadSystems(); // Trigger Filter
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
                    LoadSystems(); // Trigger Filter
                }
            }
        }

        private SystemHierarchyItemViewModel _selectedVM;
        public SystemHierarchyItemViewModel SelectedVM
        {
            get => _selectedVM;
            set
            {
                _selectedVM = value;
                OnPropertyChanged();

                // CRITICAL: Notify Ribbon to re-check if buttons should be enabled
                (AddProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddSubProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddWbsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ApplyTemplateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RollupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (EditDetailsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (BaselineCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RebaselineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private List<object> _multiClipboard = new List<object>();
        public ICommand BaselineCommand { get; }
        public ICommand RebaselineCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand ClearSystemFilterCommand { get; }
        public ICommand ClearProjectFilterCommand { get; }
        public ICommand ClearSubProjectFilterCommand { get; }
        public ICommand ShowAddSystemCommand { get; }
        public ICommand ConfirmAddSystemCommand { get; }
        public ICommand CancelAddSystemCommand { get; }
        public ICommand CopySelectedCommand { get; }
        public ICommand PasteSelectedCommand { get; }
        public ICommand StartDragSelectionCommand { get; }
        public ICommand HoverSelectionCommand { get; }
        public ICommand EndDragSelectionCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ApplyTemplateCommand { get; }
        public ICommand RollupCommand { get; }
        public ICommand AddProjectCommand { get; }
        public ICommand AddSubProjectCommand { get; }
        public ICommand AddWbsCommand { get; }
        public ICommand EditDetailsCommand { get; }
        public ICommand DeleteCommand { get; }


        public SystemManagementViewModel(DataService dataService, User currentUser, MainViewModel mainViewModel, TemplateService templateService)
        {
            _dataService = dataService;
            _currentUser = currentUser;
            _mainViewModel = mainViewModel;
            _templateService = templateService;

            ShowAddSystemCommand = new RelayCommand(() => IsAddingSystem = true);
            ConfirmAddSystemCommand = new RelayCommand(ConfirmAddSystem);
            CancelAddSystemCommand = new RelayCommand(() => IsAddingSystem = false);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            ClearSystemFilterCommand = new RelayCommand(() => SelectedSystemFilter = null);
            ClearProjectFilterCommand = new RelayCommand(() => SelectedProjectFilter = null);
            ClearSubProjectFilterCommand = new RelayCommand(() => SelectedSubProjectFilter = null);
            // Define the Ribbon-accessible commands
            BaselineCommand = new RelayCommand(
               () => ExecuteBaseline(false),
               () => SelectedVM != null && SelectedVM.Level == 0 && !SelectedVM.IsBaselined);

            // ADD RebaselineCommand
            RebaselineCommand = new RelayCommand(
                () => ExecuteBaseline(true),
                () => SelectedVM != null && SelectedVM.Level == 0 && SelectedVM.IsBaselined);
            RollupCommand = new RelayCommand(
               ExecuteRollupSelected,
               CanExecuteRollup);
            AddProjectCommand = new RelayCommand(
                 () => SelectedVM.IsAddingChild = true,
                 () => SelectedVM != null && SelectedVM.Level == 0);

            // ADD SUBPROJECT: Only enabled if a PROJECT (Level 1) is selected
            AddSubProjectCommand = new RelayCommand(
                () => SelectedVM.IsAddingChild = true,
                () => SelectedVM != null && SelectedVM.Level == 1);

            // ADD WBS: Only enabled if a SUBPROJECT or deeper (Level >= 2) is selected
            AddWbsCommand = new RelayCommand(
                () => SelectedVM.IsAddingChild = true,
                () => SelectedVM != null && SelectedVM.Level >= 2);
            DeleteCommand = new RelayCommand(
                ExecuteDeleteSelected,
                () => GetSelectedViewModels(HierarchicalSystems).Any());

            // Specific Sub-Project Tools (Level 2 ONLY)
            ApplyTemplateCommand = new RelayCommand(
                () => HandleApplyTemplate(SelectedVM),
                () => SelectedVM != null && SelectedVM.Level == 2);

            EditDetailsCommand = new RelayCommand(
                () => OpenTaskDetails(SelectedVM),
                () => SelectedVM != null && SelectedVM.Level == 2);
            StartDragSelectionCommand = new RelayCommand<SystemHierarchyItemViewModel>(item =>
            {
                if (item == null) return;

                // 1. Handle SHIFT Selection (Range)
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _lastClickedItem != null)
                {
                    PerformShiftSelection(item);
                }
                // 2. Handle CTRL Selection (Toggle/Multi)
                else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    item.IsSelected = !item.IsSelected;
                    _lastClickedItem = item;
                }
                // 3. Normal Selection (Clear and Select One)
                else
                {
                    ClearAllSelection();
                    item.IsSelected = true;
                    _lastClickedItem = item;
                }
                SelectedVM = item;
                _isDraggingSelection = true;
                _dragTargetState = item.IsSelected;
            });

            HoverSelectionCommand = new RelayCommand<SystemHierarchyItemViewModel>(item =>
            {
                // Only apply if we are actively dragging
                if (_isDraggingSelection && item != null)
                {
                    item.IsSelected = _dragTargetState;
                }
            });

            EndDragSelectionCommand = new RelayCommand(() => _isDraggingSelection = false);
            ClearSelectionCommand = new RelayCommand(ClearAllSelection);
            // Wire up Copy/Paste to the Global Keyboard logic (usually in XAML or MainViewModel)
            CopySelectedCommand = new RelayCommand(ExecuteCopy);
            PasteSelectedCommand = new RelayCommand(ExecutePaste);
            DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
            _dataService.DataChanged += OnDataChanged;
            PopulateFilters();
            LoadSystems();
        }

        private void LoadSystems(HashSet<string> expandedIds = null)
        {
            // Always get a fresh list from DataService to avoid operating on filtered UI lists
            var allSystems = _dataService.GetSystemsForUser(_currentUser);

            // 1. Filter Systems
            if (SelectedSystemFilter != null)
            {
                allSystems = allSystems.Where(s => s.Id == SelectedSystemFilter.Id).ToList();
            }

            var viewModels = new List<SystemHierarchyItemViewModel>();

            foreach (var sys in allSystems)
            {
                var sysVm = MapToViewModel(sys, expandedIds);

                // 2. Filter Projects (Level 1)
                if (SelectedProjectFilter != null)
                {
                    var match = sysVm.Children.FirstOrDefault(c => c.Id == SelectedProjectFilter.Id);
                    sysVm.Children.Clear();
                    if (match != null)
                    {
                        sysVm.Children.Add(match);
                        sysVm.IsExpanded = true;

                        // 3. Filter Sub-Projects (Level 2)
                        if (SelectedSubProjectFilter != null)
                        {
                            var subMatch = match.Children.FirstOrDefault(c => c.Id == SelectedSubProjectFilter.Id);
                            match.Children.Clear();
                            if (subMatch != null)
                            {
                                match.Children.Add(subMatch);
                                match.IsExpanded = true;
                            }
                        }
                    }
                }
                sysVm.RollupHierarchy();
                viewModels.Add(sysVm);
            }

            HierarchicalSystems = new ObservableCollection<SystemHierarchyItemViewModel>(viewModels);
        }

        private SystemHierarchyItemViewModel MapToViewModel(SystemItem item, HashSet<string> expandedIds)
        {
            var user = _dataService.AllUsers.FirstOrDefault(u => u.Id == item.ProjectManagerId);
            var vm = new SystemHierarchyItemViewModel(SaveItem, DeleteItem, AddChild, AssignDeveloper, OpenTaskDetails, HandleApplyTemplate, HandleCopy, HandlePaste)
            {
                IsLoading = true,
                Id = item.Id,
                Level = 0,
                WbsValue = item.WbsValue,
                Name = item.Name,
                Status = item.Status,
                Assignee = user?.Name ?? "---",
                ScheduleMode = ScheduleMode.Dynamic,
                IsExpanded = expandedIds?.Contains(item.Id) ?? false
            };

            foreach (var child in item.Children)
            {
                var childVm = MapToViewModel(child, 1, child.ScheduleMode, expandedIds);
                childVm.Parent = vm;
                vm.Children.Add(childVm);
            }
            vm.IsLoading = false;
            return vm;
        }

        private SystemHierarchyItemViewModel MapToViewModel(WorkBreakdownItem item, int level, ScheduleMode scheduleMode, HashSet<string> expandedIds = null)
        {
            string assigneeText = "Unassigned";
            if (item.Assignments?.Any() == true)
            {
                var namesList = _dataService.AllUsers
                    .Where(u => item.Assignments.Any(a => a.DeveloperId == u.Id))
                    .Select(u => u.Name)
                    .ToList();
                assigneeText = namesList.Any() ? string.Join(", ", namesList) : "Unassigned";
            }
            else if (!string.IsNullOrEmpty(item.AssignedDeveloperId))
            {
                var user = _dataService.AllUsers.FirstOrDefault(u => u.Id == item.AssignedDeveloperId);
                assigneeText = user?.Name ?? "Unassigned";
            }

            var vm = new SystemHierarchyItemViewModel(SaveItem, DeleteItem, AddChild, AssignDeveloper, OpenTaskDetails, HandleApplyTemplate, HandleCopy, HandlePaste)
            {
                IsLoading = true,
                IsHoursMode = this.IsEvmHoursBased,
                Id = item.Id,
                Level = level,
                IsBaselined = item.IsBaselined,
                WbsValue = item.WbsValue,
                Name = item.Name,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                DurationDays = item.DurationDays,
                Predecessors = item.Predecessors,
                StartNoEarlierThan = item.StartNoEarlierThan,
                Work = item.Work,
                ActualWork = item.ActualWork,
                Status = item.Status,
                ItemType = item.ItemType,
                Assignee = assigneeText,
                ScheduleMode = item.ScheduleMode,
                IsExpanded = expandedIds?.Contains(item.Id) ?? false,                
                BAC = item.BAC,
                Bcws = item.Bcws,
                Bcwp = item.Bcwp,
                Acwp = item.Acwp,
                Progress = item.Progress,
                IsCritical = item.IsCritical,
                TotalFloat = item.TotalFloat,
                IsOverAllocated = item.IsOverAllocated
            };

            if (item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    var childVm = MapToViewModel(child, level + 1, child.ScheduleMode, expandedIds);
                    childVm.Parent = vm;
                    childVm.IsHoursMode = this.IsEvmHoursBased;
                    vm.Children.Add(childVm);
                }
            }
            vm.IsLoading = false;
            vm.RollupHierarchy();
            return vm;
        }
        private async void ExecuteBaseline(bool isRebaseline)
        {
            if (SelectedVM == null || SelectedVM.Level != 0) return;

            await _dataService.BaselineSystemAsync(SelectedVM.Id, isRebaseline);
            RefreshData(); // Reload the tree so all colors/flags update
        }
        private async void HandleApplyTemplate(SystemHierarchyItemViewModel vm)
        {
            // 1. Fetch templates
            var templates = await _templateService.GetAllTemplatesAsync();

            // 2. Setup Dialog
            var dialogVm = new ApplyTemplateDialogViewModel(templates);

            // 3. Show via MainViewModel
            _mainViewModel.ShowModalCustomDialog(dialogVm, async () =>
            {
                var workItem = _dataService.GetWorkBreakdownItemById(vm.Id);
                if (workItem == null) return;

                // 4. Overwrite logic
                if (dialogVm.IsOverwrite)
                {
                    workItem.Children?.Clear();
                    vm.Children.Clear();
                }

                // 5. Apply via Service
                await _templateService.ApplyTemplateAsync(dialogVm.SelectedTemplate.Id, workItem);

                // 6. Refresh the UI using state-preservation logic
                RefreshData();
            });
        }

        private void HandleCopy(SystemHierarchyItemViewModel vm)
        {
            if (vm.Level == 0)
                _clipboardItem = _dataService.GetSystemById(vm.Id);
            else
                _clipboardItem = _dataService.GetWorkBreakdownItemById(vm.Id);
        }

        private async void HandlePaste(SystemHierarchyItemViewModel targetParent)
        {
            if (_clipboardItem == null) return;

            if (_clipboardItem is WorkBreakdownItem originalWorkItem)
            {
                // 1. Determine the new parent in the data model
                var parentModel = (targetParent.Level == 0)
                    ? (object)_dataService.GetSystemById(targetParent.Id)
                    : (object)_dataService.GetWorkBreakdownItemById(targetParent.Id);

                if (parentModel == null) return;

                // 2. Get the next sequence number
                int nextSeq = 0;
                if (parentModel is SystemItem s) nextSeq = s.Children.Count;
                else if (parentModel is WorkBreakdownItem w) nextSeq = w.Children.Count;

                // 3. Clone only the new branch with a unique ID based on the parent
                var newWorkItem = _dataService.CloneWorkItem(originalWorkItem, targetParent.Id, nextSeq);

                // 4. Attach to Model (Existing sibling IDs remain untouched)
                if (parentModel is SystemItem sys) sys.Children.Add(newWorkItem);
                else if (parentModel is WorkBreakdownItem work) work.Children.Add(newWorkItem);

                // 5. Save (This will now only see one 'Added' entity and no 'Deletions' of siblings)
                await _dataService.SaveDataAsync();

                // 6. Update UI using state-preservation logic
                RefreshData();
            }
        }
        private void OnDataChanged(object sender, EventArgs e)
        {
            // If we just saved an item, don't wipe and rebuild the tree
            // This preserves the user's focus and current scroll position
            if (_isInternalSave) return;

            RefreshData();
        }
        private async void SaveItem(SystemHierarchyItemViewModel vm)
        {
            _isInternalSave = true; // Set guard
            try
            {
                // We need to keep track of which System needs to be recalculated in the model
                string systemId = vm.Id.Contains("|") ? vm.Id.Split('|')[0] : vm.Id;
                var rootSystem = _dataService.GetSystemById(systemId);

                // Update the model and save
                if (vm.Level == 0)
                {
                    if (rootSystem != null)
                    {
                        rootSystem.Name = vm.Name;
                        rootSystem.Status = vm.Status;

                        foreach (var child in rootSystem.Children)
                        {
                            SyncStatusRecursive(child, vm.Status);
                        }
                        _dataService.UpdateSystem(rootSystem);
                    }
                }
                else
                {
                    var workItem = _dataService.GetWorkBreakdownItemById(vm.Id);
                    if (workItem != null)
                    {
                        workItem.Name = vm.Name;
                        workItem.StartDate = vm.StartDate;
                        workItem.EndDate = vm.EndDate;
                        workItem.DurationDays = vm.DurationDays ?? 0;
                        workItem.Predecessors = vm.Predecessors;
                        workItem.StartNoEarlierThan = vm.StartNoEarlierThan;
                        workItem.Work = vm.Work;
                        workItem.Status = vm.Status;
                        workItem.ItemType = vm.ItemType;
                        workItem.BAC = vm.BAC;
                        foreach (var child in workItem.Children)
                        {
                            SyncStatusRecursive(child, vm.Status);
                        }

                        workItem.BAC = vm.BAC;
                        workItem.Bcws = vm.Bcws;
                        workItem.Bcwp = vm.Bcwp;
                        // ACWP is SMTS-only. Never written from UI.
                        // It is set exclusively by CsvImportService and rolled up
                        // by EvmCalculationService. Do not write vm.Acwp here.
                        workItem.Progress = vm.Progress ?? 0;
                    }
                }

                // CRITICAL FIX: Recalculate the entire model branch before saving.
                // This ensures parent durations/dates in the Data Model match the new child values.
                rootSystem?.RecalculateRollup();

                await _dataService.SaveDataAsync();
            }
            finally
            {
                _isInternalSave = false; // Reset guard
                RefreshData(); // Rebuild tree to show computed schedule results (Critical Path, Slack, new Dates)
            }
        }

        private void SyncStatusRecursive(WorkBreakdownItem item, WorkItemStatus status)
        {
            if (item == null) return;
            item.Status = status;
            foreach (var child in item.Children)
            {
                SyncStatusRecursive(child, status);
            }
        }

        private async void DeleteItem(SystemHierarchyItemViewModel vm)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{vm.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            if (vm.Level == 0)
            {
                await _dataService.DeleteSystemAsync(vm.Id);
                // REMOVED: HierarchicalSystems.Remove(vm);
            }
            else
            {
                // Find parent and remove from model
                var parent = FindParent(HierarchicalSystems, vm);
                if (parent != null)
                {
                    // REMOVED: parent.Children.Remove(vm);

                    var parentModel = _dataService.GetWorkBreakdownItemById(parent.Id);
                    if (parentModel != null)
                    {
                        var modelItem = parentModel.Children.FirstOrDefault(c => c.Id == vm.Id);
                        if (modelItem != null) parentModel.Children.Remove(modelItem);
                    }
                    else
                    {
                        var systemModel = _dataService.GetSystemById(parent.Id);
                        if (systemModel != null)
                        {
                            var modelItem = systemModel.Children.FirstOrDefault(c => c.Id == vm.Id);
                            if (modelItem != null) systemModel.Children.Remove(modelItem);
                        }
                    }
                }
            }
            // 3. This call triggers the DataChanged event, which now handles the UI update
            await _dataService.SaveDataAsync();
        }

        private SystemHierarchyItemViewModel FindParent(IEnumerable<SystemHierarchyItemViewModel> items, SystemHierarchyItemViewModel target)
        {
            foreach (var item in items)
            {
                if (item.Children.Contains(target)) return item;
                var found = FindParent(item.Children, target);
                if (found != null) return found;
            }
            return null;
        }

        private async void AddChild(SystemHierarchyItemViewModel parent)
        {
            if (string.IsNullOrWhiteSpace(parent.NewChildName)) return;
            // Convert the Hours entered in the form into Dollars for the Data Model
            decimal budgetedDollars = (parent.NewChildBAC ?? 0m) * 195m;
            string rootSystemId = parent.Level == 0 ? parent.Id : parent.Id.Split('|')[0];

            string childNameStr;
            if (parent.Level >= 2)
            {
                // Children added below Subproject get no dynamic number path
                childNameStr = parent.NewChildName.Trim();
            }
            else
            {
                string parentNumber = "";
                if (!string.IsNullOrWhiteSpace(parent.Name))
                {
                    int spaceIndex = parent.Name.IndexOf(' ');
                    parentNumber = spaceIndex >= 0 ? parent.Name.Substring(0, spaceIndex) : parent.Name;
                }
                childNameStr = string.IsNullOrWhiteSpace(parentNumber)
                                    ? $"{parent.NewChildNumber} {parent.NewChildName}".Trim()
                                    : $"{parentNumber}-{parent.NewChildNumber} {parent.NewChildName}".Trim();
            }

            var newItem = new WorkBreakdownItem
            {
                Id = $"{parent.Id}|{parent.Children.Count}_{Guid.NewGuid().ToString("N").Substring(0, 4)}",
                Name = childNameStr,
                ItemType = parent.NewChildItemType,
                DurationDays = parent.NewChildDurationDays,
                Predecessors = parent.ResolvePredecessors(parent.NewChildPredecessors),
                StartNoEarlierThan = parent.NewChildStartNoEarlierThan,
                ScheduleMode = parent.Level == 0 ? parent.NewChildScheduleMode : parent.ScheduleMode, // Propagate selected mode if it's a new Project (Level 1), else inherit
                StartDate = parent.NewChildStartDate ?? DateTime.Today,
                EndDate = WorkBreakdownItem.AddBusinessDays(parent.NewChildStartDate ?? DateTime.Today, parent.NewChildDurationDays),
                Work = parent.NewChildWork ?? 0,
                BAC = budgetedDollars,
                Status = WorkItemStatus.Active,
                Level = parent.Level + 1,
                Assignments = new List<ResourceAssignment>(),
                ProgressHistory = new List<ProgressHistoryItem> { new ProgressHistoryItem { Id = Guid.NewGuid().ToString(), Date = DateTime.Today, ActualProgress = 0 } }
            };

            if (parent.Level == 0)
            {
                var system = _dataService.GetSystemById(parent.Id);
                system?.Children.Add(newItem);
            }
            else
            {
                var parentModel = _dataService.GetWorkBreakdownItemById(parent.Id);
                parentModel?.Children.Add(newItem);
            }

            // 1. Force a full regeneration of this system's branch
            _dataService.RegenerateWbsValues(rootSystemId);

            await _dataService.SaveDataAsync();

            // 2. Refresh the UI using state-preservation logic
            RefreshData();

            parent.NewChildName = "";
            parent.NewChildNumber = "";
            parent.NewChildBAC = 0;
        }


        private async void ConfirmAddSystem()
        {
            if (string.IsNullOrWhiteSpace(NewSystemName)) return;

            var start = DateTime.Today;
            var end = DateTime.Today.AddMonths(6);
            // Calculate initial work (duration in days)
            double initialWork = (end - start).TotalDays;

            var newSystem = new SystemItem
            {
                Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}",
                WbsValue = (_dataService.AllSystems.Count + 1).ToString(),
                Name = $"{NewSystemNumber} {NewSystemName}".Trim(),
                ProjectManagerId = _currentUser.Id,
                Status = WorkItemStatus.Active,
                Children = new List<WorkBreakdownItem>()
            };

            _dataService.AddSystem(newSystem);
            await _dataService.SaveDataAsync();

            IsAddingSystem = false;
            NewSystemName = "";
            NewSystemNumber = "";
        }

        private void AssignDeveloper(SystemHierarchyItemViewModel vm)
        {
            // Only applicable for Tasks (Level > 0) usually, but we can allow assignment at any level if needed.
            // Systems (Level 0) usually have a Project Manager, not multiple assignments.

            // Invoke the shared dialog from MainViewModel (or create a new one here)
            // Ideally, MainViewModel should handle the dialog to keep this VM clean, 
            // OR we use the same dialog VIEW but manage the logic here.

            // Let's assume we can trigger the same dialog logic as "Add Task" but pre-filled.
            // Better: Create a direct call to a new AssignDialog method in MainViewModel or use a Service.

            // Simplest approach: We need an "Assign" dialog. 
            // We can reuse the logic from MainViewModel.OpenAssignDialog if it exists, or create a simple one here.

            // Since we don't have a direct "AssignDialog" readily available as a service, 
            // let's use the MainViewModel to facilitate.

            var workItem = _dataService.GetWorkBreakdownItemById(vm.Id);
            if (workItem == null) return;

            // Pass the REAL list of assignments to the dialog
            var dialogVm = new AssignDeveloperViewModel(_dataService.AllUsers, workItem.Assignments.ToList());

            _mainViewModel.ShowModalCustomDialog(dialogVm, async () =>
            {
                var updatedItem = _dataService.GetWorkBreakdownItemById(vm.Id);
                if (updatedItem == null) return;

                // 1. Handle "Unassign All" case
                if (dialogVm.IsUnassignRequested)
                {
                    updatedItem.Assignments.Clear();
                    updatedItem.AssignedDeveloperId = null;
                }
                else
                {
                    // 2. Sync the removals/existing state
                    updatedItem.Assignments.Clear();
                    foreach (var wrapper in dialogVm.CurrentAssignments)
                    {
                        updatedItem.Assignments.Add(wrapper.Assignment);
                    }

                    // 3. Add the NEW selection (if one was made)
                    if (dialogVm.SelectedDeveloper != null)
                    {
                        // Check for duplicates (same person with same role)
                        if (!updatedItem.Assignments.Any(a => a.DeveloperId == dialogVm.SelectedDeveloper.Id && a.Role == dialogVm.Role))
                        {
                            updatedItem.Assignments.Add(new ResourceAssignment
                            {
                                Id = Guid.NewGuid().ToString(),
                                WorkItemId = updatedItem.Id,
                                DeveloperId = dialogVm.SelectedDeveloper.Id,
                                Role = dialogVm.Role
                            });
                        }
                    }

                    // 4. Update legacy field (AssignedDeveloperId) based on final assignments
                    // If multiple exist, we pick the first Primary, or the first available.
                    if (updatedItem.Assignments.Any())
                    {
                        var primary = updatedItem.Assignments.FirstOrDefault(a => a.Role == AssignmentRole.Primary);
                        updatedItem.AssignedDeveloperId = primary?.DeveloperId ?? updatedItem.Assignments.First().DeveloperId;
                    }
                    else
                    {
                        updatedItem.AssignedDeveloperId = null;
                    }
                }

                // Refresh UI
                UpdateAssigneeText(vm, updatedItem);
                await _dataService.SaveDataAsync();
            });
        }

        private void UpdateAssigneeText(SystemHierarchyItemViewModel vm, WorkBreakdownItem model)
        {
            if (model.Assignments?.Any() == true)
            {
                var namesList = _dataService.AllUsers
                    .Where(u => model.Assignments.Any(a => a.DeveloperId == u.Id))
                    .Select(u => u.Name)
                    .ToList();
                vm.Assignee = string.Join(", ", namesList);
            }
            else
            {
                vm.Assignee = "Unassigned";
            }
        }

        private void OpenTaskDetails(SystemHierarchyItemViewModel vm)
        {
            var workItem = _dataService.GetWorkBreakdownItemById(vm.Id);
            if (workItem != null)
            {
                var uiWorkItem = MapToWorkItem(workItem);

                if (uiWorkItem.Level == 2)
                {
                    // For Subprojects (Level 2), go to the specialized Gate Progress View
                    _mainViewModel.GoToGateProgress(uiWorkItem);
                }
                else
                {
                    // For higher levels (Gates/Tasks), go to the granular Task Details View
                    //_mainViewModel.NavigateToTaskDetails(uiWorkItem);
                }
            }
        }


        private WorkItem MapToWorkItem(WorkBreakdownItem item)
        {
            if (item == null) return null;

            return new WorkItem
            {
                Id = item.Id,
                Name = item.Name,
                WbsValue = item.WbsValue,
                Level = item.Level,
                StartDate = item.StartDate ?? DateTime.Today,
                EndDate = item.EndDate ?? DateTime.Today,
                Work = item.Work ?? 0,
                ActualWork = item.ActualWork ?? 0,
                Progress = item.Progress,
                Status = item.Status,
                BAC = (double)(item.BAC ?? 0),
                Bcws = item.Bcws,
                Bcwp = item.Bcwp,
                Acwp = item.Acwp,
                ItemType = item.Children != null && item.Children.Any() ? WorkItemType.Summary : WorkItemType.Leaf,
                // Map children recursively so detailed views can traverse the hierarchy
                Children = new ObservableCollection<WorkItem>(item.Children?.Select(MapToWorkItem) ?? Enumerable.Empty<WorkItem>()),

                // Assignments and ProgressHistory are not in WorkItem.cs, so we don't map them here.
                // TaskDetailsViewModel will fetch the full item from DB anyway.
                DeveloperName = item.Assignments?.Any() == true
                    ? string.Join(", ", item.Assignments.Select(a => a.DeveloperId))
                    : "Unassigned",
                ProgressBlocks = new ObservableCollection<ProgressBlock>(item.ProgressBlocks ?? new List<ProgressBlock>())
            };
        }

        public void RefreshData()
        {
            // Ensure we are on the UI thread for capturing state and updating the collection
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(RefreshData);
                return;
            }
            SelectedVM = null;
            // 1. Save currently selected IDs
            string savedSysId = SelectedSystemFilter?.Id;
            string savedProjId = SelectedProjectFilter?.Id;
            string savedSubId = SelectedSubProjectFilter?.Id;

            // 2. NEW: Save Expansion State
            var expandedIds = new HashSet<string>();
            if (HierarchicalSystems != null)
            {
                CollectExpandedIdsRecursive(HierarchicalSystems, expandedIds);
            }

            // Force expansion of the selected item's branch if we are refreshing after an addition
            if (SelectedVM != null)
            {
                expandedIds.Add(SelectedVM.Id);
            }

            // 2. Reload the UI tree and the System dropdown
            LoadSystems(expandedIds);
            PopulateFilters();

            // 3. Restore Selections
            if (savedSysId != null)
            {
                SelectedSystemFilter = SystemOptions.FirstOrDefault(x => x.Id == savedSysId);
                if (savedProjId != null)
                {
                    SelectedProjectFilter = ProjectOptions.FirstOrDefault(x => x.Id == savedProjId);
                    if (savedSubId != null)
                    {
                        SelectedSubProjectFilter = SubProjectOptions.FirstOrDefault(x => x.Id == savedSubId);
                    }
                }
            }

            OnPropertyChanged(nameof(IsEvmHoursBased));
            OnPropertyChanged(nameof(EvmDisplayMode));
        }
        // NEW HELPER: Traverses the current UI tree to find what's open
        private void CollectExpandedIdsRecursive(IEnumerable<SystemHierarchyItemViewModel> items, HashSet<string> expandedIds)
        {
            foreach (var item in items)
            {
                if (item.IsExpanded)
                {
                    expandedIds.Add(item.Id);
                }
                CollectExpandedIdsRecursive(item.Children, expandedIds);
            }
        }
        private void PopulateFilters()
        {
            SystemOptions.Clear();
            var systems = _dataService.GetSystemsForUser(_currentUser);
            foreach (var sys in systems)
            {
                SystemOptions.Add(new FilterItem { Id = sys.Id, Name = sys.Name, SourceItem = sys });
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
                    ProjectOptions.Add(new FilterItem { Id = child.Id, Name = child.Name, SourceItem = child });
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
                    SubProjectOptions.Add(new FilterItem { Id = child.Id, Name = child.Name, SourceItem = child });
                }
            }
        }

        private void ClearFilters()
        {
            SelectedSystemFilter = null; // Cascadng logic handles the rest
        }

        // Helper to find all selected items across the whole tree
        private List<SystemHierarchyItemViewModel> GetSelectedViewModels(IEnumerable<SystemHierarchyItemViewModel> items)
        {
            var selected = new List<SystemHierarchyItemViewModel>();
            foreach (var item in items)
            {
                if (item.IsSelected) selected.Add(item);
                selected.AddRange(GetSelectedViewModels(item.Children));
            }
            return selected;
        }

        private void ExecuteCopy()
        {
            var selectedVms = GetSelectedViewModels(HierarchicalSystems);
            if (!selectedVms.Any()) return;

            _multiClipboard.Clear();
            foreach (var vm in selectedVms)
            {
                if (vm.Level == 0)
                    _multiClipboard.Add(_dataService.GetSystemById(vm.Id));
                else
                    _multiClipboard.Add(_dataService.GetWorkBreakdownItemById(vm.Id));
            }
        }

        private async void ExecutePaste()
        {
            if (!_multiClipboard.Any()) return;

            // 1. Find the target anchor (the last item selected)
            var targetVm = GetSelectedViewModels(HierarchicalSystems).LastOrDefault();
            if (targetVm == null) return;

            // 2. Determine the Root System ID for WBS regeneration later
            // (If target is System, use its ID. If it's a child, parse the root ID).
            string rootSystemId = targetVm.Level == 0 ? targetVm.Id : targetVm.Id.Split('|')[0];

            foreach (var clipboardObj in _multiClipboard)
            {
                // We primarily handle WorkBreakdownItems (Projects/Tasks)
                if (clipboardObj is WorkBreakdownItem sourceItem)
                {
                    SystemHierarchyItemViewModel parentVm = null;
                    int insertIndex = -1;

                    // LOGIC: Intelligent Paste
                    // sourceItem.Level tells us what 'tier' the copied item is (1=Project, 2=SubProject, etc.)

                    // Scenario A: Paste INTO (User selected the Parent container)
                    // e.g., Copied Project (L1), Selected System (L0). 0 == 1 - 1
                    if (targetVm.Level == sourceItem.Level - 1)
                    {
                        parentVm = targetVm;
                        insertIndex = targetVm.Children.Count; // Append to end of container
                    }
                    // Scenario B: Paste AFTER (User selected a Sibling)
                    // e.g., Copied Project (L1), Selected existing Project (L1).
                    else if (targetVm.Level == sourceItem.Level)
                    {
                        parentVm = targetVm.Parent;
                        // If parent is null, we are at root, but WorkItems can't be roots. 
                        // So checking parentVm != null is a safety guard.
                        if (parentVm != null)
                        {
                            insertIndex = parentVm.Children.IndexOf(targetVm) + 1; // Insert right after
                        }
                    }

                    // Execute Paste if valid parent found
                    if (parentVm != null)
                    {
                        // 1. Clone the Model
                        // DataService.CloneWorkItem handles the deep copy and new ID generation
                        var clonedItem = _dataService.CloneWorkItem(sourceItem, parentVm.Id, insertIndex);

                        // Ensure the new item has the correct Level property set
                        clonedItem.Level = parentVm.Level + 1;

                        // 2. Update the Data Model (System or WorkItem)
                        if (parentVm.Level == 0)
                        {
                            var system = _dataService.GetSystemById(parentVm.Id);
                            if (system != null)
                            {
                                if (insertIndex >= system.Children.Count) system.Children.Add(clonedItem);
                                else system.Children.Insert(insertIndex, clonedItem);
                            }
                        }
                        else
                        {
                            var parentWorkItem = _dataService.GetWorkBreakdownItemById(parentVm.Id);
                            if (parentWorkItem != null)
                            {
                                if (parentWorkItem.Children == null) parentWorkItem.Children = new List<WorkBreakdownItem>();
                                if (insertIndex >= parentWorkItem.Children.Count) parentWorkItem.Children.Add(clonedItem);
                                else parentWorkItem.Children.Insert(insertIndex, clonedItem);
                            }
                        }

                        // 3. Update the UI
                        // Handled by RefreshData() later
                    }
                }
                // Handle copying whole Systems (Level 0) if needed
                else if (clipboardObj is SystemItem sourceSys)
                {
                    // Systems can only be pasted as siblings of other systems (Root level)
                    // This logic remains similar to standard lists
                    if (targetVm.Level == 0)
                    {
                        // Implementation for System Cloning would go here
                        // (Currently skipping as per requirement to focus on Projects)
                    }
                }
            }

            // 4. Cleanup & Save
            _dataService.RegenerateWbsValues(rootSystemId);
            await _dataService.SaveDataAsync();

            // 5. Refresh the UI using state-preservation logic
            RefreshData();
        }

        private void ClearAllSelection()
        {
            if (HierarchicalSystems == null) return;
            ClearRecursive(HierarchicalSystems);
        }

        private void ClearRecursive(IEnumerable<SystemHierarchyItemViewModel> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;
                if (item.Children.Any())
                {
                    ClearRecursive(item.Children);
                }
            }
        }

        private async void ExecuteDeleteSelected()
        {
            var selectedItems = GetSelectedViewModels(HierarchicalSystems);
            if (!selectedItems.Any()) return;

            string message = selectedItems.Count == 1
                ? $"Are you sure you want to delete '{selectedItems[0].Name}'?"
                : $"Are you sure you want to delete {selectedItems.Count} selected items and their children?";

            var result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            // Group by level to handle deletion safely (optional, but good practice)
            // We iterate a copy of the list because we will be modifying the collections
            foreach (var vm in selectedItems.ToList())
            {
                if (vm.Level == 0)
                {
                    await _dataService.DeleteSystemAsync(vm.Id);
                    HierarchicalSystems.Remove(vm);
                }
                else
                {
                    var parent = FindParent(HierarchicalSystems, vm);
                    if (parent != null)
                    {
                        // Remove from UI
                        parent.Children.Remove(vm);

                        // Remove from Data Model
                        var parentModel = _dataService.GetWorkBreakdownItemById(parent.Id);
                        if (parentModel != null)
                        {
                            var modelItem = parentModel.Children.FirstOrDefault(c => c.Id == vm.Id);
                            if (modelItem != null) parentModel.Children.Remove(modelItem);
                        }
                        else
                        {
                            var systemModel = _dataService.GetSystemById(parent.Id);
                            if (systemModel != null)
                            {
                                var modelItem = systemModel.Children.FirstOrDefault(c => c.Id == vm.Id);
                                if (modelItem != null) systemModel.Children.Remove(modelItem);
                            }
                        }

                        // Trigger rollup for parent
                        parent.RollupHierarchy();
                    }
                }
            }

            await _dataService.SaveDataAsync();
        }

        private void GetFlattenedVisibleItems(IEnumerable<SystemHierarchyItemViewModel> items, List<SystemHierarchyItemViewModel> flatList)
        {
            foreach (var item in items)
            {
                flatList.Add(item);
                if (item.IsExpanded && item.Children.Any())
                {
                    GetFlattenedVisibleItems(item.Children, flatList);
                }
            }
        }

        // NEW METHOD: Selects a range of items between the anchor and the current click
        private void PerformShiftSelection(SystemHierarchyItemViewModel currentItem)
        {
            var flatList = new List<SystemHierarchyItemViewModel>();
            GetFlattenedVisibleItems(HierarchicalSystems, flatList);

            int startIdx = flatList.IndexOf(_lastClickedItem);
            int endIdx = flatList.IndexOf(currentItem);

            if (startIdx == -1 || endIdx == -1) return;

            // Determine direction
            int min = Math.Min(startIdx, endIdx);
            int max = Math.Max(startIdx, endIdx);

            // If Ctrl is not held, clear others first
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                ClearAllSelection();

            for (int i = min; i <= max; i++)
            {
                flatList[i].IsSelected = true;
            }
        }

        private bool CanExecuteRollup()
        {
            var selectedItems = GetSelectedViewModels(HierarchicalSystems);

            // 1. Must have selection
            if (selectedItems == null || !selectedItems.Any()) return false;

            // 2. All selected must share the same parent
            var firstParent = selectedItems.First().Parent;
            if (firstParent == null || selectedItems.Any(x => x.Parent != firstParent)) return false;

            // 3. Parent cannot be Level 0 (must be a WorkBreakdownItem)
            if (firstParent.Level == 0) return false;

            // 4. All selected items must be leaf nodes
            if (selectedItems.Any(x => x.Children.Any())) return false;

            // 5. ALL children of that parent must be selected
            return selectedItems.Count == firstParent.Children.Count;
        }

        private async void ExecuteRollupSelected()
        {
            var selectedVms = GetSelectedViewModels(HierarchicalSystems);
            if (!selectedVms.Any()) return;

            // Since validation passed, all share the same parent
            var parentVm = selectedVms.First().Parent;

            var result = MessageBox.Show(
                $"Are you sure you want to roll up all {selectedVms.Count} children into '{parentVm.Name}'?\n\n" +
                "• Checklists will be merged into the parent.\n" +
                "• All resource assignments will be cleared.\n" +
                "• Parent dates will remain unchanged.",
                "Confirm Rollup", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // 1. Fetch Parent Data Model
            var parentModel = _dataService.GetWorkBreakdownItemById(parentVm.Id);
            if (parentModel == null) return;

            // 2. Identify Root System ID for WBS Regeneration
            string rootSystemId = parentVm.Id.Contains("|") ? parentVm.Id.Split('|')[0] : parentVm.Id;

            // 3. Bake aggregated financial/work values into Parent
            // We use the sums from children as the new "base" for this now-leaf node.
            // NOTE: Acwp is intentionally excluded — it is owned by CsvImportService
            // (SMTS import) and will be correctly recalculated by EvmCalculationService
            // on the next data load. Never manually aggregate Acwp here.
            parentModel.Work = selectedVms.Sum(v => v.Work ?? 0);
            parentModel.ActualWork = selectedVms.Sum(v => v.ActualWork ?? 0);
            parentModel.BAC = selectedVms.Sum(v => v.BAC ?? 0);
            parentModel.Bcws = selectedVms.Sum(v => v.Bcws ?? 0);
            parentModel.Bcwp = selectedVms.Sum(v => v.Bcwp ?? 0);

            // 4. Merge Checklists (Keep the items, but don't use them for the initial math)
            if (parentModel.ProgressBlocks == null) parentModel.ProgressBlocks = new List<ProgressBlock>();
            foreach (var childVm in selectedVms)
            {
                var childModel = _dataService.GetWorkBreakdownItemById(childVm.Id);
                if (childModel?.ProgressBlocks != null)
                {
                    parentModel.ProgressBlocks.AddRange(childModel.ProgressBlocks);
                }
            }

            // 5. SET PARENT PROGRESS (The "Gantt-Consistent" Way)
            decimal totalBac = parentModel.BAC ?? 0;
            if (totalBac > 0)
            {
                // This ensures the 70% in the Gantt stays 70% after rollup
                parentModel.Progress = (double)((decimal)(parentModel.Bcwp ?? 0) / totalBac);
            }
            else if (parentModel.Work > 0)
            {
                // Fallback for items with no budget: Weight by Hours
                double totalWeightedProgress = selectedVms.Sum(v => (v.Progress ?? 0) * (v.Work ?? 0));
                parentModel.Progress = totalWeightedProgress / parentModel.Work.Value;
            }
            else
            {
                // Final fallback: Simple average
                parentModel.Progress = selectedVms.Any() ? selectedVms.Average(v => v.Progress ?? 0) : 0;
            }

            // 6. Finalize Data Model
            parentModel.Assignments = new List<ResourceAssignment>();
            parentModel.AssignedDeveloperId = null;
            parentModel.Children.Clear();

            // 7. Persist & Fix WBS
            _dataService.RegenerateWbsValues(rootSystemId);
            await _dataService.SaveDataAsync();

            // 8. Update UI State
            parentVm.Children.Clear();
            parentVm.Assignee = "Unassigned";
            parentVm.Work = parentModel.Work;
            parentVm.Progress = parentModel.Progress; // UI now reflects checklist-based rollup
            parentVm.IsExpanded = false;

            parentVm.RollupHierarchy();
            ClearAllSelection();
        }

    }
}
