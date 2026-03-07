using System;
using System.Collections.Generic;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.Dialogs
{
    public class SelectSystemDialogViewModel : ViewModelBase
    {
        public List<SystemItem> ExistingSystems { get; }

        private SystemItem _selectedSystem;
        public SystemItem SelectedSystem
        {
            get => _selectedSystem;
            set { _selectedSystem = value; OnPropertyChanged(); }
        }

        private string _newSystemName;
        public string NewSystemName
        {
            get => _newSystemName;
            set { _newSystemName = value; OnPropertyChanged(); }
        }
        private string _newSystemNumber;
        public string NewSystemNumber
        {
            get => _newSystemNumber;
            set { _newSystemNumber = value; OnPropertyChanged(); }
        }
        private bool _isCreateNew;
        public bool IsCreateNew
        {
            get => _isCreateNew;
            set { _isCreateNew = value; OnPropertyChanged(); }
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event Action<bool> OnCloseRequest;

        public SelectSystemDialogViewModel(List<SystemItem> systems)
        {
            ExistingSystems = systems;
            SelectedSystem = systems.FirstOrDefault();

            // Default to the next available number
            NewSystemNumber = (systems.Count + 1).ToString();

            ConfirmCommand = new RelayCommand(() => OnCloseRequest?.Invoke(true));
            CancelCommand = new RelayCommand(() => OnCloseRequest?.Invoke(false));
        }
    }
}
