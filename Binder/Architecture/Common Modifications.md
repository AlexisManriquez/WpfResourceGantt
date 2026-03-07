---
tags: [architecture, recipes]
purpose: Quick-reference recipes for common modifications. Saves AI models from re-discovering patterns.
---

# Common Modifications

## Adding a New Ribbon Button
1. Add `ICommand` property + handler in [[MainViewModel]]
2. Add `<Button>` to `ProjectManagement/UI/Views/TacticalRibbonView.xaml`
3. Bind `Command="{Binding Show*Command}"`

## Adding a New Feature View
1. Create `Features/NewFeature/NewFeatureViewModel.cs` (extend `ViewModelBase`)
2. Create `Features/NewFeature/NewFeatureView.xaml`
3. Add `Show*Command` in [[MainViewModel]] that sets `CurrentView = "NewFeature"`
4. Add `DataTemplate` in `ProjectManagementControl.xaml` mapping VM → View
5. Add button in `TacticalRibbonView.xaml`

## Adding a New Model Property
1. Add property to `WorkBreakdownItem` in `ProjectManagement/Models/ProjectData.cs`
2. If it needs to display in UI, add corresponding property to `WorkItem.cs`
3. Add mapping in the relevant ViewModel's `ConvertWorkBreakdownItemToWorkItem()` method
4. If persisted, create an EF migration: `dotnet ef migrations add AddPropertyName`

## Changing Progress Calculation
1. **Quick update (checkbox toggle)**: `DeveloperPortalViewModel.ToggleBlockItem()`
2. **Deep recalculation**: `DataService.ExecuteDbSaveAsync()` (Phase 3 in [[Data Lifecycle]])
3. **Rollup**: `WorkBreakdownItem.RecalculateRollup()` in `ProjectData.cs`
4. **ViewModel rollup**: `WorkItem.RecalculateRollup()` in `WorkItem.cs`

## Adding a New Column to the Gantt
1. Add `GanttColumn` in `GanttViewModel.InitializeColumns()`
2. Bind in `GanttView.xaml` DataGrid columns
3. Add to `GanttViewModel.AddColumn()` / `RemoveColumn()` if user-toggleable

## Modifying EVM Metrics
See [[EVM Calculation Rules]] for all formulas and their locations:
- **BAC**: `DataService.BaselineItemsRecursive()`
- **BCWS**: `WorkBreakdownItem.RecalculateRollup()` (leaf branch)
- **BCWP**: `WorkBreakdownItem.RecalculateRollup()` (leaf branch)
- **S-Curve**: `EVMViewModel.GenerateSCurve()`

## Adding a New Dialog
1. Create `DialogViewModel.cs` extending `ViewModelBase`
2. Create `DialogView.xaml`
3. Add `DataTemplate` in `MainWindow.xaml` for the modal overlay
4. Call `MainViewModel.ShowModalCustomDialog(vm, onConfirm, onCancel)`

## Related Pages
- [[Navigation & Ribbons]] — view registration details
- [[DataService]] — save pipeline
- [[MainViewModel]] — command patterns
