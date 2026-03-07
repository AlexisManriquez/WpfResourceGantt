using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Features.Analytics;
using WpfResourceGantt.ProjectManagement.Features.ApplyTemplate;
using WpfResourceGantt.ProjectManagement.Features.AssignDeveloper;
using WpfResourceGantt.ProjectManagement.Features.Dashboard;
using WpfResourceGantt.ProjectManagement.Features.Dialogs;
using WpfResourceGantt.ProjectManagement.Features.EVM;
using WpfResourceGantt.ProjectManagement.Features.Gantt;
using WpfResourceGantt.ProjectManagement.Features.ProjectCreation;
using WpfResourceGantt.ProjectManagement.Features.QuickTasks;
using WpfResourceGantt.ProjectManagement.Features.ResourceGantt;
using WpfResourceGantt.ProjectManagement.Features.SystemManagement;
using WpfResourceGantt.ProjectManagement.Features.UserManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Services;
using WpfResourceGantt.ProjectManagement.ViewModels;
#if ENABLE_MSPROJECT
using Excel = Microsoft.Office.Interop.Excel;
#endif
namespace WpfResourceGantt.ProjectManagement
{
    public enum TimeRange
    {
        All,
        Days30,
        Days60,
        Days90,
        Days180,
        Year1,
        Year3
    }

    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        private ViewModelBase _currentDialogViewModel;
        public ViewModelBase CurrentDialogViewModel
        {
            get => _currentDialogViewModel;
            set { _currentDialogViewModel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDialogVisible)); }
        }

        public bool IsDialogVisible => CurrentDialogViewModel != null;

        private bool _isRibbonExpanded = true;
        public bool IsRibbonExpanded
        {
            get => _isRibbonExpanded;
            set { _isRibbonExpanded = value; OnPropertyChanged(); }
        }

        private readonly DataService _dataService;
        public DataService DataService => _dataService;
        private readonly TemplateService _templateService;
        public List<User> AllUsers { get; private set; }

        private User _currentUser;
        public User CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsActionsMenuVisible)); // Notify the UI to check visibility

                    // If the current view is the Gantt chart, tell it to refresh
                    (CurrentViewModel as GanttViewModel)?.OnUserChanged(value);
                }
            }
        }
        public GanttViewModel GanttViewModel { get; private set; }
        // This property will control the button's visibility
        public bool IsActionsMenuVisible => CurrentUser?.Role != Role.Developer;

        // === 2. NEW COMMANDS FOR THE MENU ITEMS ===
        public ICommand SaveCommand { get; }
        public ICommand CreateSystemCommand { get; }
        public ICommand ImportProjectCommand { get; private set; }
        public ICommand ImportTestBlocksCommand { get; private set; }
        public ICommand ExportProjectCommand { get; private set; }
        public ICommand ReconstructProjectCommand { get; }
        public ICommand SetBaselineCommand { get; }
        public ICommand EditSystemCommand { get; }
        public ICommand CreateUserCommand { get; }
        public ICommand ShowUserManagementCommand { get; }
        public ICommand AssignDeveloperCommand { get; }
        public ICommand ShowGanttViewCommand { get; }
        public ICommand ShowDashboardViewCommand { get; }
        public ICommand ShowEVMViewCommand { get; }
        public ICommand ShowSystemManagementCommand { get; }
        public ICommand ToggleRibbonCommand { get; }

        // === VIEW TAB COMMANDS ===
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand SetTimeRangeCommand { get; } // New Command
        public ICommand ToggleEvmModeCommand { get; }

        public bool IsEvmHoursBased => _dataService?.IsEvmHoursBased ?? false;

        private TimeRange _currentTimeRange = TimeRange.Days180;
        public TimeRange CurrentTimeRange
        {
            get => _currentTimeRange;
            set
            {
                if (_currentTimeRange != value)
                {
                    _currentTimeRange = value;
                    OnPropertyChanged();
                    ApplyTimeRangeToCurrentView();
                }
            }
        }



        // === DATA TAB COMMANDS ===
        public ICommand ImportHoursCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand RefreshDataCommand { get; }

        /// <summary>
        /// "Close Week" — takes a frozen EVM snapshot for every SubProject.
        /// Visible only to PMs and FlightChiefs. Disabled for Developers.
        /// </summary>
        public ICommand CloseWeekCommand { get; }


        private bool _isGanttViewActive;
        public bool IsGanttViewActive
        {
            get => _isGanttViewActive;
            set { _isGanttViewActive = value; OnPropertyChanged(); }
        }

        private bool _isDashboardViewActive;
        public bool IsDashboardViewActive
        {
            get => _isDashboardViewActive;
            set { _isDashboardViewActive = value; OnPropertyChanged(); }
        }

        private bool _isUserManagementActive;
        public bool IsUserManagementActive
        {
            get => _isUserManagementActive;
            set { _isUserManagementActive = value; OnPropertyChanged(); }
        }

        private bool _isEVMViewActive;
        public bool IsEVMViewActive
        {
            get => _isEVMViewActive;
            set { _isEVMViewActive = value; OnPropertyChanged(); }
        }

        private bool _isSystemManagementActive;
        public bool IsSystemManagementActive
        {
            get => _isSystemManagementActive;
            set { _isSystemManagementActive = value; OnPropertyChanged(); }
        }

        // === NEW: Resource Gantt & Analytics Contexts ===
        public ResourceGanttViewModel GanttContext { get; set; }
        public AnalyticsViewModel AnalyticsContext { get; set; }

        private SystemManagementViewModel _systemManagementContext;
        public SystemManagementViewModel SystemManagementContext
        {
            get => _systemManagementContext;
            private set { _systemManagementContext = value; OnPropertyChanged(); }
        }
        private UserManagementViewModel _userManagementContext;
        public UserManagementViewModel UserManagementContext
        {
            get => _userManagementContext;
            private set { _userManagementContext = value; OnPropertyChanged(); }
        }
        // Navigation State
        private string _currentView;
        public string CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGanttVisible));
                    OnPropertyChanged(nameof(IsAnalyticsVisible));
                    OnPropertyChanged(nameof(IsProjectManagementVisible));
                    OnPropertyChanged(nameof(IsRangeSectionVisible));

                    // Specific Visibility Properties for MainWindow bindings
                    OnPropertyChanged(nameof(IsSystemsVisible));
                    OnPropertyChanged(nameof(IsDashboardVisible));
                    OnPropertyChanged(nameof(IsEVMVisible));
                    OnPropertyChanged(nameof(IsUsersVisible));
                    OnPropertyChanged(nameof(IsProjectsVisible));
                    OnPropertyChanged(nameof(IsGateProgressVisible));

                    // SWITCH THE INNER VIEWMODEL BASED ON THE STRING
                    switch (value)
                    {
                        case "Dashboard":
                            ShowDashboardView();
                            break;
                        case "Projects":
                            ShowProjectGanttView();
                            break;
                        case "Systems":
                            ShowSystemManagementView();
                            break;
                        case "EVM":
                            ShowEVMView();
                            break;
                        case "Users":
                            ShowUserManagementView();
                            break;
                        case "QuickTasks":
                            ShowQuickTasksView();
                            break;
                        case "Resource Gantt":
                            CurrentViewModel = GanttContext; // Set the context as the active VM
                            break;
                        case "Analytics":
                            CurrentViewModel = AnalyticsContext; // Set the context as the active VM
                            break;
                        case "GateProgress":
                            break;
                    }

                    if (IsAnalyticsVisible)
                    {
                        AnalyticsContext?.CalculateAnalytics();
                    }

                    ApplyTimeRangeToCurrentView();
                }
            }
        }

        public bool IsGanttVisible => CurrentView == "Resource Gantt";
        public bool IsAnalyticsVisible => CurrentView == "Analytics";
        public bool IsSystemsVisible => CurrentView == "Systems";
        public bool IsDashboardVisible => CurrentView == "Dashboard";
        public bool IsEVMVisible => CurrentView == "EVM";
        public bool IsUsersVisible => CurrentView == "Users";
        public bool IsProjectsVisible => CurrentView == "Projects";
        public bool IsQuickTasksVisible => CurrentView == "QuickTasks";

        public bool IsGateProgressVisible => CurrentView == "GateProgress";
        public bool IsRangeSectionVisible => IsGanttVisible || IsAnalyticsVisible;

        // Combined visibility for the legacy ProjectManagementControl wrapper
        // Note: Projects is now handled by the PM Control too.
        public bool IsProjectManagementVisible => !IsDeveloperPortalVisible && (IsDashboardVisible || IsEVMVisible || IsSystemsVisible || IsUsersVisible || IsProjectsVisible || IsQuickTasksVisible || IsGateProgressVisible || IsGanttVisible || IsAnalyticsVisible);

        // === ADD TASK MODAL PROPERTIES ===
        private bool _isAddTaskModalOpen;
        public bool IsAddTaskModalOpen
        {
            get => _isAddTaskModalOpen;
            set { _isAddTaskModalOpen = value; OnPropertyChanged(); }
        }

        private string _newTaskName;
        public string NewTaskName
        {
            get => _newTaskName;
            set { _newTaskName = value; OnPropertyChanged(); }
        }

        public List<User> AllResources => AllUsers ?? new List<User>();

        private User _newTaskSelectedPerson;
        public User NewTaskSelectedPerson
        {
            get => _newTaskSelectedPerson;
            set { _newTaskSelectedPerson = value; OnPropertyChanged(); }
        }

        private DateTime _newTaskStartDate = DateTime.Today;
        public DateTime NewTaskStartDate
        {
            get => _newTaskStartDate;
            set { _newTaskStartDate = value; OnPropertyChanged(); }
        }

        private DateTime _newTaskEndDate = DateTime.Today.AddDays(7);
        public DateTime NewTaskEndDate
        {
            get => _newTaskEndDate;
            set { _newTaskEndDate = value; OnPropertyChanged(); }
        }

        private static List<WpfResourceGantt.ProjectManagement.Models.TaskStatus>? _statusOptions;
        public List<WpfResourceGantt.ProjectManagement.Models.TaskStatus> StatusOptions =>
            _statusOptions ??= Enum.GetValues(typeof(WpfResourceGantt.ProjectManagement.Models.TaskStatus))
                                   .Cast<WpfResourceGantt.ProjectManagement.Models.TaskStatus>().ToList();

        private WpfResourceGantt.ProjectManagement.Models.TaskStatus _newTaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus.NotStarted;
        public WpfResourceGantt.ProjectManagement.Models.TaskStatus NewTaskStatus
        {
            get => _newTaskStatus;
            set { _newTaskStatus = value; OnPropertyChanged(); }
        }

        // === COMMANDS ===
        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowProjectsCommand { get; } // Map to "Projects"
        public ICommand ShowProjectManagementCommand { get; } // Alias for Projects
        public ICommand ShowSystemsCommand { get; }
        public ICommand ShowGanttCommand { get; }
        public ICommand ShowAnalyticsCommand { get; }
        public ICommand ShowEVMCommand { get; }
        public ICommand ShowUsersCommand { get; }

        public ICommand OpenAddTaskCommand { get; }
        public ICommand CancelAddTaskCommand { get; }
        public ICommand SaveNewTaskCommand { get; }


        // Make the constructor public for XAML instantiation
        public MainViewModel()
        {
            // Create the single authoritative EVM calculation engine first,
            // then inject it into DataService so all data operations use it.
            var evmService = new EvmCalculationService();
            _dataService = new DataService(evmService, new ScheduleCalculationService(), new ResourceAnalysisService());
            _templateService = new TemplateService(_dataService);
            // AllUsers = new List<User>(); // Initialize in LoadData

            // Initialize Contexts (Legacy & New)
            GanttContext = new ResourceGanttViewModel();
            AnalyticsContext = new AnalyticsViewModel();

            // --- INITIALIZE COMMANDS ---

            // Navigation (Renamed/Aliased to match XAML expectations)
            ShowDashboardCommand = new RelayCommand(() => CurrentView = "Dashboard");
            ShowProjectsCommand = new RelayCommand(() => CurrentView = "Projects");
            ShowProjectManagementCommand = new RelayCommand(() => CurrentView = "Projects");
            ShowSystemsCommand = new RelayCommand(() => CurrentView = "Systems");
            ShowGanttCommand = new RelayCommand(() => CurrentView = "Resource Gantt");
            ShowAnalyticsCommand = new RelayCommand(() => CurrentView = "Analytics");
            ShowEVMCommand = new RelayCommand(() => CurrentView = "EVM");
            ShowUsersCommand = new RelayCommand(() => CurrentView = "Users");
            ToggleRibbonCommand = new RelayCommand(() => IsRibbonExpanded = !IsRibbonExpanded);
            // Legacy Command Properties (Keeping for compatibility if C# code references them)
            ShowGanttViewCommand = ShowGanttCommand;
            ShowDashboardViewCommand = ShowDashboardCommand;
            ShowEVMViewCommand = ShowEVMCommand;
            ShowSystemManagementCommand = ShowSystemsCommand; // Map to Systems

            // Add Task Modal
            IsAddTaskModalOpen = false; // Ensure it starts closed
            OpenAddTaskCommand = new RelayCommand(() => CurrentView = "QuickTasks");
            CancelAddTaskCommand = new RelayCommand(() => IsAddTaskModalOpen = false);
            SaveNewTaskCommand = new RelayCommand(SaveNewTask);

            // Other Existing Commands
            CreateSystemCommand = new RelayCommand(ShowCreateSystemDialog);
            SetBaselineCommand = new RelayCommand<WorkItem>(SetBaseline);
            EditSystemCommand = new RelayCommand<WorkItem>(ShowEditSystemDialog);
            CreateUserCommand = new RelayCommand(ShowCreateUserDialog);
            ShowUserManagementCommand = ShowUsersCommand; // Alias
            AssignDeveloperCommand = new RelayCommand<WorkItem>(ShowAssignDeveloperDialog);
            ImportProjectCommand = new RelayCommand(ExecuteImportProject);
            ImportTestBlocksCommand = new RelayCommand(ShowImportTestBlocksDialog);
            ExportProjectCommand = new RelayCommand(ExecuteExportProject);

            // View/Data Commands
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
            ResetZoomCommand = new RelayCommand(ResetZoom);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            SetTimeRangeCommand = new RelayCommand<object>(SetTimeRange);
            ImportHoursCommand = new RelayCommand(ImportHours);
            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            ReconstructProjectCommand = new RelayCommand(ExecuteReconstructProject);
            RefreshDataCommand = new RelayCommand(RefreshData);

            // Close Week: snapshot all SubProject EVM metrics.
            // Only available to roles that can report (PM / FlightChief).
            CloseWeekCommand = new RelayCommand(
                async () => await ExecuteCloseWeekAsync(),
                () => CurrentUser?.Role != Role.Developer);

            ToggleEvmModeCommand = new RelayCommand(async () =>
            {
                await _dataService.ToggleEvmModeAsync(!IsEvmHoursBased);
                OnPropertyChanged(nameof(IsEvmHoursBased));

                if (CurrentViewModel is EVMViewModel evmVM)
                {
                    evmVM.DisplayMode = IsEvmHoursBased ? EVMDisplayMode.Hours : EVMDisplayMode.Dollars;
                }
            });

            // New Save Command
            SaveCommand = new RelayCommand(async () =>
            {
                await _dataService.SaveDataAsync();
                // Optional: Provide a visual indicator instead of a message box later
                MessageBox.Show("Project Data Saved Successfully", "Tactical Save", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            // Subscribe to data changes
            _dataService.DataChanged += OnDataChanged;
        }

        private async void OnDataChanged(object sender, EventArgs e)
        {
            // If the application is shutting down, ignore further data change notifications
            if (Application.Current == null) return;
            // When data changes anywhere, we refresh our local AllUsers
            // and trigger refreshes on the child contexts.
            // We use InvokeAsync to ensure we are on the UI thread for view updates.
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (Application.Current == null) return;
                AllUsers = _dataService.AllUsers.ToList();
                OnPropertyChanged(nameof(AllUsers));

                // Reload Resource Gantt and Analytics data
                var resources = await _dataService.GetResourceGanttDataAsync();
                var unassigned = await _dataService.GetUnassignedGanttTasksAsync();

                GanttContext?.LoadData(resources);
                AnalyticsContext?.LoadData(resources, unassigned);

                // If the current view is Project Gantt, refresh it too
                if (CurrentViewModel is GanttViewModel gantt)
                {
                    gantt.RefreshGanttView();
                }
            });
        }
        public void SaveUIState()
        {
            // Map temporary views back to their parent view
            string viewToSave = CurrentView;
            if (viewToSave == "GateProgress" || viewToSave == "QuickTasks")
            {
                viewToSave = "Projects";
            }

            _dataService.SavePreferences(viewToSave);
        }
        public static async Task<MainViewModel> CreateAsync(User loggedInUser = null)
        {
            var viewModel = new MainViewModel();
            await viewModel.InitializeAsync(loggedInUser);
            return viewModel;
        }


        private void SaveNewTask()
        {
            // Simple logic for now: Just close the modal.
            // In a real implementation, we would create a WorkItem and add it to the DB.
            // Since we lack a 'Parent' selection, we can't easily insert it into the hierarchy yet.
            MessageBox.Show($"Verified Task Creation:\nName: {NewTaskName}\nAssigned: {NewTaskSelectedPerson?.Name}\nRange: {NewTaskStartDate.ToShortDateString()} - {NewTaskEndDate.ToShortDateString()}", "Task Created (Simulation)");
            IsAddTaskModalOpen = false;
        }

        // This method now handles all initial data loading
        private async Task InitializeAsync(User loggedInUser)
        {
            // We now AWAIT the data loading, guaranteeing it's finished
            // before we proceed.
            await _dataService.LoadDataAsync();
            AllUsers = _dataService.AllUsers;
            OnPropertyChanged(nameof(IsEvmHoursBased)); // Refresh binding state

            // Load Resource Gantt Data
            var resources = await _dataService.GetResourceGanttDataAsync();
            var unassigned = await _dataService.GetUnassignedGanttTasksAsync();
            GanttContext.LoadData(resources);
            AnalyticsContext.LoadData(resources, unassigned);

            // Set the user (passed from Login or fallback)
            // If loggedInUser is null (dev mode bypass), fallback to first FlightChief or any user
            if (loggedInUser != null)
            {
                // Ensure we get the reference from the loaded list to keep object identity
                CurrentUser = AllUsers.FirstOrDefault(u => u.Id == loggedInUser.Id) ?? loggedInUser;
            }
            else
            {
                CurrentUser = AllUsers.FirstOrDefault(u => u.Role == Role.FlightChief) ?? AllUsers.FirstOrDefault();
            }

            // Check Role to determine Initial View
            if (CurrentUser?.Role == Role.Developer)
            {
                IsDeveloperPortalVisible = true;

                // For Developers, we set CurrentView to something else to prevent 
                // IsProjectManagementVisible from becoming true (via IsDashboardVisible).
                _currentView = "DeveloperPortal";

                // Initialize Developer Portal VM
                DeveloperPortalContext = new WpfResourceGantt.ProjectManagement.Features.DeveloperPortal.DeveloperPortalViewModel(_dataService, CurrentUser);
            }
            else
            {
                IsDeveloperPortalVisible = false;

                // --- NEW LOGIC START ---
                string lastView = await _dataService.LoadPreferencesAsync();
                if (!string.IsNullOrEmpty(lastView))
                {
                    CurrentView = lastView;
                }
                else
                {
                    CurrentView = "Dashboard";
                }
                // --- NEW LOGIC END ---
            }

            // Notify all visibility properties
            OnPropertyChanged(nameof(IsDeveloperPortalVisible));
            OnPropertyChanged(nameof(IsProjectManagementVisible));
            OnPropertyChanged(nameof(IsDashboardVisible));

            // MessageBox.Show($"Data Loaded Successfully.\nUsers: {AllUsers.Count}\nSystems: {_dataService.AllSystems?.Count ?? 0}\n\nSelect a view from the ribbon.", "Debug: Data Load");
        }

        private bool _isDeveloperPortalVisible;
        public bool IsDeveloperPortalVisible
        {
            get => _isDeveloperPortalVisible;
            set { _isDeveloperPortalVisible = value; OnPropertyChanged(); }
        }

        private WpfResourceGantt.ProjectManagement.Features.DeveloperPortal.DeveloperPortalViewModel _developerPortalContext;
        public WpfResourceGantt.ProjectManagement.Features.DeveloperPortal.DeveloperPortalViewModel DeveloperPortalContext
        {
            get => _developerPortalContext;
            set { _developerPortalContext = value; OnPropertyChanged(); }
        }

        private async void ExecuteImportProject()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Microsoft Project Files (*.mpp)|*.mpp",
                Title = "Import Microsoft Project File"
            };

            if (openFileDialog.ShowDialog() != true) return;

            string filePath = openFileDialog.FileName;

            // 1. Prepare and Show Destination Dialog
            var dialogVm = new SelectSystemDialogViewModel(_dataService.AllSystems);

            dialogVm.OnCloseRequest += async (confirmed) =>
            {
                CurrentDialogViewModel = null;

                if (confirmed)
                {
                    try
                    {
                        if (dialogVm.IsCreateNew)
                        {
                            // Pass Name and Number separately to the DataService
                            await _dataService.ImportMppAndSaveAsync(
                                filePath,
                                null,
                                dialogVm.NewSystemName,
                                dialogVm.NewSystemNumber);
                        }
                        else
                        {
                            await _dataService.ImportMppAndSaveAsync(
                                filePath,
                                dialogVm.SelectedSystem?.Id,
                                null);
                        }

                        if (CurrentViewModel is GanttViewModel gantt)
                            gantt.RefreshGanttView();

                        MessageBox.Show("Project imported successfully!", "Success");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error importing: {ex.Message}", "Import Error");
                    }
                }
            };

            CurrentDialogViewModel = dialogVm;
        }



        // --- NEW EXPORT LOGIC ---
        private void ExecuteExportProject()
        {
            var systems = _dataService.AllSystems;

            if (systems == null || !systems.Any())
            {
                MessageBox.Show("No systems available to export.", "Export Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Initialize the dialog ViewModel
            var dialogViewModel = new WpfResourceGantt.ProjectManagement.Features.Dialogs.ExportSystemDialogViewModel(systems.ToList());

            // 2. Define what happens when the dialog closes
            dialogViewModel.OnCloseRequest += async (confirmed) =>
            {
                CurrentDialogViewModel = null; // Close the dialog overlay

                // 3. If the user clicked Export and selected a system
                if (confirmed && dialogViewModel.SelectedSystem != null)
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Microsoft Project Files (*.mpp)|*.mpp",
                        Title = "Export Microsoft Project File",
                        // Pre-fill the file name with the System Name
                        FileName = $"{dialogViewModel.SelectedSystem.Name.Replace(" ", "_")}.mpp"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        try
                        {
                            var exporter = new MppExportService();

                            // CHANGE: Get all users from the DataService
                            var allUsers = _dataService.AllUsers;

                            // 4. Run COM interop on a background thread to prevent UI freezing
                            // CHANGE: Pass allUsers as the third parameter
                            await Task.Run(() => exporter.ExportMppFile(saveFileDialog.FileName, dialogViewModel.SelectedSystem, allUsers));

                            MessageBox.Show("Project exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error exporting file:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            };

            // 5. Show the dialog
            CurrentDialogViewModel = dialogViewModel;
        }

        private void ShowImportTestBlocksDialog()
        {
            WorkBreakdownItem contextItem = null;

            if (CurrentViewModel is GateProgressViewModel gateVm)
            {
                contextItem = _dataService.GetWorkBreakdownItemById(gateVm.SubProject.Id);
            }

            var dialogVm = new ImportTestBlocksDialogViewModel(_dataService, contextItem);

            dialogVm.OnCloseRequest += (success) =>
            {
                CurrentDialogViewModel = null;
                if (success)
                {
                    // REFRESH LOGIC
                    if (CurrentViewModel is GateProgressViewModel currentGateVm)
                    {
                        // Use the new refresh method that re-fetches the object from the DB
                        currentGateVm.RefreshFromDataService();
                    }
                    else if (CurrentViewModel is GanttViewModel gantt)
                    {
                        gantt.RefreshGanttView();
                    }
                }
            };

            CurrentDialogViewModel = dialogVm;
        }
        private void ShowProjectGanttView()
        {
            if (GanttViewModel == null)
            {
                // First time loading: Create the instance
                GanttViewModel = new GanttViewModel(this, _dataService);
            }
            else
            {
                // Subsequent loads: Reuse the instance.
                // Call RefreshGanttView() to load latest DB changes (like Task Progress)
                // while using its internal logic to remember which items were expanded.
                GanttViewModel.RefreshGanttView();
            }

            CurrentViewModel = GanttViewModel;
        }


        private void ShowDashboardView()
        {
            CurrentViewModel = new DashboardViewModel(_dataService, CurrentUser);
        }



        public void GoToGateProgress(WorkItem subProject)
        {
            if (subProject == null) return;

            // Remove the 'freshSubProject' fetch here if it causes type conflicts.
            // The GateProgressViewModel constructor will use this 'subProject' reference.

            string previousView = CurrentView;
            Action backAction = CreateBackAction(previousView);

            CurrentView = "GateProgress";

            // FIX: Pass subProject directly. 
            // Since it is a reference type, changes made in the Gate view 
            // will reflect in the Gantt view automatically.
            CurrentViewModel = new GateProgressViewModel(this, subProject, backAction, _dataService);
        }

        private async void ShowCreateUserDialog()
        {
            var dialogViewModel = new CreateUserDialogViewModel();
            var dialog = new CreateUserDialog
            {
                DataContext = dialogViewModel,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                // 1. Get the new User model from the dialog's ViewModel (which handles all fields including Section)
                var newUser = dialogViewModel.GetUserObject();

                // 2. Save the new user to the database
                await _dataService.SaveUserAsync(newUser);

                // Note: UI refresh is now handled automatically by the OnDataChanged subscriber.
            }
        }



        private void ShowUserManagementView()
        {
            // FIX: Ensure the context exists and is assigned to the property
            if (UserManagementContext == null)
            {
                UserManagementContext = new UserManagementViewModel(_dataService);
            }
            else
            {
                UserManagementContext.RefreshUserList();
            }

            CurrentViewModel = UserManagementContext;
        }

        private void ShowEVMView()
        {
            CurrentViewModel = new EVMViewModel(_dataService, CurrentUser);
        }

        private void ShowSystemManagementView()
        {
            if (SystemManagementContext == null)
            {
                SystemManagementContext = new SystemManagementViewModel(_dataService, CurrentUser, this, _templateService);
            }
            else
            {
                // Refresh the data but keep the VM alive
                SystemManagementContext.RefreshData();
            }

            CurrentViewModel = SystemManagementContext;
        }

        public async void ShowAssignDialog(WorkItem workItem, Action onAssigned = null)
        {
            if (workItem == null) return;

            WorkBreakdownItem fullWorkItem = _dataService.GetWorkBreakdownItemById(workItem.Id);
            if (fullWorkItem == null) return;

            // FIX: Pass the collection, not the single ID string
            var dialogViewModel = new AssignDeveloperViewModel(
                _dataService.AllUsers,
                fullWorkItem.Assignments?.ToList() ?? new List<ResourceAssignment>()
            );

            CurrentDialogViewModel = dialogViewModel;

            dialogViewModel.OnCloseRequest += async (confirmed) =>
            {
                // Close the UI immediately
                CurrentDialogViewModel = null;

                if (confirmed)
                {
                    // 1. Sync the Assignments list (Handling Unassigns/Removals)
                    fullWorkItem.Assignments.Clear();
                    foreach (var wrapper in dialogViewModel.CurrentAssignments)
                    {
                        fullWorkItem.Assignments.Add(wrapper.Assignment);
                    }

                    // 2. Handle the NEW assignment if one was selected in the dropdown
                    if (dialogViewModel.SelectedDeveloper != null)
                    {
                        // If Primary, we maintain the "One Primary" rule for the legacy field
                        if (dialogViewModel.Role == Models.AssignmentRole.Primary)
                        {
                            fullWorkItem.Assignments.RemoveAll(a => a.Role == Models.AssignmentRole.Primary);
                            fullWorkItem.AssignedDeveloperId = dialogViewModel.SelectedDeveloper.Id;
                        }

                        // Avoid duplicates
                        if (!fullWorkItem.Assignments.Any(a => a.DeveloperId == dialogViewModel.SelectedDeveloper.Id && a.Role == dialogViewModel.Role))
                        {
                            fullWorkItem.Assignments.Add(new ResourceAssignment
                            {
                                Id = Guid.NewGuid().ToString(),
                                WorkItemId = fullWorkItem.Id,
                                DeveloperId = dialogViewModel.SelectedDeveloper.Id,
                                Role = dialogViewModel.Role
                            });
                        }
                    }

                    // 3. Update the UI Wrapper text (DeveloperName)
                    if (fullWorkItem.Assignments.Any())
                    {
                        var allNames = _dataService.AllUsers
                            .Where(u => fullWorkItem.Assignments.Any(a => a.DeveloperId == u.Id))
                            .Select(u => u.Name);

                        workItem.DeveloperName = string.Join(", ", allNames);

                        // Update the legacy ID to the first Primary (or first available)
                        fullWorkItem.AssignedDeveloperId = fullWorkItem.Assignments
                            .FirstOrDefault(a => a.Role == Models.AssignmentRole.Primary)?.DeveloperId
                            ?? fullWorkItem.Assignments.FirstOrDefault()?.DeveloperId;
                    }
                    else
                    {
                        workItem.DeveloperName = "Unassigned";
                        fullWorkItem.AssignedDeveloperId = null;
                    }

                    // 4. Save and Refresh
                    await _dataService.SaveDataAsync();
                    onAssigned?.Invoke();
                }
            };
            CurrentDialogViewModel = dialogViewModel;
        }

        private void ShowAssignDeveloperDialog(WorkItem workItem)
        {
            ShowAssignDialog(workItem);
        }

        /// <summary>
        /// Attempts to refresh the Resource Gantt view after data changes.
        /// Finds the Resource Gantt MainViewModel and calls its refresh method.
        /// </summary>
        /// <summary>
        /// Attempts to refresh the Resource Gantt view after data changes.
        /// Finds the Resource Gantt MainViewModel and calls its refresh method.
        /// </summary>
        private async void TryRefreshResourceGanttView()
        {
            try
            {
                // Logic updated: We are the MainViewModel and we hold the context.
                if (GanttContext != null)
                {
                    // Refresh data in context (assuming we loaded new data via LoadDataAsync previously or triggers)
                    // But wait, the caller (AssignDeveloper) saves data to DB.
                    // We need to reload data from DB to refresh current view.
                    await RefreshFromDatabase();
                }
            }
            catch (Exception ex)
            {
                // Silently fail - don't interrupt the user's workflow
                System.Diagnostics.Debug.WriteLine($"Failed to refresh Resource Gantt: {ex.Message}");
            }
        }

        // Helper to refresh everything
        private async Task RefreshFromDatabase()
        {
            await _dataService.LoadDataAsync();
            var resources = await _dataService.GetResourceGanttDataAsync();
            var unassigned = await _dataService.GetUnassignedGanttTasksAsync();
            GanttContext?.LoadData(resources);
            AnalyticsContext?.LoadData(resources, unassigned);
        }

        // 1. CREATE MODE
        private void ShowCreateSystemDialog()
        {
            ShowSystemManagementView();
            if (CurrentViewModel is SystemManagementViewModel systemMgmt)
            {
                systemMgmt.ShowAddSystemCommand.Execute(null);
            }
        }


        private async void SetBaseline(WorkItem systemWorkItem)
        {
            if (systemWorkItem == null || systemWorkItem.ItemType != WorkItemType.System)
            {
                MessageBox.Show("Baseline can only be set on a top-level System.", "Action Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to set the baseline for this system?\n\nThis will lock in the planned work as the Budget at Completion (BAC). This action cannot be undone.",
                "Confirm Baseline",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SystemItem fullSystem = _dataService.GetSystemById(systemWorkItem.Id);
                if (fullSystem != null)
                {
                    _dataService.CalculateAndSetBAC(fullSystem);
                    await _dataService.SaveDataAsync();
                    (CurrentViewModel as GanttViewModel)?.RefreshGanttView();
                    MessageBox.Show("Baseline has been set successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // 2. EDIT MODE
        private void ShowEditSystemDialog(WorkItem workItemToEdit)
        {
            if (workItemToEdit == null) return;
            ShowSystemManagementView();
            // In the new hierarchical system, items are edited directly in the table.
            // We can add logic later to find and scroll to the item if needed.
        }

        // === VIEW TAB IMPLEMENTATIONS ===
        private void ZoomIn()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.ZoomLevel = Math.Min(gantt.ZoomLevel + 20, 300); // Cap at 300
            }
        }

        private void ZoomOut()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.ZoomLevel = Math.Max(gantt.ZoomLevel - 20, 20); // Floor at 20
            }
        }

        private void ResetZoom()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.ZoomLevel = 120; // Default
            }
        }

        private void ExpandAll()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.ExpandAllCommand.Execute(null);
            }
        }

        private void CollapseAll()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.CollapseAllCommand.Execute(null);
            }
        }

        private void SetTimeRange(object rangeObj)
        {
            if (rangeObj == null) return;
            string rangeStr = rangeObj.ToString();

            if (Enum.TryParse(typeof(TimeRange), rangeStr, out var result))
            {
                CurrentTimeRange = (TimeRange)result;
            }
        }

        private void ApplyTimeRangeToCurrentView()
        {
            if (IsGanttVisible)
            {
                GanttContext?.SetTimeRangeCommand.Execute(CurrentTimeRange.ToString());
            }
            else if (IsAnalyticsVisible)
            {
                AnalyticsContext?.SetAnalyticsRangeCommand.Execute(GetDaysFromTimeRange(CurrentTimeRange));
            }
        }

        private int GetDaysFromTimeRange(TimeRange range)
        {
            return range switch
            {
                TimeRange.Days30 => 30,
                TimeRange.Days60 => 60,
                TimeRange.Days90 => 90,
                TimeRange.Days180 => 180,
                TimeRange.Year1 => 365,
                TimeRange.Year3 => 1095,
                TimeRange.All => 3650,
                _ => 30
            };
        }

        // === DATA TAB IMPLEMENTATIONS ===
        private void ImportHours()
        {
            if (CurrentViewModel is GanttViewModel gantt)
            {
                gantt.ImportHoursCommand.Execute(null);
            }
            else
            {
                MessageBox.Show("Please switch to the Resource Gantt view to import timesheets.", "View Requirement", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportToExcel()
        {
            MessageBox.Show("Export to Excel feature is coming soon!", "Feature Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ExecuteReconstructProject()
        {
            // 1. Pick File
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Data Files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Select SMTS Data File to Reconstruct Project"
            };

            if (openFileDialog.ShowDialog() != true) return;

            string originalFilePath = openFileDialog.FileName;
            string processingFilePath = originalFilePath;
            bool isTempFile = false;

            try
            {
                // 2. Conversion Logic (Excel -> Temp CSV)
                string extension = Path.GetExtension(originalFilePath).ToLower();
                if (extension == ".xlsx" || extension == ".xls")
                {
                    try
                    {
                        processingFilePath = ConvertExcelToCsv(originalFilePath);
                        isTempFile = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to convert Excel file. Ensure Excel is installed.\nError: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 3. Pre-Scan for numbers
                var csvService = new CsvImportService(_dataService);
                var (sysNum, projNum) = csvService.ScanCsvForNumbers(processingFilePath);

                // 4. Create Dialog VM
                var dialogVm = new WpfResourceGantt.ProjectManagement.Features.Dialogs.ReconstructProjectDialogViewModel(_dataService.AllSystems, sysNum, projNum);

                // 5. Manually Show Dialog & Subscribe to Close (to ensure cleanup happens AFTER user action)
                CurrentDialogViewModel = dialogVm;

                dialogVm.OnCloseRequest += async (confirmed) =>
                {
                    // Close the UI immediately
                    CurrentDialogViewModel = null;

                    try
                    {
                        if (confirmed)
                        {
                            string sysId = dialogVm.IsCreateNewSystem ? null : dialogVm.SelectedSystem?.Id;

                            var result = await csvService.ReconstructProjectFromCsvAsync(
                                processingFilePath, // File still exists here now!
                                sysId,
                                dialogVm.NewSystemName,
                                dialogVm.NewSystemNumber,
                                dialogVm.ProjectName,
                                dialogVm.ProjectNumber
                            );

                            if (result.Errors.Any())
                            {
                                MessageBox.Show($"Reconstruction completed with errors:\n{string.Join("\n", result.Errors)}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            else
                            {
                                MessageBox.Show("Project structure reconstructed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }

                            // Refresh View
                            if (CurrentViewModel is GanttViewModel gantt) gantt.RefreshGanttView();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        // 6. Cleanup Temp File (Runs after confirm OR cancel)
                        if (isTempFile && !string.IsNullOrEmpty(processingFilePath) && File.Exists(processingFilePath))
                        {
                            try { File.Delete(processingFilePath); } catch { /* Ignore cleanup errors */ }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                // Fallback cleanup if something fails before the dialog even opens
                if (isTempFile && File.Exists(processingFilePath)) try { File.Delete(processingFilePath); } catch { }
                MessageBox.Show($"Error preparing import: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Uses Microsoft.Office.Interop.Excel to save the active sheet as a CSV.
        /// Warning: Requires Excel to be installed on the machine.
        /// </summary>
        private string ConvertExcelToCsv(string excelFilePath)
        {
#if ENABLE_MSPROJECT
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            string tempCsv = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

            try
            {
                excelApp = new Excel.Application
                {
                    Visible = false,
                    DisplayAlerts = false
                };

                workbook = excelApp.Workbooks.Open(excelFilePath);

                // Save the Active Sheet as CSV
                workbook.SaveAs(tempCsv, Excel.XlFileFormat.xlCSV);
            }
            finally
            {
                // Strict COM Cleanup to prevent "Ghost" Excel processes
                if (workbook != null)
                {
                    workbook.Close(false);
                    Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                }
            }

            return tempCsv;
#else
            throw new NotSupportedException("Excel import is disabled in this build configuration. To enable, set <UseMsProject>true</UseMsProject> in the .csproj file.");
#endif
        }
        private async void RefreshData()
        {
            await _dataService.LoadDataAsync();
            AllUsers = _dataService.AllUsers;
            OnPropertyChanged(nameof(AllUsers));

            // Load Resource Gantt Data
            var resources = await _dataService.GetResourceGanttDataAsync();
            var unassigned = await _dataService.GetUnassignedGanttTasksAsync();
            GanttContext.LoadData(resources);
            AnalyticsContext.LoadData(resources, unassigned);
        }

        /// <summary>
        /// Executes the "Close Week" operation: takes a frozen EVM snapshot for every
        /// SubProject. Should be called by PM at end of reporting week (after the final
        /// SMTS import for that week has been done).
        /// </summary>
        private async Task ExecuteCloseWeekAsync()
        {
            DateTime weekEnding = DataService.GetWeekEndingDate(DateTime.Today);

            var confirm = MessageBox.Show(
                $"This will capture a frozen EVM snapshot for all SubProjects.\n\n" +
                $"Week Ending: {weekEnding:MMMM dd, yyyy} (Sunday)\n\n" +
                $"Make sure the SMTS CSV import for this week has been completed before proceeding.\n\n" +
                $"Continue?",
                "Close Week — EVM Snapshot",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int count = await _dataService.TakeWeeklySnapshotsAsync(CurrentUser?.Id ?? "system");

                MessageBox.Show(
                    $"Week closed successfully.\n\n" +
                    $"{count} SubProject snapshot(s) recorded for week ending {weekEnding:MMM dd, yyyy}.\n\n" +
                    $"Navigate to the EVM view to review the updated S-Curve.",
                    "Week Closed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Refresh EVM view if it's active
                if (CurrentViewModel is EVMViewModel evmVm)
                {
                    evmVm.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to close week:\n\n{ex.Message}",
                    "Close Week Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void ShowQuickTasksView()
        {
            CurrentViewModel = new QuickTasksViewModel(_dataService, CurrentUser);
        }

        /// <summary>
        /// Shows a modal dialog over the current view.
        /// </summary>
        /// <param name="dialogViewModel">The ViewModel for the dialog content.</param>
        /// <param name="onConfirm">Action to execute if the user confirms.</param>
        public void ShowModalCustomDialog(ViewModelBase dialogViewModel, Action onConfirm)
        {
            CurrentDialogViewModel = dialogViewModel;

            // We subscribe to the specific close event of the ApplyTemplateDialog
            if (dialogViewModel is ApplyTemplateDialogViewModel templateVm)
            {
                templateVm.OnCloseRequest += (confirmed) =>
                {
                    CurrentDialogViewModel = null; // Hide the dialog
                    if (confirmed)
                    {
                        onConfirm?.Invoke();
                    }
                };
            }
            else if (dialogViewModel is Features.AssignDeveloper.AssignDeveloperViewModel assignVm)
            {
                assignVm.OnCloseRequest += (confirmed) =>
                {
                    CurrentDialogViewModel = null; // Hide the dialog
                    if (confirmed)
                    {
                        onConfirm?.Invoke();
                    }
                };
            }
            else if (dialogViewModel is ReconstructProjectDialogViewModel reconstructVm)
            {
                reconstructVm.OnCloseRequest += (confirmed) =>
                {
                    CurrentDialogViewModel = null;
                    if (confirmed) onConfirm?.Invoke();
                };
            }
            // You can add 'else if' blocks here for other future dialog types
        }

        private Action CreateBackAction(string previousView)
        {
            return () =>
            {
                // FIX: Instead of calling the Show methods directly, we set the property.
                // This ensures the internal string state is updated, which fixes the 
                // "cannot go back twice" bug.
                CurrentView = previousView;
            };
        }

    }
}
