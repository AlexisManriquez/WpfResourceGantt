using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;
using System.Collections.Generic;
using WpfResourceGantt;

namespace WpfResourceGantt.ProjectManagement.Features.DeveloperPortal
{
    public class DeveloperPortalViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private readonly User _currentUser;

        // Assignments
        private ObservableCollection<SystemAssignmentGroup> _assignmentGroups;
        public ObservableCollection<SystemAssignmentGroup> AssignmentGroups
        {
            get => _assignmentGroups;
            set { _assignmentGroups = value; OnPropertyChanged(); }
        }

        private bool _hasAssignments;
        public bool HasAssignments
        {
            get => _hasAssignments;
            set { _hasAssignments = value; OnPropertyChanged(); }
        }

        // Quick Tasks
        private ObservableCollection<AdminTask> _quickTasks;
        public ObservableCollection<AdminTask> QuickTasks
        {
            get => _quickTasks;
            set { _quickTasks = value; OnPropertyChanged(); }
        }

        // Display Properties
        private int _totalAssignments;
        public int TotalAssignments
        {
            get => _totalAssignments;
            set { _totalAssignments = value; OnPropertyChanged(); }
        }

        private int _pendingTasksCount;
        public int PendingTasksCount
        {
            get => _pendingTasksCount;
            set { _pendingTasksCount = value; OnPropertyChanged(); }
        }

        private int _completedTasksCount;
        public int CompletedTasksCount
        {
            get => _completedTasksCount;
            set { _completedTasksCount = value; OnPropertyChanged(); }
        }

        private ObservableCollection<AdminTask> _pendingQuickTasks;
        public ObservableCollection<AdminTask> PendingQuickTasks
        {
            get => _pendingQuickTasks;
            set { _pendingQuickTasks = value; OnPropertyChanged(); }
        }

        private ObservableCollection<AdminTask> _completedQuickTasks;
        public ObservableCollection<AdminTask> CompletedQuickTasks
        {
            get => _completedQuickTasks;
            set { _completedQuickTasks = value; OnPropertyChanged(); }
        }

        private string _welcomeMessage;
        public string WelcomeMessage
        {
            get => _welcomeMessage;
            set { _welcomeMessage = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ToggleQuickTaskCommand { get; }
        public ICommand SwitchUserCommand { get; }
        public ICommand ToggleBlockItemCommand { get; }

        public DeveloperPortalViewModel(DataService dataService, User currentUser)
        {
            _dataService = dataService;
            _currentUser = currentUser;

            RefreshCommand = new RelayCommand(LoadData);
            ToggleQuickTaskCommand = new RelayCommand<AdminTask>(ToggleQuickTask);
            SwitchUserCommand = new RelayCommand(SwitchUser);
            ToggleBlockItemCommand = new RelayCommand<object>(async (param) => await ToggleBlockItem(param));

            LoadData();

            // Listen for global data changes
            _dataService.DataChanged += (s, e) => Application.Current.Dispatcher.Invoke(LoadData);
        }

        private void LoadData()
        {
            // Set Welcome Message
            var firstName = _currentUser.Name?.Split(' ').FirstOrDefault() ?? "User";
            WelcomeMessage = $"Welcome, {firstName}";

            LoadAssignments();
            LoadQuickTasks();
        }
        private void LoadAssignments()
        {
            // 1. CAPTURE CURRENT STATE
            // Remember which Systems and Projects are currently open
            var expandedSystems = AssignmentGroups?
                .Where(g => g.IsExpanded)
                .Select(g => g.SystemName).ToHashSet() ?? new HashSet<string>();

            var expandedProjects = AssignmentGroups?
                .SelectMany(g => g.Projects)
                .Where(p => p.IsExpanded)
                .Select(p => p.ProjectName).ToHashSet() ?? new HashSet<string>();

            var groups = new List<SystemAssignmentGroup>();

            foreach (var system in _dataService.AllSystems)
            {
                var assignedProjects = new List<ProjectAssignmentGroup>();

                if (system.Children != null)
                {
                    foreach (var project in system.Children)
                    {
                        var assignedWorkItems = FindAssignedLeafs(project);

                        if (assignedWorkItems != null && assignedWorkItems.Any())
                        {
                            assignedProjects.Add(new ProjectAssignmentGroup
                            {
                                ProjectName = project.Name,
                                ProjectNumber = project.WbsValue,
                                Assignments = new ObservableCollection<WorkBreakdownItem>(assignedWorkItems),
                                // RESTORE PROJECT STATE
                                IsExpanded = expandedProjects.Contains(project.Name)
                            });
                        }
                    }
                }

                if (assignedProjects.Any())
                {
                    groups.Add(new SystemAssignmentGroup
                    {
                        SystemName = system.Name,
                        DisplayCount = assignedProjects.Sum(p => p.Assignments.Count),
                        Projects = new ObservableCollection<ProjectAssignmentGroup>(assignedProjects),
                        // RESTORE SYSTEM STATE
                        IsExpanded = expandedSystems.Contains(system.Name)
                    });
                }
            }

            AssignmentGroups = new ObservableCollection<SystemAssignmentGroup>(groups);
            HasAssignments = AssignmentGroups.Any();
            TotalAssignments = AssignmentGroups.Sum(g => g.DisplayCount);
        }
        // DeveloperPortalViewModel.cs

        private async Task ToggleBlockItem(object param)
        {
            var item = param as WorkBreakdownItem;
            if (item == null || item.ProgressBlocks == null) return;

            // 1. Recalculate the progress for this specific task card
            var allItems = item.ProgressBlocks.SelectMany(b => b.Items).ToList();
            if (allItems.Count > 0)
            {
                double completedCount = allItems.Count(i => i.IsCompleted);
                item.Progress = completedCount / allItems.Count;
            }

            // 2. Save to the database (just like you do in TaskDetails)
            await _dataService.SaveDataAsync();


        }

        private List<WorkBreakdownItem> FindAssignedLeafs(WorkBreakdownItem parent, HashSet<string>? visited = null)
        {
            if (visited == null) visited = new HashSet<string>();

            var results = new List<WorkBreakdownItem>();

            // Recursion protection
            if (visited.Contains(parent.Id)) return results;
            visited.Add(parent.Id);

            // Direct check
            if (IsAssignedToUser(parent))
            {
                // It's assigned. If it's a leaf (no children), add it.
                if (parent.Children == null || !parent.Children.Any())
                {
                    results.Add(parent);
                }
            }

            // Recurse
            if (parent.Children != null)
            {
                foreach (var child in parent.Children)
                {
                    results.AddRange(FindAssignedLeafs(child, visited));
                }
            }

            return results;
        }

        private bool IsAssignedToUser(WorkBreakdownItem item)
        {
            // Check structured assignments
            if (item.Assignments != null && item.Assignments.Any(a => a.DeveloperId == _currentUser.Id))
                return true;

            // Check legacy field
            if (item.AssignedDeveloperId == _currentUser.Id)
                return true;

            return false;
        }

        private async void LoadQuickTasks()
        {
            try
            {
                var tasks = await _dataService.GetAdminTasksForUserAsync(_currentUser.Id);

                // Split into two lists based on status
                var pending = tasks.Where(t => t.Status != Models.TaskStatus.Completed)
                                   .OrderBy(t => t.EndDate)
                                   .ToList();

                var completed = tasks.Where(t => t.Status == Models.TaskStatus.Completed)
                                     .OrderByDescending(t => t.EndDate)
                                     .ToList();

                // Assign to properties
                PendingQuickTasks = new ObservableCollection<AdminTask>(pending);
                CompletedQuickTasks = new ObservableCollection<AdminTask>(completed);

                // Update counts
                PendingTasksCount = pending.Count;
                CompletedTasksCount = completed.Count;

                // Keep the main list just in case you use it elsewhere, or you can remove it
                QuickTasks = new ObservableCollection<AdminTask>(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading quick tasks: {ex.Message}");
                PendingQuickTasks = new ObservableCollection<AdminTask>();
                CompletedQuickTasks = new ObservableCollection<AdminTask>();
            }
        }

        private async void ToggleQuickTask(AdminTask task)
        {
            if (task == null) return;

            // Toggle Logic
            task.Status = task.Status == Models.TaskStatus.Completed ? Models.TaskStatus.InWork : Models.TaskStatus.Completed;

            await _dataService.UpdateAdminTaskAsync(task);

            // Reload to refresh sorting if needed, or just let UI update via binding
            LoadQuickTasks();
        }

        private void SwitchUser()
        {
            // Restart the app flow
            var startup = new StartupWindow();
            bool? result = startup.ShowDialog();

            if (result == true && startup.Tag is User newUser)
            {
                // We need to swap the MainViewModel context or Reload the Window.
                // Swapping VM is cleaner but MainWindow constructor logic ran once.

                // Delegate to App.xaml.cs for a clean restart
                if (Application.Current is App app)
                {
                    app.RestartWithUser(newUser);
                }
                else
                {
                    // Fallback
                    MessageBox.Show("Unable to restart application context. Please restart manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // Helper Wrappers for UI Grouping
    public class SystemAssignmentGroup : ViewModelBase // Inherit so UI sees changes
    {
        public string SystemName { get; set; }
        public int DisplayCount { get; set; }
        public ObservableCollection<ProjectAssignmentGroup> Projects { get; set; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
    }

    public class ProjectAssignmentGroup : ViewModelBase
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public ObservableCollection<WorkBreakdownItem> Assignments { get; set; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
    }
}
