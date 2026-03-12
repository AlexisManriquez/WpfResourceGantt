---
tags: [model, evm]
purpose: AI reference for the recursive WBS node. The most important model in the application.
---

# WorkBreakdownItem

**File**: `ProjectManagement/Models/ProjectData.cs` (line ~179)

The core recursive entity representing **every** node in the WBS tree from Level 1 (Project) downward.

## Properties

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | Pipe-delimited path: `"SYS-xxx\|0\|1\|2"` |
| `WbsValue` | `string` | Display WBS code (e.g., "1.2.3") |
| `Name` | `string` | Item name |
| `Level` | `int` | Depth in tree (1=Project, 2=Sub-Project, 3=Gate, 4+=Task) |
| `Sequence` | `int` | Sort order among siblings |
| `Status` | `WorkItemStatus` | Active, Complete, OnHold, Future |
| `ItemType` | `WorkItemType` | Leaf, Receipt, System |
| `IsBaselined` | `bool` | Whether BAC has been frozen |
| **Schedule** | | |
| `StartDate` | `DateTime?` | Planned start (calculated by forward pass) |
| `EndDate` | `DateTime?` | Planned finish (calculated by forward pass) |
| `ActualFinishDate` | `DateTime?` | Actual completion date |
| `DurationDays` | `int` | Business-day duration (input field) |
| `Predecessors` | `string?` | Comma-separated predecessor IDs (FS default) |
| `TotalFloat` | `int` | Schedule slack in business days (0 = critical) |
| `IsCritical` | `bool` | `true` when on the critical path |
| **Work** | | |
| `Work` | `double?` | Planned hours |
| `ActualWork` | `double?` | Actual hours spent (from SMTS import) |
| **EVM** | | |
| `BAC` | `decimal?` | Budget at Completion |
| `Bcws` | `double?` | Planned Value (recalculated by EvmCalculationService on load) |
| `Bcwp` | `double?` | Earned Value |
| `Acwp` | `double?` | **Actual Cost — SMTS import only. Never write this elsewhere.** |
| `Progress` | `double` | 0.0 to 1.0 (drives BCWP) |
| **SMTS Audit Trail** | | |
| `LastAcwpImportDate` | `DateTime?` | UTC timestamp of last SMTS CSV import for this task |
| `LastAcwpImportSource` | `string` | Filename of the CSV that last updated ACWP |
| **Assignment** | | |
| `AssignedDeveloperId` | `string` | FK to User.Id (leaf only) |
| **Collections** | | |
| `Children` | `List<WorkBreakdownItem>` | Recursive children |
| `ProgressBlocks` | `List<ProgressBlock>` | Checklists (leaf only) |
| `ProgressHistory` | `List<ProgressHistoryItem>` | S-curve data points |
| `Assignments` | `List<ResourceAssignment>` | Multi-developer assignments |

## Constants
```csharp
public const decimal HourlyRate = 195m;  // Used for BAC = Work × Rate
```

## ACWP Write-Lock Rule

> [!IMPORTANT]
> `Acwp` is written **exclusively** by `CsvImportService`. Any code that sets `workItem.Acwp = ...` outside of `CsvImportService.cs` is a compliance violation. The audit trail fields (`LastAcwpImportDate`, `LastAcwpImportSource`) are also set only in `CsvImportService`.

## Schedule Fields (GAO Compliance)

> [!NOTE]
> `StartDate` and `EndDate` are **calculated outputs**, not manual inputs. They are driven by `DurationDays` and `Predecessors` via the [[ScheduleCalculationService]] forward pass. Only `DurationDays` and `Predecessors` should be user-editable for leaf tasks.

### How Dates Flow
```
User enters DurationDays + Predecessors
  ↓
ScheduleCalculationService.ForwardPass()
  ↓ StartDate = Max(predecessor EndDates) + 1 business day
  ↓ EndDate = AddBusinessDays(StartDate, DurationDays)
  ↓
ScheduleCalculationService.BackwardPass()
  ↓ TotalFloat = LateFinish - EarlyFinish (in business days)
  ↓ IsCritical = (TotalFloat == 0)
```

## EvmCalculationService (Replaces RecalculateRollup)

The old `RecalculateRollup()` method on this class previously ran standalone. It is now orchestrated by `EvmCalculationService.RecalculateAll()` which is called once per load by `DataService`. The rollup logic is identical but the execution is centralized.

### Summary Node (has children):
1. Recursively processes children first
2. Dates: `StartDate = Min(children)`, `EndDate = Max(children)` — but **never shrinks** below current parent dates
3. Rolls up: Work, ActualWork, BAC, BCWS, BCWP = `Sum(children.*)`
4. **ACWP**: `Sum(children.Acwp)` — rolled up from SMTS-sourced leaf values only
5. Progress = `BCWP / BAC` (weighted), or `Average(children.Progress)` if `BAC = 0`

### Leaf Node (no children):
1. Calculates BCWS using business days (see [[EVM Calculation Rules]])
2. Calculates `BCWP = BAC × Progress`
3. ACWP is **preserved** (never recalculated — SMTS-sourced)

## WorkItem (ViewModel Wrapper)
**File**: `ProjectManagement/WorkItem.cs`

A `ViewModelBase` subclass wrapping `WorkBreakdownItem` with `INotifyPropertyChanged` for WPF binding. Contains:
- All the same properties with change notification
- `IsSystem`, `IsSummary`, `IsLeaf`, `IsSubProject` — computed helpers
- `Predecessors` — raw ID string for schedule logic
- `ScheduleHealth` — 3-state computed property (`Good` / `Warning` / `Bad`) based on `TotalFloat` threshold of 5 days. Drives Gantt bar and arrow colors.
- `LateStart` — computed `DateTime?` = `AddBusinessDays(StartDate, TotalFloat)`. The latest safe start date. `null` for summaries and critical tasks.
- EVM color properties: `SpiColor`, `CpiColor`
- `ProgressBlocks` ObservableCollection
- `IsExpanded` — tree expansion state

## Simulation Mode Behavior
When being processed by the **Temporal Sandbox** (`SimulationViewModel.cs`):
- **Ad-hoc Extension**: Duration is automatically extended if `Progress < 1.0` and `StatusDate > EndDate`.
- **Ad-hoc Shrink**: Duration is automatically shrunk to `ActualFinishDate` if `Progress >= 1.0`.
- **Original Duration Tracking**: The simulation engine maintains a separate dictionary to track baseline durations before these ad-hoc sandbox modifications.

## Related Pages
- [[Data Hierarchy]] — tree structure and leaf vs summary rules
- [[EVM Calculation Rules]] — formula details and ACWP write-lock
- [[ScheduleCalculationService]] — CPM engine that drives dates and float
- [[ProgressBlock & Items]] — what's inside leaf nodes
- [[DataService]] — persistence and ID generation
- [[CsvImportService]] — the only service that writes Acwp
