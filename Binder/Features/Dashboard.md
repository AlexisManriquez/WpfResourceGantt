---
tags: [feature]
purpose: AI reference for the high-level dashboard view.
---

# Dashboard

**Files**: `ProjectManagement/Features/Dashboard/DashboardViewModel.cs`, `DashboardView.xaml`

## Orchestration
- **Trigger**: `TacticalRibbonView` → DASHBOARD button
- **Command**: `MainViewModel.ShowDashboardCommand`
- **Handler**: `CurrentView = "Dashboard"`

## Key Capabilities
- Aggregated KPIs: `TotalProjects`, `RedProjectsCount`
- Health cards per system with status indicators
- Navigation to detailed views via card selection

## Related Pages
- [[MainViewModel]] — orchestration
- [[DataService]] — data source
- [[Navigation & Ribbons]] — view registration
