using WpfResourceGantt.ProjectManagement.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.ProjectCreation
{
    using WpfResourceGantt.ProjectManagement;
    public class CreateWorkItemViewModel : ViewModelBase
    {
        private readonly Action<CreateWorkItemViewModel> _removeAction;
        private readonly int _level; // 0 for System, 1 for Project, 2+ for sub-items
        public List<ResourceAssignment> Assignments { get; set; } = new List<ResourceAssignment>();
        // Store the original ID if editing, to preserve DB relationships
        public string Id { get; set; }
        #region Data Entry Properties
        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        private string _number;
        public string Number { get => _number; set { _number = value; OnPropertyChanged(); } }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double Work { get; set; }

        private WorkItemStatus _status = WorkItemStatus.Active;
        public WorkItemStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    // Cascade status to all children
                    SetChildStatusRecursive(Children, value);
                }
            }
        }

        private void SetChildStatusRecursive(ObservableCollection<CreateWorkItemViewModel> children, WorkItemStatus status)
        {
            foreach (var child in children)
            {
                child._status = status;
                child.OnPropertyChanged(nameof(Status));
                SetChildStatusRecursive(child.Children, status);
            }
        }
        #endregion

        #region UI Display Properties
        public string Header { get; }
        public string NumberLabel { get; }
        public bool IsNumberVisible => _level <= 1; // Show for System (0) and Project (1) only

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpansionIconSource)); }
        }
        public bool IsRemovable => _level > 0;
        public string ExpansionIconSource => IsExpanded ? "/ProjectManagement/Icons/minimize-2.png" : "/ProjectManagement/Icons/maximize-2.png";
        #endregion

        #region Hierarchy Properties
        public ObservableCollection<CreateWorkItemViewModel> Children { get; }
        #endregion

        #region Commands
        public ICommand RemoveCommand { get; }
        public ICommand AddChildCommand { get; }
        public ICommand ToggleExpansionCommand { get; }
        #endregion

        /// <summary>
        /// Constructor for CREATING a new, blank item.
        /// </summary>
        public CreateWorkItemViewModel(int level, string header, Action<CreateWorkItemViewModel> removeAction)
        {
            _level = level;
            Header = header;
            _removeAction = removeAction;
            Id = Guid.NewGuid().ToString(); // Temporary ID

            // Set dynamic labels
            if (level == 0) NumberLabel = "System Number";
            else if (level == 1) NumberLabel = "Project Number";
            else NumberLabel = "Sub-Item Number"; // Not visible, but good practice

            // Initialize collections and commands
            Children = new ObservableCollection<CreateWorkItemViewModel>();
            RemoveCommand = new RelayCommand(() => _removeAction(this));
            AddChildCommand = new RelayCommand(AddChild);
            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        /// <summary>
        /// Constructor for EDITING, populating from an existing data model.
        /// </summary>
        public CreateWorkItemViewModel(WorkBreakdownItem itemToEdit, int level, string header, Action<CreateWorkItemViewModel> removeAction)
            : this(level, header, removeAction) // Chain to the base constructor
        {
            Id = itemToEdit.Id; // Preserve ID
            Assignments = itemToEdit.Assignments ?? new List<ResourceAssignment>();
            // Parse the full name to populate the separate Number and Name fields
            ParseFullName(itemToEdit.Name, out string number, out string name);
            Number = number;
            Name = name;

            // Map the rest of the data
            StartDate = itemToEdit.StartDate;
            EndDate = itemToEdit.EndDate;
            Work = itemToEdit.Work ?? 0;

            // Recursively create ViewModels for all children
            int childCount = 1;
            foreach (var child in itemToEdit.Children)
            {
                var childHeader = $"{header}.{childCount}";
                Children.Add(new CreateWorkItemViewModel(child, level + 1, childHeader, (c) => Children.Remove(c)));
                childCount++;
            }

            // Map status (after children are created - don't cascade during load)
            _status = itemToEdit.Status;
        }
        public CreateWorkItemViewModel(SystemItem systemToEdit, int level, string header, Action<CreateWorkItemViewModel> removeAction)
    : this(level, header, removeAction) // Chain to the base constructor
        {
            Id = systemToEdit.Id; // Preserve ID
            Assignments = new List<ResourceAssignment>();
            // --- 1. PARSE THE SYSTEM'S NAME ---
            ParseFullName(systemToEdit.Name, out string number, out string name);
            Number = number;
            Name = name;

            // --- 2. MAP THE SYSTEM'S DATA ---
            StartDate = null;
            EndDate = null;
            Work = 0;

            // --- 3. RECURSIVELY CREATE VIEWMODELS FOR ALL CHILDREN ---
            // We iterate through the System's children (which are WorkBreakdownItems)
            int childCount = 1;
            foreach (var child in systemToEdit.Children)
            {
                // For each child, we call the OTHER "Edit" constructor (the one that takes a WorkBreakdownItem)
                var childHeader = $"{Header}.{childCount}";
                Children.Add(new CreateWorkItemViewModel(child, level + 1, childHeader, (c) => Children.Remove(c)));
                childCount++;
            }

            // Map status (after children are created - don't cascade during load)
            _status = systemToEdit.Status;
        }

        /// <summary>
        /// Adds a new blank child item to this item's collection.
        /// </summary>
        private void AddChild()
        {
            var childHeader = $"{Header}.{Children.Count + 1}";
            Children.Add(new CreateWorkItemViewModel(_level + 1, childHeader, (child) => Children.Remove(child)));
        }

        /// <summary>
        /// Parses a formatted name string (e.g., "SYS-PRJ Name") into its number and name parts.
        /// </summary>
        private void ParseFullName(string fullName, out string number, out string name)
        {
            number = string.Empty;
            name = string.Empty;
            if (string.IsNullOrEmpty(fullName)) return;

            int firstSpace = fullName.IndexOf(' ');
            if (firstSpace > -1)
            {
                string compositeNumber = fullName.Substring(0, firstSpace);
                string textPart = fullName.Substring(firstSpace + 1);

                // For sub-items (level > 1), we ONLY split if the first part looks like a code
                // (contains '-' or is strictly numeric). Otherwise, the whole thing is the 'name'.
                if (_level > 1 && !compositeNumber.Contains("-") && !double.TryParse(compositeNumber, out _))
                {
                    name = fullName;
                    number = string.Empty;
                    return;
                }

                name = textPart;

                // For the UI textbox, we only want the LAST part of the composite number.
                int lastDash = compositeNumber.LastIndexOf('-');
                number = (lastDash > -1 && _level > 0) // Only split for non-systems
                    ? compositeNumber.Substring(lastDash + 1)
                    : compositeNumber;
            }
            else
            {
                name = fullName;
            }
        }
    }
}
