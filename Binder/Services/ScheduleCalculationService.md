---
tags: [service, schedule, gao]
purpose: AI reference for the CPM scheduling engine (Forward Pass, Backward Pass, Float).
---

# ScheduleCalculationService

**File**: `ProjectManagement/Services/ScheduleCalculationService.cs`

The schedule engine that implements **Critical Path Method (CPM)** per GAO-16-89G standards. Dates are the **result** of duration + predecessor logic, never manual input.

## Core Algorithm

## Schedule Modes
Projects (`SystemItem`) now operate in one of two modes defined by `ScheduleMode`:
1. **Dynamic (GAO-Compliant)**: The engine automatically determines all Start/End dates using predecessors and durations.
2. **Manual**: The engine totally skips the Forward Pass. The user's manually entered dates are locked in and respected.

### Three-Pass Approach

```
1. Forward Pass  → Calculates EarlyStart / EarlyFinish (Skipped for Manual projects)
2. Backward Pass → Calculates LateStart / LateFinish (Runs for ALL projects)
3. Float Rollup  → TotalFloat = LateFinish - EarlyFinish (Runs for ALL projects)
```

### Forward Pass (Drives Dates - Dynamic Only)
For each leaf with predecessors:
```
EarlyStart = Max(all predecessor EarlyFinish dates) + 1 business day
EarlyFinish = AddBusinessDays(EarlyStart, DurationDays)
```

**No-predecessor rules (import-safe):**
- If task already has a `StartDate` → **keep it** (respects MPP import, manual entry)
- If task has no `StartDate` → default to `StartNoEarlierThan ?? DateTime.Today`
- If `StartNoEarlierThan > StartDate` → SNET constraint wins
- Summary nodes: `Start = Min(children)`, `End = Max(children)`

> [!IMPORTANT]
> Tasks without predecessors are "unconstrained anchors." The forward pass must NOT overwrite their dates with `DateTime.Today`, otherwise all imported MSP schedules lose their original timeline.

### Backward Pass (Drives Float)
Starting from `ProjectEndDate`:
```
LateFinish = Min(all successor LateStart dates) - 1 business day
LateStart = AddBusinessDays(LateFinish, -DurationDays)
```
If no successors → `LateFinish = ProjectEndDate`

### Total Float Calculation
```
TotalFloat = GetBusinessDaysSpan(EarlyFinish, LateFinish)
IsCritical = (TotalFloat == 0)
```

### Float Rollup to Summary Nodes
```
Parent.TotalFloat = Min(children.TotalFloat)
Parent.IsCritical = children.Any(c => c.IsCritical)
```

## Key Bug Fix: AddBusinessDays (Negative Support)

> [!WARNING]
> The original `AddBusinessDays` method did not handle negative day values. The backward pass was completely broken — `while (added < days)` with `days = -45` always evaluated `false` immediately. The fix adds directional stepping:
> - Positive days → step forward, skip weekends
> - Negative days → step backward, skip weekends

**Files containing `AddBusinessDays`:**
- `ProjectData.cs` (static utility)
- `SystemHierarchyItemViewModel.cs` (local copy — must stay in sync)

## Key Bug Fix: PredecessorParser Regex

The original regex `[^+-FSWH]+` excluded characters S, F, W, H from IDs, causing predecessor IDs like `SYS-02acd4...` to silently fail. Replaced with a suffix-anchored pattern that strips `FS|SS|FF|SF` + lag from the end.

**File**: `ProjectManagement/Services/PredecessorParser.cs`

## Dependency Types

| Type | Meaning | Implementation |
|------|---------|----------------|
| `FS` | Finish-to-Start | Successor starts after predecessor finishes (default) |
| `SS` | Start-to-Start | Not yet wired in forward pass |
| `FF` | Finish-to-Finish | Not yet wired in forward pass |
| `SF` | Start-to-Finish | Not yet wired in forward pass |

## Related Pages
- [[WorkBreakdownItem]] — `DurationDays`, `Predecessors`, `TotalFloat`, `IsCritical` fields
- [[Templates]] — `TemplateTask.Predecessors` relative index format
- [[EVM Calculation Rules]] — BCWS depends on schedule dates
- [[Gantt View]] — visual rendering including dependency arrows
- [[System Management]] — `PredecessorDisplayText` shows WBS codes instead of GUIDs
