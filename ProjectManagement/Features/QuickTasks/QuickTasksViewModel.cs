using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;
using TaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus;

namespace WpfResourceGantt.ProjectManagement.Features.QuickTasks
{
    public class QuickTasksViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private readonly User _currentUser;
        private string? _editingTaskId;
        public ObservableCollection<AdminTask> PendingTasks { get; set; } = new ObservableCollection<AdminTask>();
        public ObservableCollection<AdminTask> CompletedTasks { get; set; } = new ObservableCollection<AdminTask>();
        public ObservableCollection<User> AllUsers { get; set; } = new ObservableCollection<User>();

        // New Task Modal Properties

        private User _selectedFilterUser;
        public User SelectedFilterUser
        {
            get => _selectedFilterUser;
            set
            {
                _selectedFilterUser = value;
                OnPropertyChanged();
                LoadTasks(); // Re-filter whenever the selection changes
            }
        }

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

        private string _newTaskDescription;
        public string NewTaskDescription
        {
            get => _newTaskDescription;
            set { _newTaskDescription = value; OnPropertyChanged(); }
        }

        private DateTime _newTaskStartDate;
        public DateTime NewTaskStartDate
        {
            get => _newTaskStartDate;
            set { _newTaskStartDate = value; OnPropertyChanged(); }
        }

        private DateTime _newTaskEndDate;
        public DateTime NewTaskEndDate
        {
            get => _newTaskEndDate;
            set { _newTaskEndDate = value; OnPropertyChanged(); }
        }

        private User _selectedAssignee;
        public User SelectedAssignee
        {
            get => _selectedAssignee;
            set { _selectedAssignee = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ToggleTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand OpenAddTaskCommand { get; }
        public ICommand CancelAddTaskCommand { get; }
        public ICommand SaveNewTaskCommand { get; }
        public ICommand OpenEditTaskCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public QuickTasksViewModel(DataService dataService, User currentUser)
        {
            _dataService = dataService;
            _currentUser = currentUser;

            ToggleTaskCommand = new RelayCommand<AdminTask>(async (t) => await ToggleTaskAsync(t));
            DeleteTaskCommand = new RelayCommand<AdminTask>(async (t) => await DeleteTaskAsync(t));

            OpenAddTaskCommand = new RelayCommand(OpenAddTask);
            CancelAddTaskCommand = new RelayCommand(() => IsAddTaskModalOpen = false);
            SaveNewTaskCommand = new RelayCommand(async () => await SaveNewTaskAsync());
            OpenEditTaskCommand = new RelayCommand<AdminTask>(OpenEditTask);
            ClearFilterCommand = new RelayCommand(() => SelectedFilterUser = null);
            // Subscribe to data changes
            _dataService.DataChanged += OnDataChanged;

            // Initial Load
            PopulateUsers();
            LoadTasks();
        }

        private async void OnDataChanged(object sender, EventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 1. Save the current selection ID
                var currentFilterId = SelectedFilterUser?.Id;

                // 2. Refresh the user list
                PopulateUsers();

                // 3. Restore the selection from the new list
                if (!string.IsNullOrEmpty(currentFilterId))
                {
                    SelectedFilterUser = AllUsers.FirstOrDefault(u => u.Id == currentFilterId);
                }

                // 4. Reload tasks (LoadTasks already respects SelectedFilterUser)
                LoadTasks();
            });
        }

        private void PopulateUsers()
        {
            AllUsers.Clear();
            if (_dataService.AllUsers != null)
            {
                foreach (var user in _dataService.AllUsers.OrderBy(u => u.Name))
                {
                    AllUsers.Add(user);
                }
            }
        }

        private async void LoadTasks()
        {
            if (_currentUser == null) return;

            var allTasks = await _dataService.GetAllAdminTasksAsync();

            PendingTasks.Clear();
            CompletedTasks.Clear();

            var users = _dataService.AllUsers; // Local cache or property access

            foreach (var task in allTasks)
            {
                // 1. Map user name for display
                var user = users?.FirstOrDefault(u => u.Id == task.AssignedUserId);
                task.AssignedUserName = user?.Name ?? task.AssignedUserId;

                // 2. APPLY FILTER LOGIC
                // If a filter is selected, skip tasks that don't belong to that user
                if (SelectedFilterUser != null && task.AssignedUserId != SelectedFilterUser.Id)
                {
                    continue;
                }

                // 3. Categorize by status
                if (task.Status == TaskStatus.Completed)
                {
                    CompletedTasks.Add(task);
                }
                else
                {
                    PendingTasks.Add(task);
                }
            }
        }

        private async Task ToggleTaskAsync(AdminTask task)
        {
            if (task == null) return;

            // Flip Status
            if (task.Status == TaskStatus.Completed)
            {
                task.Status = TaskStatus.InWork; // Or NotStarted, but InWork is safer for "Pending"
            }
            else
            {
                task.Status = TaskStatus.Completed;
            }

            await _dataService.UpdateAdminTaskAsync(task);
            // OnDataChanged will reload the lists
        }

        private async Task DeleteTaskAsync(AdminTask task)
        {
            if (task == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete '{task.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _dataService.DeleteAdminTaskAsync(task.Id);
            }
        }

        private void OpenAddTask()
        {
            _editingTaskId = null;
            // Populate user list
            AllUsers.Clear();
            if (_dataService.AllUsers != null)
            {
                foreach (var user in _dataService.AllUsers.OrderBy(u => u.Name))
                {
                    AllUsers.Add(user);
                }
            }

            NewTaskName = "New Task";
            NewTaskDescription = "";
            NewTaskStartDate = DateTime.Today;
            NewTaskEndDate = DateTime.Today.AddDays(1);

            // 2. DEFAULT LOGIC: 
            // Look for Alexis Manriquez first. 
            // If not found, fall back to the current user, then the first person in the list.
            SelectedAssignee = AllUsers.FirstOrDefault(u => u.Name.Equals("Alexis Manriquez", StringComparison.OrdinalIgnoreCase))
                               ?? AllUsers.FirstOrDefault(u => u.Id == _currentUser?.Id)
                               ?? AllUsers.FirstOrDefault();

            IsAddTaskModalOpen = true;
        }


        // Method Logic:
        private void OpenEditTask(AdminTask task)
        {
            if (task == null) return;

            // 1. Populate Users list for the dropdown
            AllUsers.Clear();
            foreach (var user in _dataService.AllUsers.OrderBy(u => u.Name))
            {
                AllUsers.Add(user);
            }

            // 2. Map existing task data to the modal properties
            _editingTaskId = task.Id;
            NewTaskName = task.Name;
            NewTaskDescription = task.Description;
            NewTaskStartDate = task.StartDate;
            NewTaskEndDate = task.EndDate;

            // 3. Set the assignee in the dropdown
            SelectedAssignee = AllUsers.FirstOrDefault(u => u.Id == task.AssignedUserId);

            IsAddTaskModalOpen = true;
        }

        private async Task SaveNewTaskAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTaskName))
            {
                MessageBox.Show("Task Name requires a value.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var assignedId = SelectedAssignee?.Id ?? _currentUser.Id;

            if (!string.IsNullOrEmpty(_editingTaskId))
            {
                // EDIT MODE: Find the task and update it
                var allTasks = await _dataService.GetAllAdminTasksAsync();
                var task = allTasks.FirstOrDefault(t => t.Id == _editingTaskId);
                if (task != null)
                {
                    task.Name = NewTaskName;
                    task.Description = NewTaskDescription;
                    task.StartDate = NewTaskStartDate;
                    task.EndDate = NewTaskEndDate;
                    task.AssignedUserId = assignedId;

                    await _dataService.UpdateAdminTaskAsync(task);
                }
            }
            else
            {
                // CREATE MODE: Existing logic
                await _dataService.CreateAdminTaskAsync(
                    NewTaskName,
                    assignedId,
                    NewTaskStartDate,
                    NewTaskEndDate,
                    TaskStatus.InWork,
                    NewTaskDescription
                );
            }

            IsAddTaskModalOpen = false;
        }
    }
}
