---
tags: [model]
purpose: AI reference for the checklist system that drives physical % complete.
---

# ProgressBlock & Items

**Files**: `ProjectManagement/Models/ProgressBlock.cs`

These entities exist **only on leaf [[WorkBreakdownItem]] nodes** and drive the physical % complete used for BCWP.

## ProgressBlock
A logical grouping of checklist items (e.g., "Design Review", "Code Implementation").

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | Format: `"PB-{guid12}"` |
| `Name` | `string` | Block title |
| `Sequence` | `int` | Sort order |
| `Items` | `List<ProgressItem>` | Checklist items |

## ProgressItem
An individual checkbox representing one unit of work.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | GUID |
| `Name` | `string` | Item description |
| `Sequence` | `int` | Sort order |
| `IsCompleted` | `bool` | Checked = done |

## How Progress is Calculated

```
Task.Progress = CompletedItems.Count / TotalItems.Count
```

When `IsCompleted` changes:
1. The parent task's `Progress` property updates (0.0 to 1.0)
2. [[WorkBreakdownItem]] `RecalculateRollup()` computes: `BCWP = BAC × Progress`
3. Rollup propagates up the tree to summary nodes
4. [[DataService]] `SaveDataAsync()` persists to DB

## ProgressHistoryItem
**File**: `ProjectManagement/Models/ProgressHistoryItem.cs`

Time-series snapshots used for S-curve generation in [[EVM]].

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | GUID |
| `Date` | `DateTime` | Snapshot date |
| `ActualProgress` | `double` | 0.0 to 1.0 at that date |
| `ActualWork` | `double` | Hours worked at that date |

## Important Rules
- When a node **gains children** (becomes a summary), its ProgressBlocks become **orphaned** and are deleted on the next save
- Assignments are also orphaned when a node becomes a summary
- ProgressBlocks are deep-cloned during [[Apply Template Flow]] and item cloning

## Related Pages
- [[WorkBreakdownItem]] — parent entity
- [[EVM Calculation Rules]] — how Progress feeds BCWP
- [[Data Lifecycle]] — when blocks are saved/deleted
- [[Templates]] — how blocks are created from blueprints
