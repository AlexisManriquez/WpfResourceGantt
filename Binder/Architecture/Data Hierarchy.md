---
tags: [architecture, model]
purpose: AI-optimized reference for the recursive data hierarchy. Critical for any data-related change.
---

# Data Hierarchy

The entire data model is a recursive tree rooted at `ProjectData`. Understanding this hierarchy is **essential** for any modification to data, persistence, or UI binding.

## The WBS Tree (Live Data)

```
ProjectData (root JSON)
├── Users: List<User>
├── AdminTasks: List<AdminTask>  
└── Systems: List<SystemItem>           ← Level 0 (structural container)
    └── Children: List<WorkBreakdownItem>   ← Level 1 (Project)
        └── Children: List<WorkBreakdownItem>   ← Level 2 (Sub-Project)
            └── Children: List<WorkBreakdownItem>   ← Level 3 (Gate)
                └── Children: List<WorkBreakdownItem>   ← Level 4+ (Task)
                    └── (recursive — infinite depth)
```

## Hierarchy Levels

| Level | Name | Class | Has EVM? | Key Rule |
|-------|------|-------|----------|----------|
| 0 | System | `SystemItem` | ❌ | Structural only. No dates, no work, no EVM. |
| 1 | Project | `WorkBreakdownItem` | ✅ (rollup) | First level with execution metrics |
| 2 | Sub-Project | `WorkBreakdownItem` | ✅ (rollup) | Entry point for [[Gate Progress]] drill-down |
| 3 | Gate | `WorkBreakdownItem` | ✅ (rollup or leaf) | Can be a **Summary** (with child tasks) or a **Leaf** (with checklists) |
| 4+ | Task | `WorkBreakdownItem` | ✅ (leaf/rollup) | Can be a leaf or have children |

## Leaf vs Summary Distinction

> **CRITICAL RULE**: A `WorkBreakdownItem` with `Children.Count > 0` is a **Summary** node. Otherwise, it is a **Leaf** node. This distinction controls **everything**.

| Aspect | Leaf Node | Summary Node |
|--------|-----------|--------------|
| Progress source | `ProgressBlocks` checklist completion | Rolled up from children |
| BCWP calculation | `BAC * Progress` | `Sum(children.Bcwp)` |
| BCWS calculation | Time-proportional from dates | `Sum(children.Bcws)` |
| Schedule dates | Driven by `DurationDays` + `Predecessors` | `Min/Max(children dates)` |
| TotalFloat | Calculated by backward pass | `Min(children.TotalFloat)` |
| Assignments | Has `ResourceAssignment` records | Assignments are **deleted** on save |
| ProgressBlocks | Has blocks + checklist items | Blocks are ignored/orphaned |

## Execution Detail (Inside a Leaf Task)

```
WorkBreakdownItem (Leaf)
├── ProgressBlocks: List<ProgressBlock>
│   └── Items: List<ProgressItem>        ← Individual checkboxes
├── ProgressHistory: List<ProgressHistoryItem>  ← S-curve data points
└── Assignments: List<ResourceAssignment>       ← Developer assignments
```

- Checking a `ProgressItem.IsCompleted` → recalculates `Progress` on the parent task
- See [[ProgressBlock & Items]] for detail

## The Template Hierarchy (Blueprints)

Separate from live data, templates define reusable structures:

```
ProjectTemplate
└── Gates: List<TemplateGate>
    ├── Tasks: List<TemplateTask>          ← Schedule-driving leaf nodes (NEW)
    └── Blocks: List<TemplateProgressBlock> ← Internal checklists
        └── Items: List<TemplateProgressItem>
```

### Dual-Path Conversion
When applied via [[TemplateService]]:

**Path A — Gate with Tasks (GAO Logic-Driven):**
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

> [!TIP]
> GAO Decision Rule: "Does another task depend on this item finishing?" YES → `TemplateTask`. NO → `TemplateProgressItem`.

## ID Format Convention

IDs follow a **pipe-delimited parent path** format:
```
SystemItem.Id:   "SYS-a1b2c3d4"
Level 1 child:   "SYS-a1b2c3d4|0"
Level 2 child:   "SYS-a1b2c3d4|0|1"
Level 3 child:   "SYS-a1b2c3d4|0|1|2"
Level 4 task:    "SYS-a1b2c3d4|0|1|2|0_a3f2"
```

**Rules**:
- `SanitizeChildrenIds()` in [[DataService]] regenerates these on import
- The last segment is `sequence_randomSuffix` for cloned/template items
- Parent derivation: `item.Id.Substring(0, item.Id.LastIndexOf('|'))` gives the parent ID

## Related Pages
- [[ProjectData & SystemItem]] — detailed model reference
- [[WorkBreakdownItem]] — field-level documentation
- [[Templates]] — blueprint models including `TemplateTask`
- [[ScheduleCalculationService]] — how predecessors drive dates and float
- [[EVM Calculation Rules]] — how rollup drives financial metrics
- [[Data Lifecycle]] — how this tree flows through the system
