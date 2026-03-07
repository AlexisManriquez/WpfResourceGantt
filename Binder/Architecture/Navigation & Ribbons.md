---
tags: [architecture, ui]
purpose: AI-optimized reference for the dual-ribbon navigation system and view switching.
---

# Navigation & Ribbons

The application uses a **two-tier command structure** separating global navigation from view-specific actions.

## Tier 1: Primary Navigation Ribbon
**File**: `ProjectManagement/UI/Views/TacticalRibbonView.xaml`

| Group | Buttons | Command Target |
|-------|---------|----------------|
| **Navigation** | DASHBOARD, PROJECTS, RESOURCE GANTT, ANALYTICS, EVM | `MainViewModel.Show*Command` |
| **Administration** | USERS, SYSTEMS | `MainViewModel.ShowUsersCommand`, `ShowSystemsCommand` |
| **Actions** | ASSIGN TASK, CLOSE WEEK | `OpenAddTaskCommand`, `CloseWeekCommand` |
| **Features** | Ribbon collapse/expand via chevron | Local toggle |

### CLOSE WEEK Button
- Located in the **ACTIONS** group (Column 4 of ribbon grid)
- Bound to `MainViewModel.CloseWeekCommand`
- **`CanExecute`**: Returns `false` for `Role.Developer` — grayed out automatically
- **Purpose**: Freezes weekly EVM snapshot for all SubProjects after SMTS import
- **Tooltip**: Warns PM to complete SMTS import before closing the week
- **Icon**: Uses `Icon_Import` resource

**View switching pattern**:
1. Button click fires `ICommand` on [[MainViewModel]]
2. Command handler sets `CurrentView = "ViewName"` (string)
3. `DataTemplate` in `ProjectManagementControl.xaml` resolves the correct View/ViewModel

## Tier 2: Contextual Action Toolbar
**File**: `ProjectManagement/ProjectManagementControl.xaml`

This toolbar dynamically shows/hides button groups based on `CurrentView`:

| Context (`CurrentView`) | Visible Groups | Key Commands |
|------------------------|----------------|--------------|
| `"Systems"` | Editing Group | Add System, Add Item, Apply Template, Gate Progress, Delete |
| `"Projects"` | Display Group + Data Tools | Expand All, Collapse All, Zoom, Import/Export |
| `"Users"` | Users Group | Add User, Edit User, Delete User |
| *All views* | Data Tools Group | MPP Import/Export, Import Hours, Export Excel, Refresh |

**Visibility binding**: Uses `StringEqualityToVisibilityConverter` to compare `CurrentView` string against expected values.

## View Registration Map

| `CurrentView` String | ViewModel Class | View XAML |
|---------------------|-----------------|-----------| 
| `"Dashboard"` | `DashboardViewModel` | `DashboardView.xaml` |
| `"Projects"` | `GanttViewModel` | `GanttView.xaml` |
| `"Resource Gantt"` | `ResourceGanttViewModel` | `ResourceGanttView.xaml` |
| `"Analytics"` | `AnalyticsViewModel` | `AnalyticsView.xaml` |
| `"EVM"` | `EVMViewModel` | `EVMView.xaml` |
| `"Systems"` | `SystemManagementViewModel` | `SystemManagementView.xaml` |
| `"Users"` | `UserManagementViewModel` | `UserManagementView.xaml` |
| `"DeveloperPortal"` | `DeveloperPortalViewModel` | `DeveloperPortalView.xaml` |
| `"QuickTasks"` | `QuickTasksViewModel` | `QuickTasksView.xaml` |

## Dialog System
- **Modal overlay** managed by `MainViewModel.CurrentDialogViewModel`
- Rendered in `MainWindow.xaml` via a semi-transparent overlay + `ContentControl`
- Show via `MainViewModel.ShowModalCustomDialog(dialogVM, onConfirm, onCancel)`
- Used by: [[Apply Template Flow]], [[System Management]], [[User Management]]

## Adding a New View (Recipe)
1. Create `Features/NewFeature/NewFeatureViewModel.cs` and `NewFeatureView.xaml`
2. Add `Show*Command` and handler in [[MainViewModel]]
3. Add `DataTemplate` in `ProjectManagementControl.xaml` mapping ViewModel → View
4. Add button in `TacticalRibbonView.xaml`
5. Optionally add contextual toolbar group in `ProjectManagementControl.xaml`

## Adding a New Ribbon Action (Recipe)
1. Add `public ICommand MyActionCommand { get; }` to `MainViewModel`
2. Wire in constructor: `MyActionCommand = new RelayCommand(async () => await ExecuteMyActionAsync(), canExecutePredicate)`
3. Add `<Button>` in `TacticalRibbonView.xaml` ACTIONS `StackPanel` with `Command="{Binding DataContext.MyActionCommand, RelativeSource={RelativeSource AncestorType=Window}}"`
4. Use an existing `Icon_*` key from `Icons.xaml` for the `Tag` property

## Related Pages
- [[MainViewModel]] — command handlers and orchestration
- [[Common Modifications]] — recipes for common changes
- [[EVM Calculation Rules]] — how Close Week integrates with EVM
