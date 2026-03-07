---
tags: [feature]
purpose: AI reference for the Developer Portal role-specific view.
---

# Developer Portal

**Files**: `ProjectManagement/Features/DeveloperPortal/DeveloperPortalViewModel.cs`, `DeveloperPortalView.xaml`

## Orchestration
- **Trigger**: Automatic on login if `User.Role == Developer`
- **Logic**: `MainViewModel.InitializeAsync()` checks role
- **Handler**: `IsDeveloperPortalVisible = true`, `CurrentView = "DeveloperPortal"`

## Key Capabilities
- `AssignmentGroups`: Tasks grouped by status/priority, filtered to show only tasks assigned to the current developer
- `QuickTasks`: List of `AdminTask` items
- Checklist interaction: `ToggleBlockItem()` updates ProgressItem.IsCompleted → triggers progress recalculation
- Direct progress updates affect [[EVM Calculation Rules]] via [[ProgressBlock & Items]]

## Data Source
- `DataService.FilterSystemsForDeveloper()` creates a filtered copy of the hierarchy
- Only leaf tasks with matching `AssignedDeveloperId` are included

## Related Pages
- [[User & Role]] — role-based routing
- [[ProgressBlock & Items]] — checklist interaction
- [[Quick Tasks]] — shared AdminTask functionality
- [[MainViewModel]] — login/initialization flow
