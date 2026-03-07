---
tags: [model, evm]
purpose: AI reference for the weekly EVM snapshot entity. Powers the S-Curve and provides reproducible reporting data.
---

# EvmWeeklySnapshot

**File**: `ProjectManagement/Models/EvmWeeklySnapshot.cs`

A frozen, point-in-time record of EVM metrics for a **SubProject (Level 2)** captured at the end of a reporting week (Sunday). Stored in the `EvmWeeklySnapshots` SQL table.

## Purpose

> Provides the time-series data needed for the EVM S-Curve while giving PMs an immutable, auditable record for customer reports. Once locked, a snapshot cannot be overwritten — fulfilling DoD EVMS data integrity requirements.

## Properties

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | GUID |
| `SubProjectId` | `string` | FK → `WorkBreakdownItem.Id` at Level 2 |
| `WeekEndingDate` | `DateTime` | Always a **Sunday** (DoD standard week close) |
| **Cumulative EVM Values** | | All values are cumulative (not period-only) |
| `BAC` | `decimal(18,2)` | Sum of leaf BAC under this SubProject |
| `BCWS` | `double` | Cumulative Planned Value as of week end |
| `BCWP` | `double` | Cumulative Earned Value as of week end |
| `ACWP` | `double` | Cumulative Actual Cost from SMTS (never recalculated) |
| **Derived Indices** | | Stored to avoid re-computation on report load |
| `SPI` | `double` | `BCWP / BCWS` — computed at snapshot time |
| `CPI` | `double` | `BCWP / ACWP` — computed at snapshot time |
| `Progress` | `double` | `BCWP / BAC` — 0.0 to 1.0 |
| **Audit** | | |
| `CreatedAt` | `DateTime` | UTC timestamp of snapshot creation/update |
| `CreatedByUserId` | `string` | User ID of PM who triggered Close Week |
| `IsLocked` | `bool` | When `true`, snapshot is immutable (PM-reviewed) |

## Database Constraints
- **Primary Key**: `Id`
- **Unique Index**: `(SubProjectId, WeekEndingDate)` — one snapshot per SubProject per week
- **Filter**: `WHERE SubProjectId IS NOT NULL`

## Business Rules

### Week Ending Date
```csharp
// DataService.GetWeekEndingDate() — always returns prior Sunday
int daysToSunday = (int)date.DayOfWeek;  // Sunday = 0
return date.AddDays(-daysToSunday).Date;
```

### Overwrite Protection
```
Snapshot exists + IsLocked = true  → SKIP (immutable, PM has reviewed)
Snapshot exists + IsLocked = false → UPDATE (correction window before PM locks)
No snapshot for this week          → CREATE new row
```

### Lock Workflow (Future)
Currently snapshots are created as `IsLocked = false`. A future PM review screen will allow:
1. PM reviews snapshot values
2. PM clicks "Lock" → `IsLocked = true`
3. Locked snapshots feed the permanent audit trail

## How Snapshots Are Created

```
PM clicks CLOSE WEEK ribbon button
  ↓
MainViewModel.ExecuteCloseWeekAsync()
  ↓ Confirmation dialog (shows WeekEndingDate)
  ↓
DataService.TakeWeeklySnapshotsAsync(userId)
  ↓ Collects all Level 2 WorkBreakdownItems via CollectByLevel()
  ↓ For each SubProject:
      bac  = GetLeafSum(sp, w => w.BAC)
      bcws = GetLeafSum(sp, w => w.Bcws)
      bcwp = GetLeafSum(sp, w => w.Bcwp)
      acwp = GetLeafSum(sp, w => w.Acwp)   ← from SMTS, not recalculated
      spi  = bcwp / bcws
      cpi  = bcwp / acwp
      progress = bcwp / bac
      → Creates/Updates EvmWeeklySnapshot row
  ↓
Returns count of snapshots written
  ↓
EVMViewModel.Refresh() called if EVM view is active
```

## Prerequisite: SMTS Import Before Close Week

> [!IMPORTANT]
> Close Week reads ACWP from the current in-memory model. If the weekly SMTS CSV has not been imported before clicking Close Week, the snapshot ACWP will be **stale** (reflecting prior weeks). The confirmation dialog reminds PMs to import first.

## Recommended Weekly Workflow

```
Monday morning:
  1. PM or Section Chief imports SMTS CSV via "Import Hours" button
  2. Verifies ACWP values in EVM view look correct
  3. Clicks CLOSE WEEK to freeze the snapshot
  4. (Future) Reviews and locks the snapshot
  → S-Curve updates with the new data point
```

## Related Pages
- [[EVM Calculation Rules]] — snapshot business rules and S-Curve integration
- [[DataService]] — `TakeWeeklySnapshotsAsync()` and helper methods
- [[CsvImportService]] — must run before Close Week
- [[Navigation & Ribbons]] — CLOSE WEEK button location
- [[EVM]] — the view that displays snapshot data
