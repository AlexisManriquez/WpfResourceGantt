---
tags: [feature]
purpose: AI reference for the recursive in-memory WBS builder.
---

# Project Creation

**Files**: `ProjectManagement/Features/ProjectCreation/`

## Key Files
- `CreateWorkItemViewModel.cs` — The powerhouse recursive ViewModel
- `CreateProjectViewModel.cs` — Top-level "Add Project" wrapper
- `CreateSubProjectViewModel.cs` — Sub-project creation helper

## Key Capabilities
`CreateWorkItemViewModel` allows building infinite-depth WBS trees **in memory** before committing to the database.

| Feature | How |
|---------|-----|
| Recursive children | Each CreateWorkItemViewModel can contain child CreateWorkItemViewModels |
| Status cascading | Setting a System to "Active" propagates to all descendants |
| Name parsing | Splits "SYS-001 Project Alpha" into Number + Name components |
| Dynamic UI | Hides the "Number" field for lower-level tasks (Level 3+) |
| Pre-commit editing | All changes are in-memory until the user saves |

## Related Pages
- [[System Management]] — triggers project creation
- [[Data Hierarchy]] — tree structure being built
- [[DataService]] — `SanitizeChildrenIds()` applied on commit
