using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models.Templates;

namespace WpfResourceGantt.ProjectManagement.Features.ApplyTemplate
{
    public class ApplyTemplateDialogViewModel : ViewModelBase
    {
        // The list of templates fetched from the DB
        public ObservableCollection<ProjectTemplate> Templates { get; }

        private ProjectTemplate _selectedTemplate;
        public ProjectTemplate SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                _selectedTemplate = value;
                OnPropertyChanged();

                // This tells WPF to re-check all Command.CanExecute() methods
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        // Option 1: Append (Default)
        private bool _isAppend = true;
        public bool IsAppend
        {
            get => _isAppend;
            set { _isAppend = value; OnPropertyChanged(); }
        }

        // Option 2: Overwrite
        private bool _isOverwrite;
        public bool IsOverwrite
        {
            get => _isOverwrite;
            set { _isOverwrite = value; OnPropertyChanged(); }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        // Actions to close the dialog and return result
        public event Action<bool> OnCloseRequest;

        public ApplyTemplateDialogViewModel(List<ProjectTemplate> templates)
        {
            Templates = new ObservableCollection<ProjectTemplate>(templates);

            // Select the first one by default if available
            SelectedTemplate = Templates.FirstOrDefault();

            ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
        }

        private bool CanConfirm()
        {
            return SelectedTemplate != null;
        }

        private void Confirm()
        {
            // True indicates "Confirmed"
            OnCloseRequest?.Invoke(true);
        }

        private void Cancel()
        {
            // False indicates "Cancelled"
            OnCloseRequest?.Invoke(false);
        }
    }
}
