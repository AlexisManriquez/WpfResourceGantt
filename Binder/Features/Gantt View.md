---
tags: [feature]
purpose: AI reference for the core WBS editing interface.
---

# Gantt View

**Files**: `ProjectManagement/Features/Gantt/GanttViewModel.cs` (~1633 lines), `GanttView.xaml`, `GanttView.xaml.cs`

The primary editing interface — a hierarchical tree grid showing the full WBS structure.

## Orchestration
- **Trigger**: `TacticalRibbonView` → PROJECTS button
- **Command**: `MainViewModel.ShowProjectManagementCommand`
- **Handler**: `MainViewModel.ShowProjectGanttView()` → `CurrentView = "Projects"`

## Key Capabilities
| Feature | Method |
|---------|--------|
| WBS tree display | `LoadDataForCurrentUser()` → `ConvertToWorkItems()` |
| Filtering (system/project/status) | `ApplyFilter()` → `CalculateVisibility()` |
| Expand / Collapse | `ExpandAll()` / `CollapseAll()` — recursive with preserved state |
| Drag-and-drop reorder | `GanttView.xaml.cs` code-behind + `DataService.ReorderWorkItem()` |
| Health calculation | `CalculateOverallHealthRecursively()` + `CalculateEVMHealth()` |
| Dynamic columns | `InitializeColumns()`, `AddColumn()`, `RemoveColumn()` |
| Timeline generation | `CalculateTimeline()`, `GenerateTimelineHeaders()` |
| WBS ID generation | `GenerateWbsIdsRecursively()` |
| Zoom | `RecalculateTimelineWidths()` |
| Hours import | `ImportHours()` → [[CsvImportService]] |
| Delete system | `DeleteSystem()` |
| **Dependency arrows** | `DependencyArrowCanvas` overlay (see below) |

## Dependency Arrows

**File**: `ProjectManagement/Features/Gantt/DependencyArrowCanvas.cs`

A custom WPF `Canvas` overlay that draws elbow-style FS dependency arrows between Gantt bars.

### How It Works
1. **Collects visible items** — walks the TreeView visual tree to find each `WorkItem`'s Y-center position
2. **Parses predecessors** — uses `PredecessorParser.Parse()` on each item's `Predecessors` string
3. **Date → X pixel** — same math as `AnimatedGanttBar`: `(date - timelineStart) / totalDays * timelineWidth`
4. **Renders elbow path** using `StreamGeometry` via `OnRender(DrawingContext)`:
```
[Predecessor Bar]──┐
                   │
                   └──▶[Successor Bar]
```

### Arrow Styling (3-State)
| Property | Good (Safe) | Warning (Near-Critical) | Bad (Critical) |
|----------|-------------|------------------------|----------------|
| Color | `#64748B` (Slate-500) | `#D97706` (Amber-600) | `#EF4444` (Red-500) |
| Thickness | 1.5px | 1.5px | 1.5px |

Arrow color = worst `ScheduleHealth` of predecessor + successor.

### Refresh Triggers
Arrows redraw on:
- **Scroll** (horizontal/vertical) — via `GanttTree_ScrollChanged`
- **Expand/Collapse** — via `GanttTree_LayoutUpdated` (debounced 50ms)
- **Resize** — via `GanttTree_SizeChanged`
- **Initial load** — via `GanttView_Loaded`

### XAML Integration
```xml
<gantt:DependencyArrowCanvas x:Name="DependencyArrows"
    Grid.Column="2"
    IsHitTestVisible="False"
    Height="{Binding ActualHeight, ElementName=GanttTree}"
    WorkItems="{Binding WorkItems}"
    TimelineStart="{Binding ProjectStartDate}"
    TimelineEnd="{Binding ProjectEndDate}"
    TimelineWidth="{Binding TimelineWidth}"
    HorizontalOffset="{Binding Data.HorizontalOffset, Source={StaticResource ViewProxy}}"
    TreeViewReference="{Binding ElementName=GanttTree}"/>
```

## AnimatedGanttBar

**File**: `ProjectManagement/Features/Gantt/AnimatedGanttBar.xaml`

Each row gets an `AnimatedGanttBar` control that renders:
- **Task bar** — 3-state color based on `ScheduleHealth`:
  - 🔵 **Blue** (`Good`) — plenty of float (> 5 days)
  - 🟡 **Amber** (`Warning`) — near-critical (1–5 days float) — "start the salad now"
  - 🔴 **Red** (`Bad`) — critical path (TotalFloat ≤ 0)
- **Summary bar** — health-colored with SV/CV labels and staple
- **Baseline ghost bar** — faded bar showing original plan dates

### ScheduleHealth (3-State Traffic Light)
The `ScheduleHealth` property on `WorkItem` drives the bar color:
```csharp
if (TotalFloat <= 0) return MetricStatus.Bad;      // Red
if (TotalFloat <= 5) return MetricStatus.Warning;   // Amber
return MetricStatus.Good;                            // Blue
```

### LateStart (Deadline Indicator)
`WorkItem.LateStart` = the latest date a task can start without delaying the project.
- Calculated as `AddBusinessDays(StartDate, TotalFloat)`
- `null` for summary nodes and critical tasks (TotalFloat ≤ 0)
- Can be exposed as a grid column: shows the PM "you must start by this date"

## EVM Display (Post-Refactor)

> [!IMPORTANT]
> As of the Phase 1 EVM refactor, the Gantt view is **display-only** for EVM metrics. It no longer runs its own rollup engine.

### How EVM Values Flow to the Gantt
```
DataService.LoadDataAsync()
  ↓
EvmCalculationService.RecalculateAll(systems)   ← single authoritative rollup
  ↓ BCWS, BCWP, ACWP, Progress saved to DB
  ↓
DataChanged event fires
  ↓
GanttViewModel.LoadDataForCurrentUser()
  ↓ ConvertToWorkItems()  ← maps pre-computed DB values to WorkItem ViewModel
  ↓ CalculateOverallHealthRecursively()  ← colors only, no EVM recalculation
  ↓
Gantt renders with authoritative values
```

## State Preservation
On data refresh, expansion states are preserved via:
- `GetStateRecursive()` — captures `IsExpanded` map
- `SetStateRecursive()` — reapplies after rebuild

## Related Pages
- [[MainViewModel]] — orchestration
- [[DataService]] — data source
- [[WorkBreakdownItem]] — data model
- [[EVM Calculation Rules]] — single rollup engine reference
- [[ScheduleCalculationService]] — CPM schedule engine (drives dates and float)
- [[Gate Progress]] — drill-down from Sub-Project rows
- [[Navigation & Ribbons]] — view registration
