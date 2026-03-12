---
tags: [model]
purpose: AI reference for the root data object and Level 0 SystemItem container.
---

# ProjectData & SystemItem

## ProjectData (Root)
**File**: `ProjectManagement/Models/ProjectData.cs`

The root deserialization target for the entire project dataset.

```csharp
public class ProjectData
{
    public List<User> Users { get; set; }
    public List<SystemItem> Systems { get; set; }
    public List<AdminTask> AdminTasks { get; set; }
    public bool IsEvmHoursBased { get; set; } // Global EVM unit toggle
}
```

Held in memory by [[DataService]] as `_projectData`. All modifications happen to this in-memory object. Persistence to SQL happens through [[DataService]]'s `ExecuteDbSaveAsync()`.

## SystemItem (Level 0)
**File**: `ProjectManagement/Models/ProjectData.cs` (line ~132)

A **pure structural container** — no execution metrics, no dates, no EVM, **no owner/PM assignment**.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | Format: `"SYS-{guid8}"` |
| `WbsValue` | `string` | Display WBS code |
| `Name` | `string` | System name |
| `Status` | `WorkItemStatus` | Active/Complete/etc. |
| `Sequence` | `int` | Sort order |
| `Children` | `List<WorkBreakdownItem>` | Level 1 Projects |

> [!IMPORTANT]
> `ProjectManagerId` was **removed** from `SystemItem`. Systems are containers only. PM-to-project assignment is now tracked on the [[User & Role|User]] model via `ManagedProjectIds` and `ResourceAssignment` on Level 1 items.

### RecalculateRollup()
Simply delegates to children — `SystemItem` itself stores **no** aggregated metrics:
```csharp
public void RecalculateRollup()
{
    foreach (var child in Children) child.RecalculateRollup();
}
```

## Related Pages
- [[Data Hierarchy]] — where SystemItem sits in the tree
- [[WorkBreakdownItem]] — what Children contain
- [[System Management]] — CRUD UI for SystemItems
- [[DataService]] — persistence logic
