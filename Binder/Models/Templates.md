---
tags: [model]
purpose: AI reference for the template system blueprints.
---

# Templates

**Files**: `ProjectManagement/Models/Templates/`

Templates are **blueprints** stored separately from live data. They define reusable WBS structures that can be applied to any Sub-Project.

## Template Hierarchy

```
ProjectTemplate
└── Gates: List<TemplateGate>
    ├── Tasks: List<TemplateTask>       ← NEW: Schedule-driving leaf tasks
    └── Blocks: List<TemplateProgressBlock>  ← Checklists (internal milestones)
        └── Items: List<TemplateProgressItem>
```

> [!IMPORTANT]
> A gate with `Tasks` becomes a **Summary Node** when applied. Its tasks become individually-schedulable leaf `WorkBreakdownItem` nodes. A gate without tasks stays a flat leaf with `ProgressBlocks`.

## GAO Decision Rule

> "Does another task depend specifically on this item finishing?"
> - **YES** → Make it a `TemplateTask`
> - **NO** → Keep it as a `TemplateProgressItem` (checklist)

## ProjectTemplate
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Auto-increment PK |
| `Name` | `string` | e.g., "Default Template" |
| `Description` | `string` | Template purpose |
| `Gates` | `List<TemplateGate>` | Gate definitions |

## TemplateGate
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Auto-increment PK |
| `Name` | `string` | e.g., "Design", "Integration" |
| `SortOrder` | `int` | Sequence within template |
| `DurationDays` | `int` | Default duration (0 for summary gates with tasks) |
| `Predecessors` | `string?` | Relative index: `"0"` = first gate |
| `Tasks` | `List<TemplateTask>` | Schedule-driving child tasks |
| `Blocks` | `List<TemplateProgressBlock>` | Internal checklists |

## TemplateTask *(New)*
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Auto-increment PK |
| `Name` | `string` | e.g., "Software Development", "Hardware Delivery" |
| `SortOrder` | `int` | Sequence within gate |
| `DurationDays` | `int` | Business days (vendor lead time for Receipt tasks) |
| `WorkHours` | `double` | Internal effort hours (0 for Receipt tasks) |
| `ItemType` | `WorkItemType` | `Leaf` (normal work) or `Receipt` (external dependency) |
| `Predecessors` | `string?` | Relative: `"GateSortOrder.TaskSortOrder"` (e.g., `"1.0, 1.1, 1.2"`) |

## TemplateProgressBlock / TemplateProgressItem
Mirror the live `ProgressBlock` / `ProgressItem` structure but without execution data.

## Predecessor Reference Format
Template predecessors use sort-order indices, not IDs:
- `"0"` → Gate with SortOrder 0
- `"1.0"` → Gate 1, Task 0
- `"1.0, 1.1, 1.2"` → Multiple predecessors (merge point)

`TemplateService.ApplyTemplateAsync` resolves these to actual generated IDs at apply time.

## Conversion Flow
When applied via [[TemplateService]]:

**Path A — Gate with Tasks (Logic-Driven):**
```
TemplateGate       → WorkBreakdownItem (Summary, Level 3)
  TemplateTask     → WorkBreakdownItem (Leaf, Level 4) with predecessors + duration
```

**Path B — Gate without Tasks (Flat Leaf):**
```
TemplateGate           → WorkBreakdownItem (Leaf, Level 3)
  TemplateProgressBlock → ProgressBlock
    TemplateProgressItem → ProgressItem
```

## Default Template Structure
```
📁 Design (Gate, Flat Leaf, 30d)
│  └── ProgressBlocks: Develop TPSDD, Develop HRM, Review TPSDD, etc.
│
📁 Integration (Gate, Summary, rolled-up duration)
   ├── 🔧 Software Development   (Leaf, 45d, 360h, Pred: Design)
   ├── 📦 Hardware Delivery       (Receipt, 60d, 0h, no pred)
   ├── 📦 UUT Delivery            (Receipt, 30d, 0h, no pred)
   └── 🔧 Station Integration    (Leaf, 20d, 160h, Pred: SW Dev + HW + UUT)
```

## DB Registration
- `AppDbContext.TemplateTasks` DbSet
- Cascade delete from `TemplateGate` → `TemplateTask`
- Migration: `AddTemplateTask`

## Related Pages
- [[TemplateService]] — the conversion engine
- [[Apply Template Flow]] — user-facing flow
- [[ProgressBlock & Items]] — the resulting live data
- [[Data Hierarchy]] — where templates fit in the tree
- [[ScheduleCalculationService]] — how predecessors drive dates after apply
