using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

using ThreadingTask = System.Threading.Tasks.Task;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{

    public class GateProgressViewModel : ViewModelBase
    {
        private WorkItem _subProject;
        private readonly MainViewModel _mainViewModel;
        private readonly DataService _dataService;
        private readonly Action _backNavigation;
        private object _lastClickedItem;
        private bool _isDraggingSelection = false;
        private bool _dragTargetState = false;
        private List<object> _multiClipboard = new List<object>();

        public WorkItem SubProject => _subProject;
        public string SubProjectName => _subProject?.Name ?? "Unknown Sub-Project";
        public string SubProjectWbs => $"{_subProject?.WbsValue ?? "N/A"} ({RootRows.Count} Gates)";
        public string BreadcrumbText => $"{_subProject?.ProjectManagerName} / {_subProject?.DisplayName}";
        public ObservableCollection<GateRowViewModel> RootRows { get; } = new ObservableCollection<GateRowViewModel>();
        public bool HasData => RootRows.Any();



        // Commands
        public ICommand StartDragSelectionCommand { get; }
        public ICommand HoverSelectionCommand { get; }
        public ICommand EndDragSelectionCommand { get; }
        public ICommand CopySelectedCommand { get; }
        public ICommand PasteSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }

        public ICommand BackCommand { get; }
        public ICommand AddBlockCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand DeleteRowCommand { get; }

        public ICommand AddBlockToGateCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public GateProgressViewModel(MainViewModel mainViewModel, WorkItem subProject, Action backNavigation, DataService dataService)
        {
            _mainViewModel = mainViewModel;
            _subProject = subProject;
            _backNavigation = backNavigation;
            _dataService = dataService;
            BackCommand = new RelayCommand(GoBack);

            AddBlockCommand = new RelayCommand<GateRowViewModel>(AddBlock);
            AddItemCommand = new RelayCommand<GateRowViewModel>(AddItem);
            DeleteRowCommand = new RelayCommand<GateRowViewModel>(DeleteRow);
            StartDragSelectionCommand = new RelayCommand<GateRowViewModel>(ExecuteStartDrag);
            HoverSelectionCommand = new RelayCommand<GateRowViewModel>(item =>
            {
                if (_isDraggingSelection && item != null) item.IsSelected = _dragTargetState;
            });
            EndDragSelectionCommand = new RelayCommand(() => _isDraggingSelection = false);

            CopySelectedCommand = new RelayCommand(ExecuteCopy);
            PasteSelectedCommand = new RelayCommand(ExecutePaste);
            DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
            MoveRowCommand = new RelayCommand<ReorderParams>(ExecuteMoveRow);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            LoadData();
        }

        public class ReorderParams
        {
            public List<GateRowViewModel> Sources { get; set; }
            public GateRowViewModel Target { get; set; }
            public bool After { get; set; }
        }

        public ICommand MoveRowCommand { get; }

        private async void ExecuteMoveRow(ReorderParams p)
        {
            if (p?.Sources == null || !p.Sources.Any() || p.Target == null) return;

            // Filter out the target if it's in sources
            var validSources = p.Sources.Where(s => s != p.Target && s.Parent == p.Target.Parent).ToList();
            if (!validSources.Any()) return;

            var firstSource = validSources.First();
            var parentCollection = firstSource.Parent != null ? firstSource.Parent.Children : RootRows;

            // 1. Determine target index AFTER removing sources
            // This prevents index shifting issues
            int targetIndex = parentCollection.IndexOf(p.Target);
            if (targetIndex == -1) return;

            // Remove all sources from UI
            foreach (var s in validSources) parentCollection.Remove(s);

            // Re-find target index
            targetIndex = parentCollection.IndexOf(p.Target);
            if (p.After) targetIndex++;

            // Insert sources at new position
            for (int i = 0; i < validSources.Count; i++)
            {
                parentCollection.Insert(targetIndex + i, validSources[i]);
            }

            // 2. SYNC UNDERLYING MODEL
            // We use the new state of parentCollection to re-assign sequences
            if (firstSource.Parent?.Model is WorkItem parentWi)
            {
                if (firstSource.RowType == GateRowType.Block)
                {
                    parentWi.ProgressBlocks.Clear();
                    for (int i = 0; i < parentCollection.Count; i++)
                    {
                        var block = (ProgressBlock)parentCollection[i].Model;
                        block.Sequence = i;
                        parentWi.ProgressBlocks.Add(block);
                    }
                }
                else // Gate or Task
                {
                    parentWi.Children.Clear();
                    for (int i = 0; i < parentCollection.Count; i++)
                    {
                        var task = (WorkItem)parentCollection[i].Model;
                        task.Sequence = i;
                        parentWi.Children.Add(task);
                    }
                }
            }
            else if (firstSource.Parent?.Model is ProgressBlock parentPb)
            {
                parentPb.Items.Clear();
                for (int i = 0; i < parentCollection.Count; i++)
                {
                    var item = (ProgressItem)parentCollection[i].Model;
                    item.Sequence = i;
                    parentPb.Items.Add(item);
                }
            }
            else if (firstSource.Parent == null) // Root Gates
            {
                _subProject.Children.Clear();
                for (int i = 0; i < parentCollection.Count; i++)
                {
                    var gate = (WorkItem)parentCollection[i].Model;
                    gate.Sequence = i;
                    _subProject.Children.Add(gate);
                }
            }

            await SaveAndRecalculate(GetWorkItemContext(firstSource));
        }

        public List<GateRowViewModel> GetSelectedRows()
        {
            var results = new List<GateRowViewModel>();
            CollectSelectedRows(RootRows, results);
            return results;
        }

        private void CollectSelectedRows(IEnumerable<GateRowViewModel> items, List<GateRowViewModel> results)
        {
            foreach (var item in items)
            {
                if (item.IsSelected) results.Add(item);
                CollectSelectedRows(item.Children, results);
            }
        }

        private void GoBack()
        {
            // Execute the smart navigation we passed in
            _backNavigation?.Invoke();
        }

        private void ToggleRow(GateRowViewModel row)
        {
            if (row != null)
                row.IsExpanded = !row.IsExpanded;
        }

        public void LoadData()
        {
            RootRows.Clear();
            if (_subProject == null) return;

            // 1. Sort Gates (Level 0 in this view) by Sequence
            var sortedGates = _subProject.Children
                .OrderBy(c => c.Sequence)
                .ToList();

            foreach (var gateItem in sortedGates)
            {
                var gateRow = MapToRowRecursive(gateItem, GateRowType.Gate, 0);
                RootRows.Add(gateRow);
            }

            OnPropertyChanged(nameof(HasData));
        }
        private GateRowViewModel MapToRowRecursive(object model, GateRowType type, int level, GateRowViewModel parent = null)
        {
            // 1. Create the instance first
            var row = new GateRowViewModel
            {
                RowType = type,
                Level = level,
                Model = model,
                Parent = parent,
                IsExpanded = false
            };

            // 2. Now assign the action, because 'row' is fully declared
            row.OnChanged = () =>
            {
                row.RefreshRollups();
                // Use the discard symbol _ to indicate fire-and-forget for the async task
                _ = SaveAndRecalculate(GetWorkItemContext(row));
            };

            if (model is WorkItem wi)
            {
                row.Id = wi.Id;
                row.Name = wi.Name;
                row.StartDate = wi.StartDate;
                row.EndDate = wi.EndDate;

                // Branch: Gates either have Child Tasks OR Progress Blocks
                // 2. Sort Child Tasks by Sequence
                if (wi.Children != null && wi.Children.Any())
                {
                    var sortedChildren = wi.Children.OrderBy(c => c.Sequence);
                    foreach (var child in sortedChildren)
                        row.Children.Add(MapToRowRecursive(child, GateRowType.Task, level + 1, row));
                }

                // 3. Sort Progress Blocks by Sequence
                if (wi.ProgressBlocks != null && wi.ProgressBlocks.Any())
                {
                    var sortedBlocks = wi.ProgressBlocks.OrderBy(b => b.Sequence);
                    foreach (var block in sortedBlocks)
                        row.Children.Add(MapToRowRecursive(block, GateRowType.Block, level + 1, row));
                }
            }
            else if (model is ProgressBlock pb)
            {
                row.Id = pb.Id;
                row.Name = pb.Name;
                row.IsCompleted = pb.IsCompleted;

                if (pb.Items != null && pb.Items.Any())
                {
                    var sortedItems = pb.Items.OrderBy(i => i.Sequence);
                    foreach (var item in sortedItems)
                        row.Children.Add(MapToRowRecursive(item, GateRowType.ChecklistItem, level + 1, row));
                }
            }
            else if (model is ProgressItem pi)
            {
                row.Id = pi.Id;
                row.Name = pi.Name; // FIX: Ensure the item description/name is displayed
                row.IsCompleted = pi.IsCompleted;
            }

            return row;
        }
        public class WbsComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                var xParts = x.Split('.');
                var yParts = y.Split('.');

                for (int i = 0; i < Math.Min(xParts.Length, yParts.Length); i++)
                {
                    if (int.TryParse(xParts[i], out int xInt) && int.TryParse(yParts[i], out int yInt))
                    {
                        if (xInt != yInt) return xInt.CompareTo(yInt);
                    }
                    else
                    {
                        int partCompare = string.Compare(xParts[i], yParts[i], StringComparison.Ordinal);
                        if (partCompare != 0) return partCompare;
                    }
                }
                return xParts.Length.CompareTo(yParts.Length);
            }
        }
        // Helper to find the nearest WorkItem parent for database saving
        private WorkItem GetParentWorkItem(object model)
        {
            if (model is WorkItem wi) return wi;
            // Search up the tree logic would go here if needed, 
            // but row.OnChanged already captures the specific WorkItem context.
            return null;
        }
        public void RefreshFromDataService()
        {
            // 1. Get the absolutely fresh data from the DataService
            var freshEntity = _dataService.GetWorkBreakdownItemById(_subProject.Id);

            if (freshEntity != null)
            {
                // 2. RE-WRAP: We create a brand new WorkItem wrapper and manually 
                // map only the safe properties to avoid the previous Spi/Cpi errors.
                var updatedWrapper = MapEntityToViewModelSafe(freshEntity);

                // 3. Update our internal reference
                _subProject = updatedWrapper;

                // 4. Rebuild the UI (Gates -> Tasks -> Blocks)
                // This clears the 'Gates' collection and starts over with the new data
                LoadData();
            }
        }

        private WorkItem MapEntityToViewModelSafe(WorkBreakdownItem entity)
        {
            var vm = new WorkItem
            {
                Id = entity.Id,
                Sequence = entity.Sequence,
                Name = entity.Name,
                WbsValue = entity.WbsValue,
                Level = entity.Level,
                Progress = entity.Progress,
                StartDate = entity.StartDate ?? DateTime.Today,
                EndDate = entity.EndDate ?? DateTime.Today,
                ItemType = (WorkItemType)entity.Level // Heuristic for your app structure
            };

            // Map Progress Blocks (This is the critical part for your import)
            vm.ProgressBlocks = new ObservableCollection<ProgressBlock>();
            if (entity.ProgressBlocks != null)
            {
                foreach (var block in entity.ProgressBlocks.OrderBy(b => b.Sequence))
                {
                    // CRITICAL: We need to ensure the Items collection is preserved
                    if (block.Items == null) block.Items = new List<ProgressItem>();
                    vm.ProgressBlocks.Add(block);
                }
            }

            // Map Children Recursively
            vm.Children = new ObservableCollection<WorkItem>();
            if (entity.Children != null)
            {
                foreach (var child in entity.Children.OrderBy(c => c.Sequence))
                {
                    vm.Children.Add(MapEntityToViewModelSafe(child));
                }
            }

            return vm;
        }
        // FIX: Recursive helper that carries the path DOWN, so we don't need .Parent
        private List<(WorkItem Task, string Path)> GetLeavesWithPaths(WorkItem current, string currentPath)
        {
            var results = new List<(WorkItem, string)>();

            // Build the path string
            string newPath = string.IsNullOrEmpty(currentPath) ? current.Name : $"{currentPath} > {current.Name}";

            if (current.IsLeaf)
            {
                results.Add((current, newPath));
            }
            else
            {
                foreach (var child in current.Children)
                {
                    // Pass the accumulator down
                    results.AddRange(GetLeavesWithPaths(child, newPath));
                }
            }
            return results;
        }
        // Helper to create Block VMs and wire up their commands

        // --- NEW COMMAND HANDLERS ---

        private async void AddBlock(GateRowViewModel parentRow)
        {
            if (parentRow == null || !(parentRow.Model is WorkItem parentWorkItem)) return;

            var newBlock = new ProgressBlock
            {
                Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Name = string.Empty,
                // Ensure Sequence is unique and at the end
                Sequence = parentWorkItem.ProgressBlocks?.Count ?? 0,
                Items = new List<ProgressItem>()
            };

            if (parentWorkItem.ProgressBlocks == null)
                parentWorkItem.ProgressBlocks = new ObservableCollection<ProgressBlock>();

            parentWorkItem.ProgressBlocks.Add(newBlock);

            // Create UI Row and Add to Children
            var blockRow = MapToRowRecursive(newBlock, GateRowType.Block, parentRow.Level + 1, parentRow);
            parentRow.Children.Add(blockRow);
            blockRow.IsSelected = true;
            parentRow.IsExpanded = true;

            await SaveAndRecalculate(parentWorkItem);
        }
        private async void AddItem(GateRowViewModel blockRow)
        {
            if (blockRow.Model is ProgressBlock block)
            {
                var newItem = new ProgressItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = string.Empty,
                    IsCompleted = false,
                    Sequence = block.Items?.Count ?? 0
                };

                // Update Model
                if (block.Items == null) block.Items = new List<ProgressItem>();
                block.Items.Add(newItem);

                // Update UI
                var itemRow = MapToRowRecursive(newItem, GateRowType.ChecklistItem, blockRow.Level + 1, blockRow);
                blockRow.Children.Add(itemRow);
                itemRow.IsSelected = true;
                blockRow.IsExpanded = true;

                blockRow.RefreshRollups();

                // Save (Find nearest WorkItem parent)
                await SaveAndRecalculate(GetWorkItemContext(blockRow));
            }
        }

        private WorkItem GetWorkItemContext(GateRowViewModel row)
        {
            var current = row;
            while (current != null)
            {
                if (current.Model is WorkItem wi) return wi;
                current = current.Parent;
            }
            return _subProject; // Fallback
        }

        private async void DeleteRow(GateRowViewModel row)
        {
            var parentRow = row.Parent;
            if (parentRow == null) return;

            // 1. Remove from Data Model
            if (row.RowType == GateRowType.Block && parentRow.Model is WorkItem parentWorkItem)
            {
                parentWorkItem.ProgressBlocks.Remove((ProgressBlock)row.Model);
                // Trigger save on the WorkItem that owned the block
                await SaveAndRecalculate(parentWorkItem);
            }
            else if (row.RowType == GateRowType.ChecklistItem && parentRow.Model is ProgressBlock parentBlock)
            {
                parentBlock.Items.Remove((ProgressItem)row.Model);
                // For items, we still need to sync the WorkItem that contains this block
                await SaveAndRecalculate(GetWorkItemContext(parentRow));
            }

            // 2. Remove from UI
            parentRow.Children.Remove(row);

            // 3. Rollup
            parentRow.RefreshRollups();
        }
        private void ExecuteStartDrag(GateRowViewModel item)
        {
            if (item == null) return;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _lastClickedItem is GateRowViewModel lastVm)
            {
                PerformShiftSelection(item, lastVm);
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                item.IsSelected = !item.IsSelected;
                _lastClickedItem = item;
            }
            else
            {
                ClearAllSelection();
                item.IsSelected = true;
                _lastClickedItem = item;
            }

            _isDraggingSelection = true;
            _dragTargetState = item.IsSelected;
        }

        private void PerformShiftSelection(GateRowViewModel currentItem, GateRowViewModel lastItem)
        {
            var flatList = new List<GateRowViewModel>();
            GetFlattenedVisibleItems(RootRows, flatList);

            int startIdx = flatList.IndexOf(lastItem);
            int endIdx = flatList.IndexOf(currentItem);

            if (startIdx == -1 || endIdx == -1) return;

            int min = Math.Min(startIdx, endIdx);
            int max = Math.Max(startIdx, endIdx);

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) ClearAllSelection();

            for (int i = min; i <= max; i++) flatList[i].IsSelected = true;
        }

        private void GetFlattenedVisibleItems(IEnumerable<GateRowViewModel> items, List<GateRowViewModel> flatList)
        {
            foreach (var item in items)
            {
                flatList.Add(item);
                if (item.IsExpanded && item.Children.Any())
                    GetFlattenedVisibleItems(item.Children, flatList);
            }
        }
        private void ExecuteCopy()
        {
            var selected = GetSelectedRows(RootRows);
            if (!selected.Any()) return;

            _multiClipboard.Clear();
            foreach (var row in selected)
            {
                // Store the underlying data model
                _multiClipboard.Add(row.Model);
            }
        }

        private async void ExecutePaste()
        {
            if (!_multiClipboard.Any()) return;
            var targetRow = GetSelectedRows(RootRows).LastOrDefault();
            if (targetRow == null) return;

            foreach (var clipboardObj in _multiClipboard)
            {
                // Scenario A: Paste ProgressItem into a Block
                if (clipboardObj is ProgressItem sourceItem && targetRow.RowType == GateRowType.Block)
                {
                    var parentBlock = (ProgressBlock)targetRow.Model;
                    var newItem = new ProgressItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = sourceItem.Name + " (Copy)",
                        Sequence = parentBlock.Items.Count
                    };
                    parentBlock.Items.Add(newItem);
                    targetRow.Children.Add(MapToRowRecursive(newItem, GateRowType.ChecklistItem, targetRow.Level + 1, targetRow));
                }
                // Scenario B: Paste ProgressBlock into a WorkItem (Gate/Task)
                else if (clipboardObj is ProgressBlock sourceBlock && targetRow.Model is WorkItem targetWi)
                {
                    var newBlock = new ProgressBlock
                    {
                        Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                        Name = sourceBlock.Name + " (Copy)",
                        Sequence = targetWi.ProgressBlocks.Count,
                        Items = sourceBlock.Items?.Select(i => new ProgressItem { Id = Guid.NewGuid().ToString(), Name = i.Name }).ToList()
                    };
                    targetWi.ProgressBlocks.Add(newBlock);
                    targetRow.Children.Add(MapToRowRecursive(newBlock, GateRowType.Block, targetRow.Level + 1, targetRow));
                }
            }

            targetRow.IsExpanded = true;
            await SaveAndRecalculate(GetWorkItemContext(targetRow));
        }

        private List<GateRowViewModel> GetSelectedRows(IEnumerable<GateRowViewModel> items)
        {
            var selected = new List<GateRowViewModel>();
            foreach (var item in items)
            {
                if (item.IsSelected) selected.Add(item);
                selected.AddRange(GetSelectedRows(item.Children));
            }
            return selected;
        }

        private async void ExecuteDeleteSelected()
        {
            var selected = GetSelectedRows(RootRows).ToList();
            if (!selected.Any()) return;

            var result = MessageBox.Show($"Delete {selected.Count} items?", "Confirm", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            foreach (var row in selected)
            {
                DeleteRow(row); // Uses the consolidated DeleteRow we created in clean-up
            }
        }
        public void CancelDragSelection()
        {
            _isDraggingSelection = false;
        }

        private void ClearAllSelection()
        {
            Action<IEnumerable<GateRowViewModel>> clear = null;
            clear = (items) =>
            {
                foreach (var i in items) { i.IsSelected = false; clear(i.Children); }
            };
            clear(RootRows);
        }
        public WorkItem GetSelectedGateOrTask()
        {
            // Find the first selected row in the UI
            var selectedRow = GetSelectedRows().FirstOrDefault();

            if (selectedRow != null)
            {
                var current = selectedRow;

                // If they selected a Block or Checklist Item, walk up the tree to find the parent Gate/Task
                while (current != null && current.RowType != GateRowType.Gate && current.RowType != GateRowType.Task)
                {
                    current = current.Parent;
                }

                return current?.Model as WorkItem;
            }

            return null;
        }
        private async ThreadingTask SaveAndRecalculate(WorkItem task)
        {
            if (task == null) return;

            // 1. Locate the Master Entity
            var masterItem = _dataService.GetWorkBreakdownItemById(task.Id);
            if (masterItem == null) return;

            // 2. SYNC COLLECTIONS (Only for Additions/Deletions)
            if (masterItem.ProgressBlocks.Count != task.ProgressBlocks.Count)
            {
                masterItem.ProgressBlocks.Clear();
                foreach (var b in task.ProgressBlocks) masterItem.ProgressBlocks.Add(b);
            }

            // 3. CALCULATION
            if (masterItem.ProgressBlocks != null && masterItem.ProgressBlocks.Any())
            {
                int totalItems = 0;
                int completedItems = 0;

                foreach (var b in masterItem.ProgressBlocks)
                {
                    if (b.Items == null || !b.Items.Any())
                    {
                        totalItems += 1;
                        if (b.IsCompleted) completedItems += 1;
                    }
                    else
                    {
                        totalItems += b.Items.Count;
                        completedItems += b.Items.Count(i => i.IsCompleted);
                    }
                }

                double calculatedProgress = totalItems > 0 ? (double)completedItems / totalItems : 0;

                // Sync BOTH the Master and the UI Wrapper
                masterItem.Progress = calculatedProgress;
                task.Progress = calculatedProgress; // <--- THIS UPDATES THE TASK ROW TEXT

                // MILESTONE AUTOMATION: When a milestone's checklist reaches 100%,
                // automatically stamp ActualFinishDate. If it drops below, clear it.
                if (masterItem.IsMilestone)
                {
                    if (calculatedProgress >= 1.0)
                    {
                        if (!masterItem.ActualFinishDate.HasValue)
                            masterItem.ActualFinishDate = DateTime.Today;
                    }
                    else
                    {
                        masterItem.ActualFinishDate = null;
                    }
                }
            }

            // 4. UI ROLLUP (Updates the Gates)
            _subProject.RecalculateRollup();

            // 5. GLOBAL ROLLUP (Updates the System)
            string systemId = masterItem.Id.Split('|')[0];
            var rootSystem = _dataService.GetSystemById(systemId);
            if (rootSystem != null) rootSystem.RecalculateRollup();

            // Mark the entire affected system subtree dirty so only these entities
            // are written to the DB — not every entity across every system.
            _dataService.MarkSystemDirty(systemId);

            // --- SNAPSHOT LOGIC (For Dashboard S-Curves) ---
            var today = DateTime.Today;

            // 1. Snapshot for the Leaf Task
            UpdateProgressHistory(masterItem, today);

            // 2. Snapshot for the Sub-Project (Gate)
            var subProjectMaster = _dataService.GetWorkBreakdownItemById(_subProject.Id);
            if (subProjectMaster != null)
            {
                UpdateProgressHistory(subProjectMaster, today);
            }

            // 6. REFRESH UI LABELS
            // Wrap in Dispatcher to ensure the UI updates the text immediately
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshGridUi(RootRows);
            });

            await _dataService.SaveDataAsync();
        }

        private void UpdateProgressHistory(WorkBreakdownItem item, DateTime date)
        {
            if (item == null) return;
            if (item.ProgressHistory == null) item.ProgressHistory = new List<ProgressHistoryItem>();

            // Calculate Expected Progress as a decimal (0.0 to 1.0)
            double expected = (item.BAC ?? 0) > 0 ? (double)(item.Bcws ?? 0) / (double)item.BAC.Value : 0;
            var entry = item.ProgressHistory.FirstOrDefault(h => h.Date.Date == date.Date);

            if (entry != null)
            {
                entry.ActualProgress = item.Progress;
                entry.ExpectedProgress = expected;
            }
            else
            {
                item.ProgressHistory.Add(new ProgressHistoryItem
                {
                    Date = date,
                    ActualProgress = item.Progress,
                    ExpectedProgress = expected
                });
            }
        }

        private void RefreshGridUi(IEnumerable<GateRowViewModel> rows)
        {
            foreach (var row in rows)
            {
                row.RefreshRollups(); // Updates the Progress, StatusText, and Colors
                if (row.Children.Any()) RefreshGridUi(row.Children);
            }
        }
        private List<WorkItem> GetAllLeafTasks(WorkItem parent)
        {
            var result = new List<WorkItem>();
            if (parent.IsLeaf)
            {
                result.Add(parent);
            }
            else
            {
                foreach (var child in parent.Children)
                {
                    result.AddRange(GetAllLeafTasks(child));
                }
            }
            return result;
        }
        private void ExpandAll()
        {
            SetExpandedState(RootRows, true);
        }

        private void CollapseAll()
        {
            SetExpandedState(RootRows, false);
        }

        private void SetExpandedState(IEnumerable<GateRowViewModel> rows, bool isExpanded)
        {
            foreach (var row in rows)
            {
                if (row.Children != null && row.Children.Any())
                {
                    row.IsExpanded = isExpanded;
                }
                SetExpandedState(row.Children, isExpanded);
            }
        }
    }

    // --- NESTED VIEW MODELS FOR THE TREE GRID ---

    public enum GateRowType { Gate, Task, Block, ChecklistItem }

    public class GateRowViewModel : ViewModelBase
    {
        // --- Identification & Hierarchy ---
        private GateRowType _rowType;
        public GateRowType RowType
        {
            get => _rowType;
            set
            {
                if (_rowType != value)
                {
                    _rowType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDateVisible));
                }
            }
        }
        public bool IsDateVisible => RowType == GateRowType.Gate;

        public int Level { get; set; }
        public string Id { get; set; }
        public object Model { get; set; } // Points to WorkItem, ProgressBlock, or ProgressItem
        public GateRowViewModel Parent { get; set; }
        public ObservableCollection<GateRowViewModel> Children { get; } = new ObservableCollection<GateRowViewModel>();

        // --- Interaction State ---
        private bool _isExpanded = false;
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

        // --- Data Properties ---
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                UpdateModelName(value);
                OnPropertyChanged();
                OnChanged?.Invoke();
            }
        }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value;

                // Ensure the underlying model is updated before OnChanged fires
                if (Model is ProgressItem pi)
                {
                    pi.IsCompleted = value;
                    pi.IsDirty = true;
                }
                else if (Model is ProgressBlock pb)
                {
                    pb.IsCompleted = value;
                    pb.IsDirty = true;
                }

                OnPropertyChanged();
                RefreshRollups();
                // Triggers the SaveAndRecalculate method updated above
                OnChanged?.Invoke();
            }
        }

        // --- UI Helpers ---
        public double Progress => CalculateProgress();
        public string StatusText => GetStatusText();
        public string StatusColor => Progress >= 1.0 ? "#065F46" : (Progress > 0 ? "#1E40AF" : "#9CA3AF");
        public string StatusBackground => Progress >= 1.0 ? "#D1FAE5" : (Progress > 0 ? "#DBEAFE" : "#F3F4F6");

        public bool ShowCheckbox => RowType == GateRowType.ChecklistItem || (RowType == GateRowType.Block && (Children == null || Children.Count == 0));
        public bool ShowProgressText => RowType == GateRowType.Gate || RowType == GateRowType.Task || (RowType == GateRowType.Block && Children != null && Children.Count > 0);

        public Action OnChanged { get; set; }

        // --- Logic Methods ---
        private void UpdateModelName(string newName)
        {
            if (Model is WorkItem wi) wi.Name = newName;
            else if (Model is ProgressBlock pb) { pb.Name = newName; pb.IsDirty = true; }
            else if (Model is ProgressItem pi)  { pi.Name = newName; pi.IsDirty = true; }
        }

        private double CalculateProgress()
        {
            if (Model is WorkItem wi) return wi.Progress;
            if (Model is ProgressBlock pb)
            {
                if (pb.Items == null || !pb.Items.Any()) return pb.IsCompleted ? 1.0 : 0.0;
                return (double)pb.Items.Count(i => i.IsCompleted) / pb.Items.Count;
            }
            if (Model is ProgressItem pi) return pi.IsCompleted ? 1.0 : 0.0;
            return 0;
        }

        private string GetStatusText()
        {
            double p = Progress;
            if (p >= 1.0) return "Completed";
            if (p > 0) return "In Progress";
            return "Not Started";
        }

        public void RefreshRollups()
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(ShowCheckbox));
            OnPropertyChanged(nameof(ShowProgressText));
            Parent?.RefreshRollups();
        }
    }
}
