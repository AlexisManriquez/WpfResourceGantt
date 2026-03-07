using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Features.Dialogs
{
    public class ReconstructProjectDialogViewModel : ViewModelBase
    {
        public event Action<bool> OnCloseRequest;

        // Choices
        public List<SystemItem> ExistingSystems { get; }

        private bool _isCreateNewSystem = true;
        public bool IsCreateNewSystem
        {
            get => _isCreateNewSystem;
            set { _isCreateNewSystem = value; OnPropertyChanged(); }
        }

        private SystemItem _selectedSystem;
        public SystemItem SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                _selectedSystem = value;
                OnPropertyChanged();
                // If they pick an existing system, we might want to lock/hide the "New System" fields
                if (value != null) IsCreateNewSystem = false;
            }
        }

        // New System Inputs
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

        // Project Inputs
        private string _projectName;
        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        private string _projectNumber;
        public string ProjectNumber
        {
            get => _projectNumber;
            set { _projectNumber = value; OnPropertyChanged(); }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public ReconstructProjectDialogViewModel(List<SystemItem> existingSystems, string detectedSystemNum, string detectedProjectNum)
        {
            ExistingSystems = existingSystems;

            // Auto-fill from CSV pre-scan
            NewSystemNumber = detectedSystemNum;
            ProjectNumber = detectedProjectNum;

            // Default names (User must edit)
            NewSystemName = "New System";
            ProjectName = "New Project";

            ConfirmCommand = new RelayCommand(() => OnCloseRequest?.Invoke(true));
            CancelCommand = new RelayCommand(() => OnCloseRequest?.Invoke(false));
        }
    }
}
