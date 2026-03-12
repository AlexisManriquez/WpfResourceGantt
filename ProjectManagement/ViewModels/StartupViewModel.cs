using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

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
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isAutoLoginMode;
        /// <summary>
        /// True when UseWindowsAuthentication is enabled.
        /// Bound in XAML to hide the combobox and show the auto-login UI.
        /// </summary>
        public bool IsAutoLoginMode
        {
            get => _isAutoLoginMode;
            set { _isAutoLoginMode = value; OnPropertyChanged(); }
        }

        private bool _isManualLoginMode;
        /// <summary>
        /// Inverse of IsAutoLoginMode. Used to show the combobox when in dev mode.
        /// </summary>
        public bool IsManualLoginMode
        {
            get => _isManualLoginMode;
            set { _isManualLoginMode = value; OnPropertyChanged(); }
        }

        private string _autoLoginStatusText;
        /// <summary>
        /// Status message displayed during auto-login (e.g., "Logging in as John Doe...")
        /// </summary>
        public string AutoLoginStatusText
        {
            get => _autoLoginStatusText;
            set { _autoLoginStatusText = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        // Action to trigger window closing or main window launch
        public event Action<User> OnLoginSuccess;

        public StartupViewModel()
        {
            _dataService = new DataService();
            Users = new ObservableCollection<User>();
            LoginCommand = new RelayCommand(Login, CanLogin);

            IsAutoLoginMode = AppSettings.UseWindowsAuthentication;
            IsManualLoginMode = !AppSettings.UseWindowsAuthentication;

            LoadUsers();
        }

        private async void LoadUsers()
        {
            IsLoading = true;
            try
            {
                await _dataService.LoadDataAsync();

                if (AppSettings.UseWindowsAuthentication)
                {
                    // === PRODUCTION MODE: Auto-login from Windows identity ===
                    await AttemptAutoLogin();
                }
                else
                {
                    // === DEV MODE: Show combobox with all users (excluding Administrators) ===
                    var allUsers = _dataService.AllUsers
                        .Where(u => u.Role != Role.Administrator)
                        .OrderBy(u => u.GroupOrder)
                        .ThenBy(u => u.Name);
                    Users = new ObservableCollection<User>(allUsers);
                }
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

        /// <summary>
        /// Attempts to find a matching user in the database based on Windows identity.
        /// Handles name formats: "DOMAIN\username", "Lastname, Firstname", "Firstname Lastname"
        /// </summary>
        private async Task AttemptAutoLogin()
        {
            string windowsName = GetWindowsDisplayName();
            AutoLoginStatusText = $"Authenticating: {windowsName}...";

            if (string.IsNullOrEmpty(windowsName))
            {
                AutoLoginStatusText = "Could not determine Windows identity.";
                // Fall back to manual mode
                FallBackToManualMode();
                return;
            }

            // Try to match against all users in the database
            var matchedUser = FindMatchingUser(windowsName);

            if (matchedUser != null)
            {
                AutoLoginStatusText = $"Welcome, {matchedUser.Name}";
                // Small delay so user can see the greeting
                await Task.Delay(800);
                OnLoginSuccess?.Invoke(matchedUser);
            }
            else
            {
                AutoLoginStatusText = $"No matching profile found for \"{windowsName}\".\n\nPlease ask your Section Chief or Flight Chief to add you as a user.";
                // Don't fall back to manual — in production mode, unrecognized users should be blocked
            }
        }

        /// <summary>
        /// Gets the display name of the current Windows user.
        /// Returns the account name portion (e.g., "John.Doe" or "Doe, John").
        /// </summary>
        private string GetWindowsDisplayName()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                if (identity == null) return null;

                // identity.Name is typically "DOMAIN\username" (e.g., "AFNET\john.doe")
                string fullName = identity.Name;

                // Strip the domain prefix
                if (fullName.Contains("\\"))
                    fullName = fullName.Split('\\').Last();

                // Replace dots/underscores with spaces (e.g., "john.doe" → "john doe")
                fullName = fullName.Replace(".", " ").Replace("_", " ");

                return fullName.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fuzzy-matches a Windows identity name against all database users.
        /// Handles:
        ///   - Exact match (case-insensitive)
        ///   - "Doe John" vs "John Doe" (order-independent)
        ///   - "Doe, John" vs "John Doe" (comma-separated variants)
        /// </summary>
        private User FindMatchingUser(string windowsName)
        {
            if (string.IsNullOrWhiteSpace(windowsName)) return null;

            string normalizedInput = NormalizeName(windowsName);

            foreach (var user in _dataService.AllUsers)
            {
                if (string.IsNullOrWhiteSpace(user.Name)) continue;

                string normalizedDbName = NormalizeName(user.Name);

                // 1. Direct match after normalization
                if (string.Equals(normalizedInput, normalizedDbName, StringComparison.OrdinalIgnoreCase))
                    return user;

                // 2. Reversed order match (handles "Doe John" vs "John Doe")
                string reversedInput = ReverseNameParts(normalizedInput);
                if (string.Equals(reversedInput, normalizedDbName, StringComparison.OrdinalIgnoreCase))
                    return user;

                // 3. Also try reversing the DB name
                string reversedDbName = ReverseNameParts(normalizedDbName);
                if (string.Equals(normalizedInput, reversedDbName, StringComparison.OrdinalIgnoreCase))
                    return user;
            }

            return null;
        }

        /// <summary>
        /// Normalizes a name by removing commas, extra spaces, and lowercasing.
        /// "Doe, John"  → "doe john"
        /// "John  Doe"  → "john doe"
        /// </summary>
        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Remove commas, collapse multiple spaces
            string cleaned = name.Replace(",", " ");
            while (cleaned.Contains("  "))
                cleaned = cleaned.Replace("  ", " ");

            return cleaned.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Reverses the order of name parts.
        /// "doe john" → "john doe"
        /// "john doe smith" → "smith doe john"
        /// </summary>
        private string ReverseNameParts(string normalizedName)
        {
            var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Array.Reverse(parts);
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Falls back to manual (combobox) mode if auto-login fails in a recoverable way.
        /// </summary>
        private void FallBackToManualMode()
        {
            IsAutoLoginMode = false;
            IsManualLoginMode = true;

            var allUsers = _dataService.AllUsers
                .Where(u => u.Role != Role.Administrator)
                .OrderBy(u => u.GroupOrder)
                .ThenBy(u => u.Name);
            Users = new ObservableCollection<User>(allUsers);
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
