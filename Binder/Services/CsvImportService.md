---
tags: [service]
purpose: AI reference for the CSV hours import pipeline. The ONLY authorized writer of ACWP.
---

# CsvImportService

**File**: `ProjectManagement/CsvImportService.cs`

Handles importing actual work hours from SMTS CSV files. This is the **sole authorized writer** of `WorkBreakdownItem.Acwp` in the entire application.

## DoD Compliance Rule

> [!IMPORTANT]
> ACWP must **never** be written by any other service, ViewModel, or UI interaction. Hours come from SMTS (the government labor charging system). If a developer charged to the wrong assignment, a corrected CSV re-import is the only valid fix.

## Import Flow
1. User clicks "Import Hours" in the contextual toolbar
2. `MainViewModel.ImportHours()` opens file dialog
3. `CsvImportService.ImportHoursFromCsvAsync(filePath)` processes the CSV in **three passes**:

### Pass 1: Scan File
- Reads all CSV rows into memory
- Skips header + empty first row
- Requires ≥ 21 columns per row

### Pass 2: Update Actual Work
For each CSV row:
- Reads: `fields[0]` = resource name, `fields[2]` = date, `fields[7]` = hours, `fields[20]` = task name
- Matches to `WorkBreakdownItem` by `Name` (case-insensitive)
- Increments `task.ActualWork += hoursToAdd`
- **Writes ACWP**: `task.Acwp += hoursToAdd × user.HourlyRate` (default $195/hr)
- **Stamps audit trail**:
  ```csharp
  task.LastAcwpImportDate = DateTime.Now;
  task.LastAcwpImportSource = Path.GetFileName(filePath);
  ```
- Creates/updates a `ProgressHistoryItem` for that date

### Pass 3: Recalculate History Progress
- For each affected task, recalculates `ActualProgress` on all history entries using linear interpolation from `StartDate` to the latest charged date

4. `DataService.SaveDataAsync()` persists all changes
5. `EvmCalculationService.RecalculateAll()` rolls up ACWP to parent summary nodes on next load

## CSV Format (SMTS)
| Column Index | Field |
|---|---|
| 0 | Resource name (`"LastName, FirstName"`) |
| 2 | Date (`yyyy-MM-dd`) |
| 7 | Hours charged |
| 20 | Task name (matches `WorkBreakdownItem.Name`) |

## Audit Trail Fields
Added to `WorkBreakdownItem` in Phase 2 refactor:

| Field | Type | Purpose |
|-------|------|---------|
| `LastAcwpImportDate` | `DateTime?` | UTC timestamp of last SMTS import for this task |
| `LastAcwpImportSource` | `string` | CSV filename (e.g., `SMTS_W09_2025.csv`) |

## Impact on EVM
- `ACWP` is the **only** EVM metric not calculated by the application
- After import, `CPI` and `CV` change: `CPI = BCWP / ACWP`
- ACWP rollup to summary nodes is handled by `EvmCalculationService` on next load
- PMs should run **Close Week** after each weekly SMTS import to capture a snapshot

## Related Pages
- [[EVM Calculation Rules]] — ACWP write-lock rules and DoD compliance
- [[DataService]] — persistence + weekly snapshot methods
- [[Gantt View]] — the import button lives here
- [[Navigation & Ribbons]] — Close Week button in ribbon
