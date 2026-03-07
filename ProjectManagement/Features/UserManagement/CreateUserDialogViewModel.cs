using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.UserManagement
{
    using WpfResourceGantt.ProjectManagement;
    public class CreateUserDialogViewModel : ViewModelBase
    {
        public bool IsEditMode { get; }
        public string DialogTitle => IsEditMode ? "Edit User" : "Add New User";

        private string _userId; // Keep track of ID if editing

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        private string _section;
        public string Section
        {
            get => _section;
            set { _section = value; OnPropertyChanged(); }
        }

        private Role _selectedRole;
        public Role SelectedRole
        {
            get => _selectedRole;
            set { _selectedRole = value; OnPropertyChanged(); }
        }

        // This property provides the list of all enum values to the ComboBox.
        public IEnumerable<Role> AllRoles { get; }

        public CreateUserDialogViewModel()
        {
            // Use C#'s enum helpers to get all defined roles.
            IsEditMode = false;
            AllRoles = Enum.GetValues(typeof(Role)).Cast<Role>();
            // Set a default selection.
            SelectedRole = AllRoles.FirstOrDefault();
        }

        // Constructor for Editing
        public CreateUserDialogViewModel(User userToEdit)
        {
            IsEditMode = true;
            AllRoles = Enum.GetValues(typeof(Role)).Cast<Role>();

            _userId = userToEdit.Id;
            FullName = userToEdit.Name;
            Section = userToEdit.Section;
            SelectedRole = userToEdit.Role;
        }

        public User GetUserObject()
        {
            return new User
            {
                Id = IsEditMode ? _userId : $"usr-{Guid.NewGuid().ToString().Substring(0, 4).ToLower()}",
                Name = FullName,
                Section = Section,
                Role = SelectedRole
            };
        }
    }
}
