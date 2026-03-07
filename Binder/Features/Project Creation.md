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
- `ReconstructProjectDialogViewModel.cs` — (**New**) Automated project reconstruction from external files

## Key Capabilities
`CreateWorkItemViewModel` allows building infinite-depth WBS trees **in memory** before committing to the database.

| Feature | How |
|---------|-----|
| Recursive children | Each CreateWorkItemViewModel can contain child CreateWorkItemViewModels |
| Status cascading | Setting a System to "Active" propagates to all descendants |
| Name parsing | Splits "SYS-001 Project Alpha" into Number + Name components |
| Dynamic UI | Hides the "Number" field for lower-level tasks (Level 3+) |
| Pre-commit editing | All changes are in-memory until the user saves |

## Automated Reconstruction ("RECONST PROJECT")
A high-speed alternative to manual WBS building that recreates a full DoD hierarchy from SMTS source data.

| Feature | Description |
|---------|-------------|
| **End-to-End Pipeline** | Rebuilds System → Project → SubProject → Leaf Tasks in one pass. |
| **Smart Dialog UI** | Dark-themed modal with options for **Existing System** vs **New System** containers. |
| **Auto-Detection** | Pre-scanner (`ScanCsvForNumbers`) extracts System/Project IDs before the dialog opens. |
| **Excel (.xlsx) Support**| Background COM Interop (`ConvertExcelToCsv`) converts workbooks to temporary CSVs. |
| **Historical Metrics** | Preserves execution metrics and historical data during the reconstruction process. |

## Related Pages
- [[System Management]] — triggers project creation
- [[Data Hierarchy]] — tree structure being built
- [[DataService]] — `SanitizeChildrenIds()` applied on commit
