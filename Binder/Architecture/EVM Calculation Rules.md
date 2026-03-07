---
tags: [architecture, evm]
purpose: AI-optimized reference for all EVM calculation rules. Critical for any EVM or progress-related change.
---

# EVM Calculation Rules

This application implements DoD-standard Earned Value Management. This page documents **every formula** and **where it is computed** in the codebase.

## Core EVM Terms

| Term | Full Name | Definition | Source of Truth |
|------|-----------|------------|----------------|
| **BAC** | Budget at Completion | Total planned cost for a task | `WorkBreakdownItem.BAC` |
| **BCWS** | Budgeted Cost of Work Scheduled | How much *should* be done by now (Planned Value) | Calculated at load time |
| **BCWP** | Budgeted Cost of Work Performed | How much *is* done (Earned Value) | `BAC × Progress` |
| **ACWP** | Actual Cost of Work Performed | What was actually spent | **SMTS CSV import only** — never entered manually |
| **SPI** | Schedule Performance Index | `BCWP / BCWS` (>1 = ahead) | Derived |
| **CPI** | Cost Performance Index | `BCWP / ACWP` (>1 = under budget) | Derived |
| **SV** | Schedule Variance | `BCWP - BCWS` | Derived |
| **CV** | Cost Variance | `BCWP - ACWP` | Derived |
| **EAC** | Estimate at Completion | `BAC / CPI` | Derived |
| **VAC** | Variance at Completion | `BAC - EAC` | Derived |
| **TCPI** | To-Complete Performance Index | `(BAC - BCWP) / (BAC - ACWP)` | Derived |

## BAC Calculation (Baseline)

**Trigger**: User clicks "Set Baseline" in [[MainViewModel]]

**Method**: `DataService.BaselineSystemAsync()` → `BaselineItemsRecursive()`

**Rules**:
1. Only **leaf nodes** get BAC set directly: `BAC = Work × HourlyRate`
2. `HourlyRate` is a constant: `195.0m` (defined in `WorkBreakdownItem.HourlyRate`)
3. Summary nodes get BAC from rollup: `BAC = Sum(children.BAC)`
4. Sets `IsBaselined = true` on every node
5. A **rebaseline** overwrites existing BAC with current Work hours

```csharp
// DataService.cs — BaselineItemsRecursive()
decimal multiplier = IsEvmHoursBased ? 1.0m : WorkBreakdownItem.HourlyRate;
item.BAC = (decimal)(item.Work ?? 0) * multiplier;  // Leaf
```

## BCWS Calculation (Planned Value)

**Computed**: By `EvmCalculationService.RecalculateAll()` on every data load.

**Leaf Node Rule** — time-proportional using **business days**:
```
if today < StartDate → BCWS = 0
if today > EndDate   → BCWS = BAC
otherwise            → BCWS = BAC × (businessDaysElapsed / totalBusinessDays)
```

**Summary Node Rule**: `BCWS = Sum(children.Bcws)`

**Business Day Calculation**: `GetBusinessDaysSpan()` counts weekdays (Mon-Fri) inclusive of both start and end dates, matching MS Project's duration calculation.

## BCWP Calculation (Earned Value)

**Leaf Node**: `BCWP = BAC × Progress`
- `Progress` is driven by `ProgressItem.IsCompleted` checklist completion
- See [[ProgressBlock & Items]]

**Summary Node**: `BCWP = Sum(children.Bcwp)`

## ACWP (Actual Cost) — SMTS Import Only

> [!IMPORTANT]
> ACWP is **write-locked** to `CsvImportService`. It must **never** be manually entered or computed by any other service or ViewModel. This is a hard DoD compliance rule.

- Imported exclusively via [[CsvImportService]] (SMTS CSV)
- Formula: `ACWP += hoursCharged × user.HourlyRate` (per CSV row)
- Stored on each leaf `WorkBreakdownItem.Acwp`
- Summary rollup: `ACWP = Sum(children.Acwp)` — done by `EvmCalculationService`
- Audit trail: `LastAcwpImportDate` and `LastAcwpImportSource` are stamped on every leaf after import

**What NOT to do**:
```csharp
// ❌ WRONG — never set Acwp outside CsvImportService
workItem.Acwp = someCalculatedValue;

// ✅ CORRECT — only CsvImportService writes this
task.Acwp = (task.Acwp ?? 0) + (hoursToAdd * (double)rate);
task.LastAcwpImportDate = DateTime.Now;
task.LastAcwpImportSource = Path.GetFileName(filePath);
```

## Single Rollup Engine: EvmCalculationService

> [!IMPORTANT]
> As of the Phase 1 refactor, **all EVM rollup is centralized** in `EvmCalculationService.cs`. The old parallel `GanttViewModel.CalculateAllRollups()` and `WorkBreakdownItem.RecalculateRollup()` patterns are replaced by this single service.

**File**: `ProjectManagement/Services/EvmCalculationService.cs`

**Called by**: `DataService.LoadDataAsync()` → `_evmService.RecalculateAll(systems)`

**Runs once** at load time. All views (Gantt, EVM, System Management) read the pre-computed, persisted values from the database — they do **not** recalculate.

### Rollup Pass (Leaf → Root)
```
Leaf node:
  BCWP = BAC × Progress
  BCWS = BAC × (businessDaysElapsed / totalBusinessDays)
  ACWP = preserved (SMTS-sourced, never recalculated)

Summary node:
  Work, ActualWork, BAC, BCWS, BCWP, ACWP = Sum(children.*)
  Dates = Min(children.StartDate), Max(children.EndDate)
  Progress = BCWP / BAC  (or Average if BAC = 0)
```

## Progress Rollup

**Leaf**: Progress = `CompletedChecklistItems / TotalChecklistItems` (0.0 to 1.0)

**Summary** (BAC-weighted):
```
if totalBAC > 0:
    Progress = BCWP / BAC
else:
    Progress = Average(children.Progress)   ← fallback for un-baselined items
```

## SPI/CPI Calculation

Indices are computed from the stored EVM values:
```csharp
SPI = BCWP / BCWS   (1.0 if BCWS = 0)
CPI = BCWP / ACWP   (1.0 if ACWP = 0)
```

Color rules:
```csharp
SpiColor => SPI < 0.9 ? Red : (< 1.0 ? Yellow : Green)
CpiColor => CPI < 1.0 ? Red : Green
```

## S-Curve and Weekly Snapshots

The EVM S-Curve is **powered by weekly snapshot data**, not live recalculation.

### Weekly Snapshot Flow
1. PM completes SMTS CSV import for the week
2. PM clicks **"CLOSE WEEK"** in the ribbon (ACTIONS group)
3. `MainViewModel.ExecuteCloseWeekAsync()` calls `DataService.TakeWeeklySnapshotsAsync()`
4. One `EvmWeeklySnapshot` row is written per SubProject (Level 2)
5. `WeekEndingDate` = prior Sunday (DoD standard week closing)
6. Snapshot is **unlocked** by default — PM can re-run to correct before locking

### EvmWeeklySnapshot Fields (All Cumulative)
| Field | Formula | Notes |
|-------|---------|-------|
| `BAC` | Sum of leaf BAC under SubProject | Decimal, precision 18,2 |
| `BCWS` | Sum of leaf BCWS at week end | Double |
| `BCWP` | Sum of leaf BCWP at week end | Double |
| `ACWP` | Sum of leaf ACWP (from SMTS) | Double — never recalculated |
| `SPI` | `BCWP / BCWS` | Stored at snapshot time |
| `CPI` | `BCWP / ACWP` | Stored at snapshot time |
| `Progress` | `BCWP / BAC` | 0.0 to 1.0 |
| `IsLocked` | PM review gate | Locked rows cannot be overwritten |

### Business Rules: Snapshot Overwrite Protection
```
If snapshot exists for (SubProjectId, WeekEndingDate):
    If IsLocked = true  → SKIP (immutable, PM reviewed)
    If IsLocked = false → OVERWRITE (correction window)
If no snapshot exists   → CREATE new row
```

## Health Color Rules

| Metric | Green | Yellow | Red |
|--------|-------|--------|-----|
| SPI | ≥ 1.0 | 0.9 – 0.99 | < 0.9 |
| CPI | ≥ 1.0 | — | < 1.0 |
| TCPI | ≤ 1.0 | 1.0 – 1.1 | > 1.1 |

## Variance Percent Formulas (DoD Standard)
- **SV%** = `SV / BCWS` (not `SV / BAC`)
- **CV%** = `CV / BCWP` (not `CV / BAC`)

## Key Source Files
| File | What it does for EVM |
|------|---------------------|
| `ProjectManagement/Services/EvmCalculationService.cs` | **Single authoritative rollup engine** — BCWS/BCWP/BAC leaf+rollup |
| `ProjectManagement/Models/ProjectData.cs` | `WorkBreakdownItem` entity + `GetBusinessDaysSpan()` |
| `ProjectManagement/Models/EvmWeeklySnapshot.cs` | Weekly frozen snapshot entity |
| `ProjectManagement/DataService.cs` | `BaselineSystemAsync()`, `TakeWeeklySnapshotsAsync()`, `GetSnapshotsForSubProjectAsync()` |
| `ProjectManagement/CsvImportService.cs` | **Only writer of ACWP** |
| `ProjectManagement/Features/EVM/EVMViewModel.cs` | S-Curve, KPI cards, display modes |
| `ProjectManagement/MainViewModel.cs` | `CloseWeekCommand` + `ExecuteCloseWeekAsync()` |

## Related Pages
- [[Data Hierarchy]] — leaf vs summary distinction
- [[ProgressBlock & Items]] — how Progress is calculated
- [[DataService]] — baseline and save logic
- [[EVM]] — the feature view that displays these metrics
- [[Data Lifecycle]] — when rollup happens in the data flow
- [[CsvImportService]] — SMTS import pipeline
