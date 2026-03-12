---
tags: [service]
purpose: AI reference for the central data repository. The most critical service in the application.
---

# DataService

**File**: `ProjectManagement/DataService.cs` (~1840 lines)

The central repository for **all data I/O**. Every ViewModel depends on this service.

## Core Architecture
- Holds `_projectData: ProjectData` (in-memory model)
- Exposes `AllUsers` and `AllSystems` as read-through properties
- Fires `DataChanged` event after every save to trigger UI refresh
- Uses a **debounced save** pattern (300ms `CancellationTokenSource`)
- Uses EF Core `TrackGraph` for smart upsert (see [[Data Lifecycle]])
- Accepts `IEvmCalculationService` via constructor injection

## Constructors

| Constructor | Used by | Notes |
|---|---|---|
| `DataService(IEvmCalculationService, IScheduleCalculationService, IResourceAnalysisService)` | `MainViewModel` | Primary — full DI |
| `DataService()` (parameterless) | `StartupViewModel` | Creates internal services; login-only |

## Key Method Groups

### Data Loading
| Method | Purpose |
|--------|---------|
| `LoadDataAsync()` | Loads all data from SQL, rebuilds tree, calls `_evmService.RecalculateAll()`, saves BCWS back to DB |
| `SortChildrenRecursive()` | Orders children/blocks/items by Sequence after load |
| `SeedDatabaseFromJson()` | Nuclear restore from JSON backup |

### Data Saving
| Method | Purpose |
|--------|---------|
| `SaveDataAsync()` | **Debounced** entry point (300ms delay) |
| `EnsureSavedAsync()` | Immediate save (used on app shutdown) |
| `ExecuteDbSaveAsync()` | Actual save — collects IDs, deletes orphans, TrackGraph upsert |

### System CRUD
| Method | Purpose |
|--------|---------|
| `AddSystem()` | Adds to `_projectData.Systems` |
| `UpdateSystem()` | Updates in-memory SystemItem properties |
| `DeleteSystemAsync()` | Removes from memory + cascades cleanup of `ManagedProjectIds` + triggers save |
| `GetSystemById()` | Finds by ID |
| `GetSystemsForUser()` | Role-filtered system list (see access control below) |
| `CloneSystem()` | Deep clone with new IDs |

### Role-Based System/Project Filtering
| Method | Purpose |
|--------|---------|
| `GetSystemsForUser()` | Routes to appropriate filter based on user role |
| `FilterProjectsForPM()` | Returns all systems, filters Level 1 children to PM's managed projects |
| `FilterSystemsForDeveloper()` | Returns only systems containing developer's assigned tasks |

**`FilterProjectsForPM` logic**: A PM "manages" a project if:
1. Project ID is in `User.ManagedProjectIds`, **OR**
2. PM has a `ResourceAssignment` on the project (Level 1 item)

### Deletion Cascade Cleanup
| Method | Purpose |
|--------|---------|
| `CleanupManagedProjectIds(List<string> deletedIds)` | Removes deleted IDs from all PM users' `ManagedProjectIds` |
| `CollectAllItemIdsRecursive(items, ids)` | Recursively collects all descendant item IDs |
| `CleanupAssignmentsOnDelete(items)` | Clears `Assignments` and `AssignedDeveloperId` on deleted branches |

### Work Item Operations
| Method | Purpose |
|--------|---------|
| `GetWorkBreakdownItemById()` | Recursive tree search by ID |
| `CloneWorkItem()` | Deep clone with new IDs, reset execution data |
| `ReorderWorkItem()` | Swaps two sibling items |
| `SanitizeImportedIds()` | Ensures globally unique IDs on import |
| `SanitizeChildrenIds()` | Regenerates `ParentID|Sequence` IDs |
| `RegenerateWbsValues()` | Updates display WBS codes |

### EVM / Baseline
| Method | Purpose |
|--------|---------|
| `BaselineSystemAsync()` | Freezes BAC on all leaves: `BAC = Work × $195` |
| `CalculateAndSetBAC()` | Recursive BAC calculation |
| `CalculateBACRecursive()` | Uses developer HourlyRate or default $195 |

### Weekly EVM Snapshots (Phase 3)
| Method | Purpose |
|--------|---------|
| `TakeWeeklySnapshotsAsync(userId)` | Captures one frozen `EvmWeeklySnapshot` row per SubProject (Level 2). Respects `IsLocked` rule. |
| `GetSnapshotsForSubProjectAsync(id)` | Returns chronological snapshot history for one SubProject |
| `GetSnapshotsForSystemAsync(systemId)` | Returns snapshots for all SubProjects under a system |
| `GetWeekEndingDate(date)` (static) | Returns the prior Sunday for any given date |
| `CollectByLevel(items, level, result)` | Recursive helper to collect nodes at a given depth |
| `GetLeafSum(item, selector)` | Sums a numeric property across all leaf descendants |

> [!NOTE]
> `TakeWeeklySnapshotsAsync` reads `Acwp` directly from the in-memory model (already set by `CsvImportService`). It does **not** recalculate ACWP. If the SMTS import for the week hasn't been done, snapshot ACWP will reflect prior week values.

### Resource Gantt Data
| Method | Purpose |
|--------|---------|
| `GetResourceGanttDataAsync()` | Builds `ResourcePerson/ResourceTask` collections for [[Resource Gantt]] |
| `GetUnassignedGanttTasksAsync()` | Finds leaf tasks with no assignments |

### Project Office Symbol Derivation
The project office symbol is derived by:
1. Extracting the Level 1 project ID from the work item's ID
2. Finding the PM who manages that project (via `User.ManagedProjectIds`)
3. Finding the Section Chief who manages that PM (via `SectionChief.ManagedProjectManagerIds`)
4. Using the Section Chief's `Section` as the project office symbol

### Backup
| Method | Purpose |
|--------|---------|
| `ExportFullDatabaseToBackup()` | JSON export of all 8 tables |

### User CRUD
| Method | Purpose |
|--------|---------|
| `AddUser()`, `SaveUserAsync()`, `DeleteUserAsync()` | User management |
| `RemoveUserAssignmentsRecursive()` | Cleans up assignments when user is deleted |

### Admin Tasks
| Method | Purpose |
|--------|---------|
| `CreateAdminTaskAsync()`, `GetAdminTasksForUserAsync()`, `UpdateAdminTaskAsync()`, `DeleteAdminTaskAsync()` | Quick task CRUD |

## Default Constants
| Constant | Value | Usage |
|----------|-------|-------|
| `DEFAULT_HOURLY_RATE` | `195.0m` | Fallback when user has no custom rate |
| `PREFERENCES_FILE` | `"user_preferences.json"` | Last view state |

## Related Pages
- [[Data Lifecycle]] — end-to-end flow through this service
- [[EVM Calculation Rules]] — BAC/BCWS formulas, snapshot business rules
- [[WorkBreakdownItem]] — the primary entity managed
- [[MainViewModel]] — primary consumer
- [[CsvImportService]] — ACWP import pipeline (feeds snapshot ACWP)
- [[User & Role]] — role-based access control matrix
- [[Authentication & Deployment]] — login and auto-login flow
