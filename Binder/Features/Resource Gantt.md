---
tags: [feature]
purpose: AI reference for the Resource Gantt capacity planning view.
---

# Resource Gantt

**Files**: `ProjectManagement/Features/ResourceGantt/ResourceGanttViewModel.cs`, `ResourceGanttView.xaml`

## Orchestration
- **Trigger**: `TacticalRibbonView` → RESOURCE GANTT button
- **Command**: `MainViewModel.ShowGanttCommand`
- **Handler**: `CurrentView = "Resource Gantt"`

## Key Capabilities
- Person-centric timeline showing who works on what and when
- `SectionStatistics`: Availability counts per organizational section
- Uses persistent `GanttContext` on [[MainViewModel]] to preserve filter state
- Loads data via `DataService.GetResourceGanttDataAsync()` which:
  - Fetches only leaf tasks (filters out parent/summary nodes)
  - Maps developer assignments to ResourcePerson/ResourceTask
  - Includes AdminTasks as "Administrative" entries
  - Derives Project Office Symbol from SectionChief assignments

## Key Behavior
- Visible when `IsGanttVisible = true` in `MainWindow.xaml`
- AdminTasks appear with `ProjectOfficeSymbol = "ADM"`
- Tasks sorted by StartDate within each person

## Related Pages
- [[Resource & Gantt Models]] — ResourcePerson/ResourceTask models
- [[DataService]] — `GetResourceGanttDataAsync()`
- [[User & Role]] — user and section data
- [[Analytics]] — complementary utilization view
