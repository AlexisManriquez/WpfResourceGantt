using System;
using System.Collections.Generic;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Features.Dialogs
{
    public class ExportSystemDialogViewModel : ViewModelBase
    {
        public List<SystemItem> AvailableSystems { get; }

        private SystemItem _selectedSystem;
        public SystemItem SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                _selectedSystem = value;
                OnPropertyChanged();
                // Using standard command pattern, trigger re-evaluation if CanExecute changes
                (ConfirmCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        // Action callback to notify MainViewModel when the dialog closes
        public Action<bool> OnCloseRequest { get; set; }

        public ExportSystemDialogViewModel(List<SystemItem> systems)
        {
            AvailableSystems = systems;

            // Only allow confirmation if a system is selected
            ConfirmCommand = new RelayCommand(() => OnCloseRequest?.Invoke(true), () => SelectedSystem != null);
            CancelCommand = new RelayCommand(() => OnCloseRequest?.Invoke(false));
        }
    }
}
