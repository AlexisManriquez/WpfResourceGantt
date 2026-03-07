using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels; // For ViewModelBase

namespace WpfResourceGantt.ProjectManagement.Features.UserManagement
{
    using WpfResourceGantt.ProjectManagement;
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly DataService _dataService;

        public ObservableCollection<User> Users { get; set; }
        private User _selectedUser;
        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    OnPropertyChanged();

                    // This calls the fixed method in RelayCommand.cs
                    (EditUserCommand as WpfResourceGantt.ProjectManagement.RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteUserCommand as WpfResourceGantt.ProjectManagement.RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }


        public UserManagementViewModel(DataService dataService)
        {
            _dataService = dataService;
            Users = new ObservableCollection<User>(_dataService.AllUsers);

            AddUserCommand = new RelayCommand(AddUser);
            EditUserCommand = new RelayCommand(EditUser, () => SelectedUser != null);
            DeleteUserCommand = new RelayCommand(DeleteUser, () => SelectedUser != null);
            // Subscribe to external data changes (like the Refresh button)
            _dataService.DataChanged += OnDataChanged;
        }
        private void OnDataChanged(object sender, System.EventArgs e)
        {
            // Ensure we update the UI collection on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshUserList();
            });
        }

        public void RefreshUserList()
        {
            var updatedList = _dataService.AllUsers.ToList();

            // Sync the ObservableCollection instead of creating a new one 
            // to preserve any active bindings or selections in the DataGrid.
            Users.Clear();
            foreach (var user in updatedList)
            {
                Users.Add(user);
            }
        }
        private void AddUser()
        {
            ShowUserDialog(null);
        }

        private void EditUser() // CHANGE: No parameter needed
        {
            if (SelectedUser == null) return;
            ShowUserDialog(SelectedUser);
        }

        private void DeleteUser() // CHANGE: No parameter needed
        {
            if (SelectedUser == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete {SelectedUser.Name}?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _dataService.DeleteUserAsync(SelectedUser.Id);
            }
        }

        private async void ShowUserDialog(User userToEdit)
        {
            var vm = userToEdit == null
                ? new CreateUserDialogViewModel()
                : new CreateUserDialogViewModel(userToEdit);

            var dialog = new CreateUserDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var resultingUser = vm.GetUserObject();

                // Save to DB - This will trigger DataChanged in the DataService
                await _dataService.SaveUserAsync(resultingUser);

                // REMOVED: manual Users.Add / Users[index] = resultingUser;
                // The OnDataChanged event handler will handle the UI refresh.
            }
        }


    }
}
