---
tags: [feature]
purpose: AI reference for the Analytics resource utilization view.
---

# Analytics

**Files**: `ProjectManagement/Features/Analytics/AnalyticsViewModel.cs`, `AnalyticsView.xaml`

## Orchestration
- **Trigger**: `TacticalRibbonView` → ANALYTICS button
- **Command**: `MainViewModel.ShowAnalyticsCommand`
- **Handler**: `CurrentView = "Analytics"`

## Key Capabilities
- Resource utilization analysis
- `Resources`: List of users with workload metrics
- `UnassignedTasks`: "Bench" report showing tasks without developers
- Uses `LiveCharts` for visualization

## Related Pages
- [[DataService]] — `GetUnassignedGanttTasksAsync()`
- [[User & Role]] — resource data source
- [[Resource Gantt]] — complementary capacity view
