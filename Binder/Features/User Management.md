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

## Administrator Filtering
The `Administrator` role is **completely hidden** from the User Management UI:

| Location | Filter Applied |
|----------|---------------|
| User list (`UserManagementViewModel`) | `AllUsers.Where(u => u.Role != Role.Administrator)` |
| Role dropdown (`CreateUserDialogViewModel`) | `Enum.GetValues().Where(r => r != Role.Administrator)` |

This means:
- Admin users **never appear** in the user list
- Users **cannot assign** the Administrator role via the UI
- Admin accounts can only be created via direct database insert

## Related Pages
- [[User & Role]] — User model and role-based access control
- [[DataService]] — user CRUD methods
- [[Navigation & Ribbons]] — toolbar context
- [[Authentication & Deployment]] — login flow and Windows auto-login
