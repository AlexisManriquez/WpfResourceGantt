---
tags: [service]
purpose: AI reference for MS Project import and export.
---

# MppImportExport

**Files**: `ProjectManagement/Services/MppImportService.cs`, `MppExportService.cs`

## MppImportService
Parses MS Project XML exports into the application's data model. Imported projects are typically already-started schedules with tracked progress.

### What Gets Imported
| MSP Field | Model Field | Notes |
|-----------|-------------|-------|
| `task.Work` (minutes) | `Work` (hours) | Planned hours — divided by 60 |
| `task.PercentComplete` | `Progress` (0–1) | Leaf tasks only |
| `task.Start` | `StartDate` | Planned start |
| `task.Finish` | `EndDate` | Planned finish |
| `task.ActualFinish` | `ActualFinishDate` | Only if 100% complete |
| *(derived)* | `DurationDays` | Back-calculated: `GetBusinessDaysSpan(Start, End)` |

### What Is NOT Imported (By Design)
| Field | Reason |
|-------|--------|
| Predecessors | Dependencies are set up in-app, not migrated from MSP |
| ActualWork / ACWP | Imported separately via [[CsvImportService]] (SMTS hours) |
| Baseline dates | Not currently extracted |

### Import Flow
| Method | Purpose |
|--------|---------|
| `ParseMppFile()` | Reads XML, returns `List<SystemItem>` |

1. User selects `.xml` file via [[MainViewModel]]`.ExecuteImportProject()`
2. `MppImportService.ParseMppFile()` parses tasks into `SystemItem` → `WorkBreakdownItem` tree
3. **DurationDays back-calculated** from Start/End for each leaf task
4. `DataService.SanitizeImportedIds()` generates unique pipe-delimited IDs
5. `DataService.SaveDataAsync()` persists
6. On next load, [[ScheduleCalculationService]] runs — **preserves imported dates** (no-predecessor tasks keep their original Start/End)

## MppExportService
Exports the application's WBS to a native MS Project file.

**Key EVM alignment rules**:
- Uses **Physical % Complete** to decouple checklist progress from labor burn-rate
- Disables automatic cost calculations in MS Project
- Forces 5-day, 8-hour Government calendar for Schedule Variance alignment
- Injects `FixedCost` for unassigned tasks
- Maps assignments and resource allocations

**Export flow**:
1. User selects system via [[MainViewModel]]`.ExecuteExportProject()`
2. `MppExportService` builds XML structure
3. Saves to user-selected path

## Related Pages
- [[DataService]] — `ImportMppAndSaveAsync()` orchestrates import
- [[MainViewModel]] — `ExecuteImportProject()`, `ExecuteExportProject()`
- [[EVM Calculation Rules]] — DoD alignment rules
