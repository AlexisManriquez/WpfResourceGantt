---
tags: [architecture]
purpose: AI-optimized reference for the complete data flow from creation through persistence to UI display.
---

# Data Lifecycle

This page documents how data flows through the application from creation to rendering. Understanding this flow is critical for debugging and for knowing **where** to make changes.

## Phase 1: Data Loading

**Trigger**: `MainViewModel.InitializeAsync()` → `DataService.LoadDataAsync()`

```
SQL Server (AppDbContext)
  ↓ EF Core query with Includes
  ↓ Include(ProgressBlocks).ThenInclude(Items)
  ↓ Include(ProgressHistory)
  ↓ Include(Assignments)
  ↓ OrderBy(Sequence)
  ↓
In-Memory Model (_projectData: ProjectData)
  ↓ SortChildrenRecursive()
  ↓ _evmService.RecalculateAll(systems)  ← SINGLE authoritative EVM rollup
  ↓   └─ For every leaf: BCWP = BAC × Progress
  ↓   └─ For every leaf: BCWS = BAC × businessDaysElapsed / totalBusinessDays
  ↓   └─ ACWP preserved (SMTS-owned, never recalculated)
  ↓   └─ Summary nodes: Sum(children.*) for all metrics
  ↓ context.SaveChangesAsync()  ← Updated BCWS/BCWP saved back to DB
  ↓
DataChanged event fires
  ↓
All subscribed ViewModels refresh
```

**Key Detail**: BCWS is **recalculated on every load** because it depends on `DateTime.Today`. The updated values are immediately persisted back to the database so they're always current. All views read these pre-computed values — no view runs its own EVM rollup.

## Phase 2: Data Editing (User Interaction)

### Scenario A: WBS Structure Changes (System Management)
```
User action (Add/Delete/Reorder in SystemManagementView)
  ↓
SystemManagementViewModel modifies in-memory _projectData
  ↓ ACWP is never written here — reserved for SMTS import only
DataService.SaveDataAsync()  ← debounced (300ms)
  ↓
DataChanged event → UI refresh
```

### Scenario B: Progress Update (Developer Portal / Gantt)
```
User checks ProgressItem.IsCompleted = true
  ↓
ProgressBlock recalculates completion %
  ↓
Parent WorkBreakdownItem.Progress updates
  ↓
DataService.SaveDataAsync()  ← debounced
  ↓
On next LoadDataAsync(), EvmCalculationService recalculates BCWP = BAC × Progress
  ↓
SPI/CPI/SV/CV update on all ViewModels
```

### Scenario C: Hours Import (SMTS CSV)
```
User imports CSV → CsvImportService processes
  ↓ Pass 1: Scan CSV rows
  ↓ Pass 2: For each row — update ActualWork, ACWP (ONLY authorized write point)
            Stamp LastAcwpImportDate + LastAcwpImportSource audit fields
  ↓ Pass 3: Recalculate ProgressHistory.ActualProgress (interpolated)
  ↓
DataService.SaveDataAsync()
  ↓
DataChanged event → UI refresh
  ↓
PM then clicks CLOSE WEEK to capture weekly snapshot
```

### Scenario D: Template Application
```
User selects template in ApplyTemplateDialog
  ↓
TemplateService.ApplyTemplateAsync()
  ↓ Converts TemplateGate → WorkBreakdownItem
  ↓ Converts TemplateProgressBlock → ProgressBlock  
  ↓ Converts TemplateProgressItem → ProgressItem
  ↓ Generates IDs: ParentID|Sequence format
  ↓
DataService.RegenerateWbsValues()  ← ensures WBS codes are set
DataService.SaveDataAsync()
  ↓
DataChanged event → UI refresh
```

### Scenario E: EVM Mode Toggle
```
User clicks "EVM: Dollars/Hours" button
  ↓
DataService.ToggleEvmModeAsync(bool toHours)
  ↓ Recursively scales every BAC and ACWP (×195 or ÷195)
  ↓ Sets IsEvmHoursBased global flag
  ↓
DataService.SaveDataAsync()
  ↓
DataChanged event → UI refresh
```

### Scenario F: Close Week (PM Weekly Snapshot)
```
PM clicks "CLOSE WEEK" ribbon button
  ↓ Prerequisite: SMTS CSV import for the week must be done first
MainViewModel.ExecuteCloseWeekAsync()
  ↓ Confirms week-ending Sunday with PM dialog
  ↓ Guard: Developers cannot execute (CanExecute = false)
DataService.TakeWeeklySnapshotsAsync(userId)
  ↓ Collects all SubProject (Level 2) items from AllSystems
  ↓ For each SubProject:
      Reads cumulative BAC, BCWS, BCWP, ACWP from leaf rollup
      Computes SPI, CPI, Progress at that moment
      If snapshot (SubProjectId, WeekEndingDate) doesn't exist → CREATE
      If exists and IsLocked = false → UPDATE (correction window)
      If exists and IsLocked = true  → SKIP (immutable)
  ↓ Writes EvmWeeklySnapshot rows to EvmWeeklySnapshots table
  ↓
EVMViewModel.Refresh() called if EVM view is active
```

## Phase 3: Data Persistence (Save Pipeline)

**Method**: `DataService.SaveDataAsync()` → `ExecuteDbSaveAsync()`

The save system uses a **debounced, graph-tracked** approach:

### Step 1: Debounce
```csharp
// 300ms delay — if another save request comes in, the timer resets
await Task.Delay(300, token);  // CancellationTokenSource
```

### Step 2: Collect All IDs from Memory
```
For each SystemItem:
  ├── Set system.Sequence = index
  └── For each WorkBreakdownItem (recursive):
      ├── Set item.Sequence = index
      ├── Collect WorkItem IDs
      ├── Collect ProgressBlock IDs (leaves only)
      ├── Collect ProgressItem IDs (leaves only)
      ├── Collect ProgressHistory IDs
      └── Collect Assignment IDs (leaves only)
```

### Step 3: Fetch All IDs from Database
```
SELECT Id FROM Systems, WorkItems, ProgressBlocks, ProgressItems, ProgressHistory, ResourceAssignments, Users
```

### Step 4: Delete Orphans
```
DB_IDs - Memory_IDs = items to DELETE from DB
```
**Deletion order** (respects FK constraints):
1. ProgressItems
2. ProgressBlocks
3. ProgressHistory
4. ResourceAssignments
5. WorkItems
6. Systems
7. Users

> [!NOTE]
> `EvmWeeklySnapshots` are **never** deleted by the standard orphan-cleanup. They are permanent audit records. To delete a snapshot, a PM must explicitly unlock and delete it (future feature).

### Step 5: Smart Upsert via TrackGraph
```csharp
context.ChangeTracker.TrackGraph(system, node => {
    node.Entry.State = existsInDb ? EntityState.Modified : EntityState.Added;
});
```
- Uses a `trackedIdsInThisSession` HashSet to prevent duplicate tracking
- Detaches entities with null IDs or already-tracked IDs

### Step 6: Commit + Notify
```csharp
await context.SaveChangesAsync();
DataChanged?.Invoke(this, EventArgs.Empty);
```

## Phase 4: EVM Rollup (via EvmCalculationService)

**When**: On every `LoadDataAsync()`, after SQL load but before the DataChanged event.

**Service**: `EvmCalculationService.RecalculateAll(systems)` — the **single authoritative engine**.

The rollup is **bottom-up recursive**:
```
Leaf nodes calculate:
  BCWP = BAC × Progress
  BCWS = BAC × (businessDaysElapsed / totalBusinessDays)
  ACWP = preserved as-is (SMTS-sourced, never recalculated)

Summary nodes aggregate:
  Work = Sum(children.Work)
  ActualWork = Sum(children.ActualWork)
  BAC = Sum(children.BAC)
  BCWS = Sum(children.Bcws)
  BCWP = Sum(children.Bcwp)
  ACWP = Sum(children.Acwp)   ← rolling up SMTS values only
  Dates = Min(children.StartDate), Max(children.EndDate)
  Progress = BCWP / BAC (or Average if BAC = 0)
```

See [[EVM Calculation Rules]] for detailed formulas.

## Phase 5: UI Rendering

ViewModels subscribe to `DataService.DataChanged`:
```
DataChanged fires
  ↓
GanttViewModel.OnDataChanged() → RefreshAndPreserveState()
  ↓ Rebuilds WorkItem tree from SystemItem data (pre-computed EVM values)
  ↓ CalculateOverallHealthRecursively() — traffic-light colors only
  ↓ Preserves expansion state via GetStateRecursive/SetStateRecursive
  ↓
EVMViewModel → Re-generates S-Curve from ProgressHistory
DashboardViewModel → Recalculates KPIs
ResourceGanttViewModel → Refreshes person/task grid
```

## App Shutdown

```
MainWindow.Closing event
  ↓
DataService.EnsureSavedAsync()
  ↓ Cancels debounce timer
  ↓ Waits for any active save
  ↓ Forces one final immediate save
```

## Key Source Files
| File | Role |
|------|------|
| `DataService.cs` | Load, save, rollup trigger, SMTS import, weekly snapshots |
| `EvmCalculationService.cs` | **Single EVM rollup engine** |
| `CsvImportService.cs` | **Only writer of ACWP** |
| `ProjectData.cs` | `WorkBreakdownItem` entity + business day helpers |
| `MainViewModel.cs` | Orchestration, `InitializeAsync()`, `CloseWeekCommand` |
| `WorkItem.cs` | ViewModel-level display wrapper |

## Related Pages
- [[DataService]] — detailed method reference
- [[EVM Calculation Rules]] — formula details and snapshot rules
- [[Data Hierarchy]] — tree structure
- [[CsvImportService]] — SMTS import pipeline
- [[App Overview]] — entry point context
