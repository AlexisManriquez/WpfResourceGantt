---
tags: [service]
purpose: AI reference for the template-to-live-data conversion engine.
---

# TemplateService

**File**: `ProjectManagement/Services/TemplateService.cs`

Bridges [[Templates]] (blueprints) to live [[WorkBreakdownItem]] data. Supports both flat checklists and GAO-compliant logic-driven task structures.

## Key Methods

| Method | Purpose |
|--------|---------|
| `GetAllTemplatesAsync()` | Returns all `ProjectTemplate` records from DB |
| `ApplyTemplateAsync()` | Converts template to live WBS items (dual-path) |

## ApplyTemplateAsync — Dual-Path Flow

### Phase 1: ID Map Construction
Builds a `Dictionary<string, string>` mapping template-relative indices to generated IDs so task-level predecessors can resolve:
- `"0"` → actual gate 0 ID
- `"1.0"` → gate 1, task 0 ID

### Phase 2: Gate Processing
For each `TemplateGate` (ordered by `SortOrder`):

**Path A — Gate has `Tasks` (Logic-Driven, GAO-Compliant):**
1. Gate becomes a **Summary** `WorkBreakdownItem` (`DurationDays = 0`, `Work = 0`)
2. For each `TemplateTask`:
   - Creates child `WorkBreakdownItem` (Leaf, Level 4)
   - Sets `DurationDays`, `WorkHours`, `ItemType` from template
   - Resolves `Predecessors` from relative index format (`"1.0, 1.1, 1.2"`) to actual generated IDs
   - Adds as child of the gate

**Path B — Gate has NO Tasks (Flat Leaf):**
1. Gate becomes a **Leaf** `WorkBreakdownItem` with its own duration
2. `TemplateProgressBlock` → `ProgressBlock`
3. `TemplateProgressItem` → `ProgressItem`

### Phase 3: Finalization
1. `RecalculateRollup()` on the sub-project
2. `DataService.RegenerateWbsValues()` for WBS codes
3. `DataService.SaveDataAsync()` persists to SQL

## EF Query (Include Chain)
```csharp
context.ProjectTemplates
    .Include(t => t.Gates).ThenInclude(g => g.Blocks).ThenInclude(b => b.Items)
    .Include(t => t.Gates).ThenInclude(g => g.Tasks)
```

## ID Generation
- Gate: `SubProjectId|gateSequence`
- Task: `GateId|taskSequence_randomSuffix`
- ProgressBlock: `PB-randomHex`

## Dependencies
- Depends on [[DataService]] for persistence and WBS regeneration
- Created in [[MainViewModel]] constructor: `new TemplateService(_dataService)`

## Related Pages
- [[Templates]] — the blueprint models (includes `TemplateTask`)
- [[Apply Template Flow]] — the UI-facing feature
- [[DataService]] — persistence
- [[Data Hierarchy]] — ID format rules
- [[ScheduleCalculationService]] — schedule engine runs after template apply
