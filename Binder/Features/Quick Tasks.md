---
tags: [feature]
purpose: AI reference for the Quick Tasks flat list view.
---

# Quick Tasks

**Files**: `ProjectManagement/Features/QuickTasks/QuickTasksViewModel.cs`, `QuickTasksView.xaml`

## Orchestration
- **Trigger**: `TacticalRibbonView` → QUICK TASKS button
- **Command**: `MainViewModel.ShowQuickTasksCommand`
- **Handler**: `MainViewModel.ShowQuickTasksView()` → `CurrentView = "QuickTasks"`

## Key Capabilities
- Flat list of `AdminTask` items for rapid entry and tracking
- CRUD via `DataService.CreateAdminTaskAsync()`, `UpdateAdminTaskAsync()`, `DeleteAdminTaskAsync()`
- AdminTasks also appear in [[Resource Gantt]] as "Administrative" entries

## Related Pages
- [[DataService]] — AdminTask CRUD
- [[Developer Portal]] — also shows QuickTasks
- [[Resource & Gantt Models]] — AdminTask model
