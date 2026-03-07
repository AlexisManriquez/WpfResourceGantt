using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.ProjectCreation
{
    using WpfResourceGantt.ProjectManagement;
    public class CreateSubProjectViewModel : ViewModelBase
    {
        private readonly Action<CreateSubProjectViewModel> _removeAction;
        public string SubProjectHeader { get; }
        public string SubProjectName { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double Work { get; set; }

        public string Description { get; set; }
        public ICommand RemoveCommand { get; }

        public ICommand AddTaskCommand { get; }

        private bool _isExpanded = true; // Default to expanded when a new project is added
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpansionIconSource)); }
        }
        // This property will dynamically provide the correct icon path
        public string ExpansionIconSource => IsExpanded ? "/ProjectManagement/Icons/minimize-2.png" : "/ProjectManagement/Icons/maximize-2.png";

        public ICommand ToggleExpansionCommand { get; }

        public ObservableCollection<CreateTaskViewModel> Tasks { get; }

        public CreateSubProjectViewModel(int number, Action<CreateSubProjectViewModel> removeAction)
        {
            _removeAction = removeAction;
            SubProjectHeader = $"Subproject {number}";
            RemoveCommand = new RelayCommand(() => _removeAction(this));
            Tasks = new ObservableCollection<CreateTaskViewModel>();
            AddTaskCommand = new RelayCommand(AddTask);
            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        public CreateSubProjectViewModel(SubProject subProjectToEdit, Action<CreateSubProjectViewModel> removeAction)
        {
            _removeAction = removeAction;
            int firstSpace = subProjectToEdit.Name.IndexOf(' ');
            if (firstSpace > -1)
            {
                SubProjectName = subProjectToEdit.Name.Substring(firstSpace + 1);
            }
            else { SubProjectName = subProjectToEdit.Name; }
            StartDate = subProjectToEdit.StartDate;
            EndDate = subProjectToEdit.EndDate;
            Work = subProjectToEdit.Work;

            Tasks = new ObservableCollection<CreateTaskViewModel>(
                subProjectToEdit.Tasks.Select(t => new CreateTaskViewModel(t, task => Tasks.Remove(task)))
            );
            AddTaskCommand = new RelayCommand(AddTask);
            RemoveCommand = new RelayCommand(() => _removeAction(this));
            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        private void AddTask()
        {
            Tasks.Add(new CreateTaskViewModel(Tasks.Count + 1, (task) => Tasks.Remove(task)));
        }

        private void ParseFullName(string fullName, out string number, out string name)
        {
            number = string.Empty;
            name = string.Empty;
            if (string.IsNullOrEmpty(fullName)) return;

            int firstSpace = fullName.IndexOf(' ');
            if (firstSpace > -1)
            {
                string compositeNumber = fullName.Substring(0, firstSpace);
                name = fullName.Substring(firstSpace + 1);

                // The "Number" we want for the textbox is the LAST part of the composite number.
                int lastDash = compositeNumber.LastIndexOf('-');
                number = (lastDash > -1)
                    ? compositeNumber.Substring(lastDash + 1)
                    : compositeNumber; // If no dash, the whole thing is the number (for Systems)
            }
            else
            {
                // Fallback if there's no space in the name
                name = fullName;
            }
        }
    }
}
