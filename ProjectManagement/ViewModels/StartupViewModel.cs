using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;  // ViewModelBase

namespace WpfResourceGantt.ProjectManagement.ViewModels
{
    public class StartupViewModel : ViewModelBase
    {
        private readonly DataService _dataService;

        private ObservableCollection<User> _users;
        public ObservableCollection<User> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        private User _selectedUser;
        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested(); // This forces all commands to re-evaluate
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        // Action to trigger window closing or main window launch
        public event Action<User> OnLoginSuccess;

        public StartupViewModel()
        {
            _dataService = new DataService();
            Users = new ObservableCollection<User>();
            LoginCommand = new RelayCommand(Login, CanLogin);

            LoadUsers();
        }

        private async void LoadUsers()
        {
            IsLoading = true;
            try
            {
                await _dataService.LoadDataAsync();
                var allUsers = _dataService.AllUsers.OrderBy(u => u.GroupOrder).ThenBy(u => u.Name);
                Users = new ObservableCollection<User>(allUsers);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load users: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanLogin() => SelectedUser != null;

        private void Login()
        {
            if (SelectedUser != null)
            {
                OnLoginSuccess?.Invoke(SelectedUser);
            }
        }
    }
}
