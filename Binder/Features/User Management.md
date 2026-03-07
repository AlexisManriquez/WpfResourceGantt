---
tags: [feature]
purpose: AI reference for the User Management CRUD view.
---

# User Management

**Files**: `ProjectManagement/Features/UserManagement/UserManagementViewModel.cs`, `UserManagementView.xaml`, `CreateUserDialog.xaml`, `CreateUserDialogViewModel.cs`

## Orchestration
- **Trigger**: `TacticalRibbonView` → USERS button
- **Command**: `MainViewModel.ShowUsersCommand`
- **Handler**: `MainViewModel.ShowUserManagementView()` → `CurrentView = "Users"`

## Key Capabilities
- List all users with role and section info
- Create new user via modal dialog
- Edit user details
- Delete user (cascades assignment cleanup via `DataService.RemoveUserAssignmentsRecursive()`)

## Related Pages
- [[User & Role]] — User model
- [[DataService]] — user CRUD methods
- [[Navigation & Ribbons]] — toolbar context
