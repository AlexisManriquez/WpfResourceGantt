using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;
using WpfResourceGantt.ProjectManagement;

namespace WpfResourceGantt.ProjectManagement.Features.SystemManagement
{
    public class SystemHierarchyItemViewModel : ViewModelBase
    {
        private readonly Action<SystemHierarchyItemViewModel> _onApplyTemplate;
        private readonly Action<SystemHierarchyItemViewModel> _onCopy;
        private readonly Action<SystemHierarchyItemViewModel> _onPaste;
        private readonly Action<SystemHierarchyItemViewModel> _onDelete;
        private readonly Action<SystemHierarchyItemViewModel> _onSave;
        private readonly Action<SystemHierarchyItemViewModel> _onAddChild;
        private readonly Action<SystemHierarchyItemViewModel> _onAssign;
        private readonly Action<SystemHierarchyItemViewModel> _onEditDetails;

        private bool _isBaselined;
        public bool IsBaselined
        {
            get => _isBaselined;
            set
            {
                _isBaselined = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInPlanningMode));
            }
        }

        public bool IsInPlanningMode => !IsBaselined;
        public bool IsHoursMode { get; set; }
        private WorkItemType _itemType;
        public WorkItemType ItemType
        {
            get => _itemType;
            set 
            { 
                if (_itemType == value) return;
                _itemType = value; 
                OnPropertyChanged(); 
                RollupHierarchy();
            }
        }

        private ScheduleMode _scheduleMode;
        public ScheduleMode ScheduleMode
        {
            get => _scheduleMode;
            set { _scheduleMode = value; OnPropertyChanged(); }
        }

        private string _id;
        public string Id 
        { 
            get => _id; 
            set { _id = value; OnPropertyChanged(); } 
        }

        private string _wbsValue;
        public string WbsValue 
        { 
            get => _wbsValue; 
            set { _wbsValue = value; OnPropertyChanged(); } 
        }

        private string _name;
        public string Name 
        { 
            get => _name; 
            set 
            { 
                _name = value; 
                ParseName(value);
                OnPropertyChanged(); 
            } 
        }

        private string _localNumber;
        public string LocalNumber
        {
            get => _localNumber;
            set { _localNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private string _localName;
        public string LocalName
        {
            get => _localName;
            set { _localName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(LocalNumber) ? LocalName : $"{LocalNumber} {LocalName}";

        private void ParseName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                LocalNumber = "";
                LocalName = "";
                return;
            }

            if (Level > 2)
            {
                LocalNumber = "";
                LocalName = fullName;
                return;
            }

            int spaceIndex = fullName.IndexOf(' ');
            if (spaceIndex >= 0)
            {
                string fullNumber = fullName.Substring(0, spaceIndex);
                if (fullNumber.Any(char.IsDigit))
                {
                    LocalName = fullName.Substring(spaceIndex + 1).TrimStart();
                    int dashIndex = fullNumber.LastIndexOf('-');
                    if (dashIndex >= 0 && dashIndex < fullNumber.Length - 1)
                        LocalNumber = fullNumber.Substring(dashIndex + 1);
                    else
                        LocalNumber = fullNumber;
                }
                else
                {
                    LocalNumber = "";
                    LocalName = fullName;
                }
            }
            else
            {
                LocalNumber = "";
                LocalName = fullName;
            }
        }

        public bool IsEmptyContainer => (Level == 0 || Level == 1 || Level == 2) && !HasLeafDescendant(this);

        private bool HasLeafDescendant(SystemHierarchyItemViewModel node)
        {
            if (node.Children == null || !node.Children.Any()) return false;
            foreach (var child in node.Children)
            {
                if (child.Level > 2) return true;
                if (HasLeafDescendant(child)) return true;
            }
            return false;
        }

        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => (Level == 0) ? null : _startDate;
            set
            {
                if (_startDate == value) return;
                _startDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationDisplay));

                UpdateDuration();
                RollupHierarchy();
            }
        }

        private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => (Level == 0) ? null : _endDate;
            set
            {
                if (_endDate == value) return;
                _endDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationDisplay));
                UpdateDuration();
            }
        }

        private int? _durationDays;
        public int? DurationDays
        {
            get => IsEmptyContainer ? null : _durationDays;
            set
            {
                if (_durationDays == value) return;
                _durationDays = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationDisplay));
                // OPTIMIZATION: Only roll up if NOT in explicit edit mode (programmatic changes).
                // When in Edit Mode, the SaveCommand will handle the rollup at the end.
                if (!IsEditingDuration)
                {
                    RollupHierarchy();
                }
            }
        }

        private string? _predecessors;
        public string? Predecessors
        {
            get => IsEmptyContainer ? null : _predecessors;
            set
            {
                if (_predecessors == value) return;
                _predecessors = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PredecessorDisplayText));
                OnPropertyChanged(nameof(PredecessorEditValue));
                // OPTIMIZATION: Only roll up if NOT in explicit edit mode.
                if (!IsEditingPredecessors)
                {
                    RollupHierarchy();
                }
            }
        }

        public string PredecessorDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Predecessors)) return null;
                var root = this;
                while (root.Parent != null) root = root.Parent;
                var parts = Predecessors.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var displayParts = new System.Collections.Generic.List<string>();

                foreach (var part in parts)
                {
                    var token = part.Trim();
                    var depRegex = new System.Text.RegularExpressions.Regex(@"(FS|SS|FF|SF)([+-]\d+(?:d|w)?)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var suffixMatch = depRegex.Match(token);
                    string idPart = suffixMatch.Success && suffixMatch.Index > 0 ? token.Substring(0, suffixMatch.Index).Trim() : token;
                    string suffix = suffixMatch.Success ? token.Substring(suffixMatch.Index) : "";

                    var found = FindItemById(root, idPart);
                    if (found != null && !string.IsNullOrEmpty(found.WbsValue))
                        displayParts.Add(found.WbsValue + suffix);
                    else if (idPart.Length > 12)
                        displayParts.Add(idPart.Substring(0, 10) + ".." + suffix);
                    else
                        displayParts.Add(token);
                }
                return string.Join(", ", displayParts);
            }
        }

        public string? PredecessorEditValue
        {
            get => PredecessorDisplayText;
            set => Predecessors = ResolvePredecessors(value);
        }

        public string? ResolvePredecessors(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var root = this;
            while (root.Parent != null) root = root.Parent;
            var parts = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var idParts = new System.Collections.Generic.List<string>();

            foreach (var part in parts)
            {
                var token = part.Trim();
                var depRegex = new System.Text.RegularExpressions.Regex(@"(FS|SS|FF|SF)([+-]\d+(?:d|w)?)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var suffixMatch = depRegex.Match(token);
                string labelPart = suffixMatch.Success && suffixMatch.Index > 0 ? token.Substring(0, suffixMatch.Index).Trim() : token;
                string suffix = suffixMatch.Success ? token.Substring(suffixMatch.Index) : "";

                var found = FindItemByLabel(root, labelPart);
                if (found != null)
                    idParts.Add(found.Id + suffix);
                else
                    idParts.Add(token);
            }
            return string.Join(", ", idParts);
        }

        private SystemHierarchyItemViewModel FindItemByLabel(SystemHierarchyItemViewModel node, string label)
        {
            if (node.WbsValue == label || node.Name == label) return node;
            foreach (var child in node.Children)
            {
                var found = FindItemByLabel(child, label);
                if (found != null) return found;
            }
            return null;
        }

        private SystemHierarchyItemViewModel FindItemById(SystemHierarchyItemViewModel node, string id)
        {
            if (node.Id == id) return node;
            foreach (var child in node.Children)
            {
                var found = FindItemById(child, id);
                if (found != null) return found;
            }
            return null;
        }

        private DateTime? _startNoEarlierThan;
        public DateTime? StartNoEarlierThan
        {
            get => IsEmptyContainer ? null : _startNoEarlierThan;
            set
            {
                if (_startNoEarlierThan == value) return;
                _startNoEarlierThan = value;
                OnPropertyChanged();
                RollupHierarchy();
            }
        }

        private bool _isCritical;
        public bool IsCritical
        {
            get => _isCritical;
            set { _isCritical = value; OnPropertyChanged(); }
        }

        private bool _isOverAllocated;
        public bool IsOverAllocated
        {
            get => _isOverAllocated;
            set { _isOverAllocated = value; OnPropertyChanged(); }
        }

        private int? _totalFloat;
        public int? TotalFloat
        {
            get => IsEmptyContainer ? null : _totalFloat;
            set { _totalFloat = value; OnPropertyChanged(); }
        }
        public double? BurnRate => (Work.HasValue && DurationDays.HasValue && DurationDays.Value > 0)
        ? (Work.Value / DurationDays.Value)
        : null;

        public string BurnRateDisplay => !IsSummary && BurnRate.HasValue
        ? $"{BurnRate.Value:F1} h/d"
        : "--";

        // GAO-16-89G Best Practice 8: Flag if daily burn exceeds 6.5 hours per resource
        public bool IsOverallocated => BurnRate > 6.5;
         public ICommand OptimizeDurationCommand => new RelayCommand(OptimizeDuration, CanOptimizeDuration);
        public bool ShowBurnOptimizer => !IsSummary && Work.HasValue && Work.Value > 0;
        private bool CanOptimizeDuration() => Work.HasValue && Work.Value > 0;

    private void OptimizeDuration()
    {
        // GAO-16-89G: Target ~6.5 hours per day. Math.Ceiling ensures we don't exceed it.
        DurationDays = (int)Math.Ceiling(Work.Value / 6.5);
        
        // Notify UI of the updates (assuming your base properties don't already cascade these)
        OnPropertyChanged(nameof(DurationDays));
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(BurnRate));
        OnPropertyChanged(nameof(BurnRateDisplay));
        OnPropertyChanged(nameof(IsOverallocated));
            RollupHierarchy();

            // Note: If you have a Schedule Engine, you may also need to trigger an EndDate recalculation here
        }
        private double? _work;
        public double? Work
        {
            get => (Level == 0 || IsEmptyContainer) ? null : _work;
            set
            {
                if (_work == value) return;
                _work = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationDisplay));
                if (!IsEditingWork)
                {
                    RollupHierarchy();
                }
            }
        }
        private double? _actualWork;
        public double? ActualWork
        {
            get => IsEmptyContainer ? null : _actualWork;
            set { _actualWork = value; OnPropertyChanged(); RollupHierarchy(); }
        }
        private WorkItemStatus _status;
        public WorkItemStatus Status 
        { 
            get => _status; 
            set 
            { 
                if (_status == value) return;
                _status = value; 
                OnPropertyChanged(); 
                
                foreach (var child in Children)
                {
                    child.Status = value;
                }

                OnPropertyChanged(nameof(StatusSummary));
            } 
        }

        private string _assignee;
        public string Assignee 
        { 
            get => _assignee; 
            set { _assignee = value; OnPropertyChanged(); } 
        }

        private int _level;
        public int Level 
        { 
            get => _level; 
            set 
            { 
                _level = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsNumberApplicable));
                OnPropertyChanged(nameof(IsChildNumberApplicable));
                OnPropertyChanged(nameof(IsAddingProjectMode));
                OnPropertyChanged(nameof(IsChildMetricsApplicable));
            } 
        }

        public bool IsNumberApplicable => Level <= 2;
        public bool IsChildNumberApplicable => Level < 2;
        public bool IsChildMetricsApplicable => Level >= 2;
        public bool IsChildMetricsInapplicable => !IsChildMetricsApplicable;
        public bool IsSubProject => Level == 2;
        private bool _isExpanded;
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set { _isExpanded = value; OnPropertyChanged(); } 
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public SystemHierarchyItemViewModel Parent { get; set; }
        public bool IsLoading { get; set; }

        public decimal? BACHours => BAC / 195m;

        private decimal? _bac;
        public decimal? BAC
        {
            get
            {
                // FIX: Level 0 (System) never shows metrics.
                if (Level == 0 || IsEmptyContainer || !_bac.HasValue) return null;

                return IsHoursMode ? _bac / 195m : _bac;
            }
            set
            {
                decimal? newValue = (!IsLoading && IsHoursMode) ? value * 195m : value;
                if (_bac == newValue) return;
                _bac = newValue;
                OnPropertyChanged();
                if (!IsLoading) RollupHierarchy();
            }
        }

        private double? _bcws;
        public double? Bcws
        {
            // FIX: Level 0 (System) never shows metrics.
            get => (Level == 0 || IsEmptyContainer || !_bcws.HasValue) ? null : (IsHoursMode ? _bcws / 195.0 : _bcws);
            set { _bcws = (!IsLoading && IsHoursMode) ? value * 195.0 : value; OnPropertyChanged(); }
        }

        private double? _bcwp;
        public double? Bcwp
        {
            // FIX: Level 0 (System) never shows metrics.
            get => (Level == 0 || IsEmptyContainer || !_bcwp.HasValue) ? null : (IsHoursMode ? _bcwp / 195.0 : _bcwp);
            set { _bcwp = (!IsLoading && IsHoursMode) ? value * 195.0 : value; OnPropertyChanged(); }
        }

        private double? _acwp;
        public double? Acwp
        {
            // FIX: Level 0 (System) never shows metrics.
            get => (Level == 0 || IsEmptyContainer || !_acwp.HasValue) ? null : (IsHoursMode ? _acwp / 195.0 : _acwp);
            set { _acwp = (!IsLoading && IsHoursMode) ? value * 195.0 : value; OnPropertyChanged(); }
        }

        private double? _progress;
        public double? Progress
        {
            // FIX: Level 0 (System) never shows metrics.
            get => (Level == 0 || IsEmptyContainer) ? null : _progress;
            set { _progress = value; OnPropertyChanged(); if (!IsLoading) RollupHierarchy(); }
        }

        public bool IsSummary => Children != null && Children.Any();

        private bool _isRollingUp;

        public void RollupHierarchy()
        {
            if (_isRollingUp) return;
            _isRollingUp = true;

            try
            {
                if (Children != null && Children.Any())
                {
                    // 1. Calculate Min/Max from Children
                    var validStarts = Children.Where(c => c.StartDate.HasValue).Select(c => c.StartDate.Value).ToList();
                    var validEnds = Children.Where(c => c.EndDate.HasValue).Select(c => c.EndDate.Value).ToList();

                    // 2. Strict Rollup: Parent takes the exact bounds of children
                    // (We do not check if 'current' dates are wider anymore)
                    if (validStarts.Any())
                    {
                        var minStart = validStarts.Min();
                        if (_startDate != minStart)
                        {
                            _startDate = minStart;
                            OnPropertyChanged(nameof(StartDate));
                        }
                    }

                    if (validEnds.Any())
                    {
                        var maxEnd = validEnds.Max();
                        if (_endDate != maxEnd)
                        {
                            _endDate = maxEnd;
                            OnPropertyChanged(nameof(EndDate));
                        }
                    }

                    // 3. FIX: Calculate Parent Duration based on these new dates
                    if (_startDate.HasValue && _endDate.HasValue)
                    {
                        _durationDays = WorkBreakdownItem.GetBusinessDaysSpan(_startDate.Value, _endDate.Value);
                    }
                    else
                    {
                        _durationDays = 0;
                    }


                    var sumWork = Children.Sum(c => c.Work ?? 0);
                    if (_work != sumWork)
                    {
                        _work = sumWork;
                        OnPropertyChanged(nameof(Work));
                    }
                    var sumActual = Children.Sum(c => c.ActualWork ?? 0);
                    if (_actualWork != sumActual) { _actualWork = sumActual; OnPropertyChanged(nameof(ActualWork)); }
                    var sumBac = Children.Sum(c => c.BAC ?? 0);
                    if (_bac != sumBac) { _bac = sumBac; OnPropertyChanged(nameof(BAC)); }

                    var sumBcws = Children.Sum(c => c.Bcws ?? 0);
                    if (_bcws != sumBcws) { _bcws = sumBcws; OnPropertyChanged(nameof(Bcws)); }

                    var sumBcwp = Children.Sum(c => c.Bcwp ?? 0);
                    if (_bcwp != sumBcwp) { _bcwp = sumBcwp; OnPropertyChanged(nameof(Bcwp)); }

                    var sumAcwp = Children.Sum(c => c.Acwp ?? 0);
                    if (_acwp != sumAcwp) { _acwp = sumAcwp; OnPropertyChanged(nameof(Acwp)); }

                    if (sumBac > 0)
                    {
                        _progress = (double)(sumBcwp / (double)sumBac);
                        OnPropertyChanged(nameof(Progress));
                    }
                    else if (Children.Any())
                    {
                        _progress = Children.Average(c => c.Progress ?? 0);
                        OnPropertyChanged(nameof(Progress));
                    }
                }
                else
                {
                    if (ItemType == WorkItemType.Receipt)
                    {
                        if (_work != 0) { _work = 0; OnPropertyChanged(nameof(Work)); }
                    }

                    if (StartDate.HasValue)
                    {
                        var expectedEnd = WorkBreakdownItem.AddBusinessDays(StartDate.Value, DurationDays ?? 0);
                        if (_endDate != expectedEnd)
                        {
                            _endDate = expectedEnd;
                            OnPropertyChanged(nameof(EndDate));
                        }
                    }

                    if (IsInPlanningMode)
                    {
                        decimal expectedCost = (decimal)(Work ?? 0) * 195m;
                        if (_bac != expectedCost)
                        {
                            _bac = expectedCost;
                            OnPropertyChanged(nameof(BAC));
                        }
                    }
                }

                OnPropertyChanged(nameof(DurationDays));
                OnPropertyChanged(nameof(DurationDisplay));
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));

                Parent?.RollupHierarchy();
            }
            finally
            {
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                OnPropertyChanged(nameof(DurationDays));
                OnPropertyChanged(nameof(Work));
                OnPropertyChanged(nameof(BAC));
                OnPropertyChanged(nameof(Bcws));
                OnPropertyChanged(nameof(Bcwp));
                OnPropertyChanged(nameof(Acwp));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(TotalFloat));
                OnPropertyChanged(nameof(Predecessors));

                _isRollingUp = false;
            }
        }

        private bool _isEditingName;
        public bool IsEditingName { get => _isEditingName; set { _isEditingName = value; OnPropertyChanged(); } }

        private bool _isEditingStartDate;
        public bool IsEditingStartDate { get => _isEditingStartDate; set { _isEditingStartDate = value; OnPropertyChanged(); } }

        private bool _isEditingEndDate;
        public bool IsEditingEndDate { get => _isEditingEndDate; set { _isEditingEndDate = value; OnPropertyChanged(); } }

        private bool _isEditingDuration;
        public bool IsEditingDuration { get => _isEditingDuration; set { _isEditingDuration = value; OnPropertyChanged(); } }

        private bool _isEditingStatus;
        public bool IsEditingStatus { get => _isEditingStatus; set { _isEditingStatus = value; OnPropertyChanged(); } }

        private bool _isEditingPredecessors;
        public bool IsEditingPredecessors { get => _isEditingPredecessors; set { _isEditingPredecessors = value; OnPropertyChanged(); } }

        private bool _isEditingStartNoEarlierThan;
        public bool IsEditingStartNoEarlierThan { get => _isEditingStartNoEarlierThan; set { _isEditingStartNoEarlierThan = value; OnPropertyChanged(); } }

        private bool _isEditingWork;
        public bool IsEditingWork { get => _isEditingWork; set { _isEditingWork = value; OnPropertyChanged(); } }

        private bool _isAddingChild;
        public bool IsAddingChild 
        { 
            get => _isAddingChild; 
            set { _isAddingChild = value; OnPropertyChanged(); } 
        }

        private string _newChildName;
        public string NewChildName { get => _newChildName; set { _newChildName = value; OnPropertyChanged(); } }

        private string _newChildNumber;
        public string NewChildNumber { get => _newChildNumber; set { _newChildNumber = value; OnPropertyChanged(); } }
        
        private WorkItemType _newChildItemType;
        public WorkItemType NewChildItemType { get => _newChildItemType; set { _newChildItemType = value; OnPropertyChanged(); } }

        private ScheduleMode _newChildScheduleMode = ScheduleMode.Dynamic;
        public ScheduleMode NewChildScheduleMode { get => _newChildScheduleMode; set { _newChildScheduleMode = value; OnPropertyChanged(); } }

        public Array ScheduleModeOptions => Enum.GetValues(typeof(ScheduleMode));

        public bool IsAddingProjectMode => Level == 0;

        private DateTime? _newChildStartDate;
        public DateTime? NewChildStartDate { get => _newChildStartDate; set { _newChildStartDate = value; OnPropertyChanged(); } }
        
        private DateTime? _newChildEndDate;
        public DateTime? NewChildEndDate { get => _newChildEndDate; set { _newChildEndDate = value; OnPropertyChanged(); } }

        private int _newChildDurationDays;
        public int NewChildDurationDays { get => _newChildDurationDays; set { _newChildDurationDays = value; OnPropertyChanged(); } }

        private string? _newChildPredecessors;
        public string? NewChildPredecessors { get => _newChildPredecessors; set { _newChildPredecessors = value; OnPropertyChanged(); } }

        private DateTime? _newChildStartNoEarlierThan;
        public DateTime? NewChildStartNoEarlierThan { get => _newChildStartNoEarlierThan; set { _newChildStartNoEarlierThan = value; OnPropertyChanged(); } }
        
        private double? _newChildWork;
        public double? NewChildWork { get => _newChildWork; set { _newChildWork = value; OnPropertyChanged(); } }

        private decimal? _newChildBAC;
        public decimal? NewChildBAC { get => _newChildBAC; set { _newChildBAC = value; OnPropertyChanged(); } }

        public string DurationDisplay
        {
            get
            {
                // FIX 1: System Level (0) is a strict container and should never show duration.
                if (Level == 0) return "--";

                if (IsEmptyContainer) return "--";
                if (DurationDays.HasValue) return $"{DurationDays.Value}d";
                return "0d";
            }
        }

        public string StatusSummary => Status.ToString();

        public string CurrentNumberPrefix
        {
            get
            {
                if (Level == 0) return "";
                if (Level == 1)
                {
                    int spaceIndex = Name?.IndexOf(' ') ?? -1;
                    return spaceIndex >= 0 ? Name.Substring(0, spaceIndex) + "-" : Name + "-";
                }
                return "";
            }
        }

        public ObservableCollection<SystemHierarchyItemViewModel> Children { get; } = new ObservableCollection<SystemHierarchyItemViewModel>();

        public ICommand ToggleExpansionCommand { get; }
        
        public ICommand EditNameCommand { get; }
        public ICommand EditStartDateCommand { get; }
        public ICommand EditEndDateCommand { get; }
        public ICommand EditDurationCommand { get; }
        public ICommand EditStatusCommand { get; }
        public ICommand EditPredecessorsCommand { get; }
        public ICommand EditStartNoEarlierThanCommand { get; }
        public ICommand EditWorkCommand { get; }
        public ICommand ApplyTemplateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddChildCommand { get; }
        public ICommand ConfirmAddChildCommand { get; }
        public ICommand CancelAddChildCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AssignDeveloperCommand { get; }
        public ICommand EditDetailsCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }

        private string _originalName;
        private DateTime? _originalStartDate;
        private DateTime? _originalEndDate;
        private double? _originalWork;
        private WorkItemStatus _originalStatus;
        private WorkItemType _originalType;

        public SystemHierarchyItemViewModel(
            Action<SystemHierarchyItemViewModel> onSave, 
            Action<SystemHierarchyItemViewModel> onDelete, 
            Action<SystemHierarchyItemViewModel> onAddChild,
            Action<SystemHierarchyItemViewModel> onAssign,
            Action<SystemHierarchyItemViewModel> onEditDetails,
            Action<SystemHierarchyItemViewModel> onApplyTemplate,
            Action<SystemHierarchyItemViewModel> onCopy,
            Action<SystemHierarchyItemViewModel> onPaste)
        {
            _onSave = onSave;
            _onDelete = onDelete;
            _onAddChild = onAddChild;
            _onAssign = onAssign;
            _onEditDetails = onEditDetails;
            _onApplyTemplate = onApplyTemplate;
            _onCopy = onCopy;
            _onPaste = onPaste;

            Children.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsSummary));
                OnPropertyChanged(nameof(DurationDisplay));
                if (!IsLoading) RollupHierarchy();
            };

            ToggleExpansionCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

            EditNameCommand = new RelayCommand(() => { CancelAllEdits(); SnapshotValues(); IsEditingName = true; });
            EditStartDateCommand = new RelayCommand(() => 
            { 
                if (!IsSummary && ScheduleMode == ScheduleMode.Manual) 
                { 
                    CancelAllEdits(); 
                    SnapshotValues(); 
                    IsEditingStartDate = true; 
                } 
            });
            EditEndDateCommand = new RelayCommand(() => 
            { 
                if (!IsSummary && ScheduleMode == ScheduleMode.Manual) 
                { 
                    CancelAllEdits(); 
                    SnapshotValues(); 
                    IsEditingEndDate = true; 
                } 
            });
            EditDurationCommand = new RelayCommand(() => 
            { 
                if (!IsSummary) 
                { 
                    CancelAllEdits(); 
                    SnapshotValues(); 
                    IsEditingDuration = true; 
                } 
            });
            EditStatusCommand = new RelayCommand(() => { CancelAllEdits(); SnapshotValues(); IsEditingStatus = true; });
            EditPredecessorsCommand = new RelayCommand(() => { CancelAllEdits(); SnapshotValues(); IsEditingPredecessors = true; });
            EditStartNoEarlierThanCommand = new RelayCommand(() => { CancelAllEdits(); SnapshotValues(); IsEditingStartNoEarlierThan = true; });
            EditWorkCommand = new RelayCommand(() => 
            { 
                if (!IsSummary && ItemType != WorkItemType.Receipt) 
                { 
                    CancelAllEdits(); 
                    SnapshotValues(); 
                    IsEditingWork = true; 
                } 
            });

            SaveCommand = new RelayCommand(() =>
            {
                if (Level == 0)
                {
                    _name = $"{LocalNumber} {LocalName}".Trim();
                }
                else if (Level <= 2)
                {
                    string parentNumber = "";
                    if (Parent != null && !string.IsNullOrWhiteSpace(Parent.Name))
                    {
                        int spaceIndex = Parent.Name.IndexOf(' ');
                        parentNumber = spaceIndex >= 0 ? Parent.Name.Substring(0, spaceIndex) : Parent.Name;
                    }
                    if (string.IsNullOrWhiteSpace(parentNumber))
                        _name = $"{LocalNumber} {LocalName}".Trim();
                    else
                        _name = $"{parentNumber}-{LocalNumber} {LocalName}".Trim();
                }
                else
                {
                    _name = LocalName.Trim();
                }
                OnPropertyChanged(nameof(Name));

                CloseEditMode();
                RollupHierarchy();
                _onSave?.Invoke(this);
            });

            AddChildCommand = new RelayCommand(() =>
            {
                CloseEditMode();
                IsAddingChild = true;
                IsExpanded = true;
                NewChildItemType = WorkItemType.Leaf;
                NewChildStartDate = StartDate ?? DateTime.Today;
                NewChildDurationDays = 5;
                NewChildWork = 40;
                NewChildStartNoEarlierThan = null;
                NewChildPredecessors = "";
                NewChildScheduleMode = this.ScheduleMode;
            });

            ConfirmAddChildCommand = new RelayCommand(() => { _onAddChild?.Invoke(this); IsAddingChild = false; });
            CancelAddChildCommand = new RelayCommand(() => IsAddingChild = false);
            DeleteCommand = new RelayCommand(() => _onDelete?.Invoke(this));
            AssignDeveloperCommand = new RelayCommand(() => _onAssign?.Invoke(this));
            EditDetailsCommand = new RelayCommand(() => _onEditDetails?.Invoke(this));
            ApplyTemplateCommand = new RelayCommand(() => _onApplyTemplate?.Invoke(this));
            CopyCommand = new RelayCommand(() => _onCopy?.Invoke(this));
            PasteCommand = new RelayCommand(() => _onPaste?.Invoke(this));
        }

        private bool IsAnyEditing => IsEditingName || IsEditingStartDate || IsEditingEndDate || IsEditingDuration || IsEditingStatus || IsEditingPredecessors || IsEditingStartNoEarlierThan || IsEditingWork;
        
        private void SnapshotValues()
        {
            _originalName = Name;
            _originalStartDate = StartDate;
            _originalEndDate = EndDate;
            _originalWork = Work;
            _originalStatus = Status;
            _originalType = ItemType;
        }

        private void RevertValues()
        {
            if (!IsAnyEditing) return;
            if (Name != _originalName) Name = _originalName;
            if (StartDate != _originalStartDate) StartDate = _originalStartDate;
            if (EndDate != _originalEndDate) EndDate = _originalEndDate;
            if (Work != _originalWork) Work = _originalWork;
            if (Status != _originalStatus) Status = _originalStatus;
            if (ItemType != _originalType) ItemType = _originalType;
        }

        private void CloseEditMode()
        {
            IsEditingName = false;
            IsEditingStartDate = false;
            IsEditingEndDate = false;
            IsEditingDuration = false;
            IsEditingStatus = false;
            IsEditingPredecessors = false;
            IsEditingStartNoEarlierThan = false;
            IsEditingWork = false;
        }

        private void CancelAllEdits()
        {
            if (IsAnyEditing)
            {
                RevertValues();
                CloseEditMode();
            }
        }

        public void UpdateDuration()
        {
            if (IsSummary) return;

            if (ScheduleMode == ScheduleMode.Manual && StartDate.HasValue && EndDate.HasValue)
            {
                int computedDuration = WorkBreakdownItem.GetBusinessDaysSpan(StartDate.Value, EndDate.Value);
                if (_durationDays != computedDuration)
                {
                    _durationDays = computedDuration;
                    OnPropertyChanged(nameof(DurationDays));
                    RollupHierarchy();
                }
            }

            OnPropertyChanged(nameof(DurationDisplay));
        }
    }
}
