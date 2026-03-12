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
    public class CreateProjectViewModel : ViewModelBase
    {
        private readonly Action<CreateProjectViewModel> _removeAction;
        public string ProjectHeader { get; }
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string Description { get; set; }

        public double Work { get; set; }
        public ObservableCollection<CreateSubProjectViewModel> SubProjects { get; }
        public ICommand AddSubProjectCommand { get; }
        public ICommand RemoveCommand { get; }

        private bool _isExpanded = true; // Default to expanded when a new project is added
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpansionIconSource)); }
        }
        // This property will dynamically provide the correct icon path
        public string ExpansionIconSource => IsExpanded ? "/ProjectManagement/Icons/minimize-2.png" : "/ProjectManagement/Icons/maximize-2.png";

        public ICommand ToggleExpansionCommand { get; }
        public CreateProjectViewModel(int number, Action<CreateProjectViewModel> removeAction)
        {
            _removeAction = removeAction;
            ProjectHeader = $"Project {number}";
            SubProjects = new ObservableCollection<CreateSubProjectViewModel>();
            AddSubProjectCommand = new RelayCommand(AddSubProject);
            RemoveCommand = new RelayCommand(() => _removeAction(this));
            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        public CreateProjectViewModel(Project projectToEdit, Action<CreateProjectViewModel> removeAction)
        {
            _removeAction = removeAction;
            int firstSpace = projectToEdit.Name.IndexOf(' ');
            if (firstSpace > -1)
            {
                string compositeNumber = projectToEdit.Name.Substring(0, firstSpace);
                ProjectName = projectToEdit.Name.Substring(firstSpace + 1);

                // The Project Number is the part after the last dash.
                int lastDash = compositeNumber.LastIndexOf('-');
                if (lastDash > -1)
                {
                    ProjectNumber = compositeNumber.Substring(lastDash + 1);
                }
            }
            else { ProjectName = projectToEdit.Name; }
            Work = projectToEdit.Work;

            SubProjects = new ObservableCollection<CreateSubProjectViewModel>(
                projectToEdit.SubProjects.Select(sp => new CreateSubProjectViewModel(sp, sub => SubProjects.Remove(sub)))
            );
            AddSubProjectCommand = new RelayCommand(AddSubProject);
            RemoveCommand = new RelayCommand(() => _removeAction(this));
            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        private void AddSubProject()
        {
            SubProjects.Add(new CreateSubProjectViewModel(SubProjects.Count + 1, (sub) => SubProjects.Remove(sub)));
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
