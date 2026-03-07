using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.AssignDeveloper
{
    using System.Collections.ObjectModel;
    using System.Windows.Input;
    using WpfResourceGantt.ProjectManagement;
    public class AssignmentItemViewModel : ViewModelBase
    {
        public ResourceAssignment Assignment { get; set; }
        public string DeveloperName { get; set; }
    }
    public class AssignDeveloperViewModel : ViewModelBase
    {
        public List<User> Developers { get; }
        public User SelectedDeveloper { get; set; }
        public bool IsUnassignRequested { get; set; }
        public event Action<bool> OnCloseRequest;
        public ObservableCollection<AssignmentItemViewModel> CurrentAssignments { get; }
        public ICommand RemoveAssignmentCommand { get; }
        private Models.AssignmentRole _role = Models.AssignmentRole.Primary;
        public Models.AssignmentRole Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPrimary)); OnPropertyChanged(nameof(IsSecondary)); }
        }

        public bool IsPrimary
        {
            get => Role == Models.AssignmentRole.Primary;
            set { if (value) Role = Models.AssignmentRole.Primary; }
        }

        public bool IsSecondary
        {
            get => Role == Models.AssignmentRole.Secondary;
            set { if (value) Role = Models.AssignmentRole.Secondary; }
        }

        public AssignDeveloperViewModel(List<User> allDevelopers, List<ResourceAssignment> existingAssignments)
        {
            Developers = allDevelopers.ToList();

            // Map existing assignments to the wrapper class
            var wrappers = existingAssignments.Select(a => new AssignmentItemViewModel
            {
                Assignment = a,
                DeveloperName = Developers.FirstOrDefault(d => d.Id == a.DeveloperId)?.Name ?? "Unknown"
            });

            CurrentAssignments = new ObservableCollection<AssignmentItemViewModel>(wrappers);

            // UPDATED: Command logic for the wrapper
            RemoveAssignmentCommand = new RelayCommand<AssignmentItemViewModel>(a => CurrentAssignments.Remove(a));
        }

        // Helper to get user names for the UI list
        public string GetUserName(string userId) => Developers.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";
        public void Close(bool confirmed)
        {
            OnCloseRequest?.Invoke(confirmed);
        }
    }
}
