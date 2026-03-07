using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.ProjectCreation
{
    using WpfResourceGantt.ProjectManagement;
    public class CreateTaskViewModel : ViewModelBase
    {
        private readonly Action<CreateTaskViewModel> _removeAction;
        public string TaskHeader { get; }
        public ICommand RemoveCommand { get; }

        // --- NEW PROPERTIES FOR TASK DATA ---
        public string TaskName { get; set; }
        public DateTime? StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; } = DateTime.Today.AddDays(7);
        public double Work { get; set; } // Planned work in hours

        public CreateTaskViewModel(int number, Action<CreateTaskViewModel> removeAction)
        {
            _removeAction = removeAction;
            TaskHeader = $"Task {number}";
            RemoveCommand = new RelayCommand(() => _removeAction(this));
        }

        public CreateTaskViewModel(TaskItems taskToEdit, Action<CreateTaskViewModel> removeAction)
        {
            _removeAction = removeAction;
            int firstSpace = taskToEdit.Name.IndexOf(' ');
            if (firstSpace > -1)
            {
                TaskName = taskToEdit.Name.Substring(firstSpace + 1);
            }
            else { TaskName = taskToEdit.Name; }
            StartDate = taskToEdit.StartDate;
            EndDate = taskToEdit.EndDate;
            Work = taskToEdit.Work;
            RemoveCommand = new RelayCommand(() => _removeAction(this));
        }
    }
}
