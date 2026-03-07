---
tags: [model]
purpose: AI reference for Resource Gantt-specific models.
---

# Resource & Gantt Models

**File**: `ProjectManagement/Models/ResourceGanttModels.cs`

These models are specific to the [[Resource Gantt]] view and are constructed by [[DataService]]`.GetResourceGanttDataAsync()`.

## ResourcePerson
Represents a user and their assigned tasks.

| Property | Type | Notes |
|----------|------|-------|
| `Name` | `string` | User display name |
| `Section` | `string` | Office symbol |
| `Role` | `string` | "Developer", "PM", "SectionChief" |
| `Tasks` | `ObservableCollection<ResourceTask>` | All assigned tasks |

## ResourceTask
A flattened task view for timeline rendering.

| Property | Type | Notes |
|----------|------|-------|
| `Name` | `string` | Task name |
| `StartDate` | `DateTime` | Planned start |
| `EndDate` | `DateTime` | Planned end |
| `Status` | `TaskStatus` | InWork, OnHold, Future |
| `ProjectName` | `string` | Parent system name |
| `ProjectOfficeSymbol` | `string` | Derived from SectionChief.Section |
| `ResourceOfficeSymbol` | `string` | User's section |
| `AssignmentRole` | `AssignmentRole` | Primary, Support, Reviewer |
| `IsVisibleInView` | `bool` | Filter toggle |

## Other Models
- **`GanttColumn.cs`** — Column definition for dynamic Gantt DataGrid columns
- **`AdminTask.cs`** — Quick task / to-do item (used in [[Developer Portal]], [[Quick Tasks]])
- **`ProgressHistoryItem.cs`** — S-curve time-series data (see [[ProgressBlock & Items]])

## Related Pages
- [[Resource Gantt]] — the view that uses these models
- [[DataService]] — `GetResourceGanttDataAsync()` constructs ResourcePerson/Task
- [[User & Role]] — source data for ResourcePerson
