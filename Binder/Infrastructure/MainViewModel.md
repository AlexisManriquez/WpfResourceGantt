---
tags: [infrastructure]
purpose: AI reference for the central orchestrator ViewModel.
---

# MainViewModel

**File**: `ProjectManagement/MainViewModel.cs` (~1079 lines, 40 methods)

The central orchestrator managing navigation, global state, and the dialog system.

## Core Responsibilities
1. **View switching** via `CurrentView` string property
2. **Dialog hosting** via `CurrentDialogViewModel` + modal overlay
3. **Service ownership**: Creates `DataService` and `TemplateService`
4. **User state**: Holds `CurrentUser`, controls Developer vs Manager mode
5. **Data refresh orchestration**: `RefreshFromDatabase()` → `RefreshData()`

## Key Properties
| Property | Type | Purpose |
|----------|------|---------|
| `CurrentView` | `string` | Drives `DataTemplate` selection in `ProjectManagementControl.xaml` |
| `CurrentViewModel` | `ViewModelBase` | The active view's ViewModel |
| `CurrentDialogViewModel` | `ViewModelBase` | Active dialog (null = no dialog) |
| `CurrentUser` | `User` | Logged-in user |
| `IsSystemsVisible` | `bool` | Contextual toolbar visibility |
| `IsProjectsVisible` | `bool` | Contextual toolbar visibility |
| `GanttContext` | `ResourceGanttViewModel` | Persistent context for Resource Gantt |

## Navigation Commands
| Command | Handler | Sets CurrentView to |
|---------|---------|---------------------|
| `ShowDashboardCommand` | `ShowDashboardView()` | `"Dashboard"` |
| `ShowProjectManagementCommand` | `ShowProjectGanttView()` | `"Projects"` |
| `ShowGanttCommand` | — | `"Resource Gantt"` |
| `ShowAnalyticsCommand` | — | `"Analytics"` |
| `ShowEVMCommand` | `ShowEVMView()` | `"EVM"` |
| `ShowSystemsCommand` | `ShowSystemManagementView()` | `"Systems"` |
| `ShowUsersCommand` | `ShowUserManagementView()` | `"Users"` |
| `ShowQuickTasksCommand` | `ShowQuickTasksView()` | `"QuickTasks"` |

## Data Tab Commands
| Command | Handler | Purpose |
|---------|---------|---------|
| `ImportProjectCommand` | `ExecuteImportProject()` | MPP XML import |
| `ExportProjectCommand` | `ExecuteExportProject()` | MPP export |
| `ImportHoursCommand` | `ImportHours()` | CSV import |
| `SetBaselineCommand` | `SetBaseline()` | Freeze BAC |
| `RefreshDataCommand` | `RefreshData()` | Full reload from DB |

## View Tab Commands
| Command | Handler | Purpose |
|---------|---------|---------|
| Zoom In/Out/Reset | `ZoomIn()`, `ZoomOut()`, `ResetZoom()` | Gantt zoom |
| Expand/Collapse All | `ExpandAll()`, `CollapseAll()` | Tree state |
| Time Range | `SetTimeRange()` | Chart/Gantt date filtering |

## Dialog System
```csharp
ShowModalCustomDialog(dialogVM, onConfirm, onCancel)
```
- Sets `CurrentDialogViewModel` → triggers overlay visibility in `MainWindow.xaml`
- `onConfirm` callback executes when user confirms
- `onCancel` hides the overlay

## Initialization Flow
```
MainViewModel() → constructor creates services, commands
CreateAsync() → calls InitializeAsync()
InitializeAsync() → LoadDataAsync(), set user, route to correct view
```

## Related Pages
- [[Navigation & Ribbons]] — how views map to commands
- [[DataService]] — the service this ViewModel orchestrates
- [[App Overview]] — entry point context
- [[Common Modifications]] — recipes for adding commands
