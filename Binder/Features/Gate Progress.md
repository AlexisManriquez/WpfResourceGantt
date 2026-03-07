---
tags: [feature]
purpose: AI reference for the Gate Progress drill-down view.
---

# Gate Progress

**Files**: `ProjectManagement/Features/Gantt/GateProgressViewModel.cs`, `GateProgressView.xaml`

## Orchestration
- **Trigger**: Launch icon button on Level 2 (Sub-Project) rows in [[Gantt View]]
- **Command**: `GanttViewModel.OpenGateProgressCommand`
- **Handler**: `MainViewModel.GoToGateProgress(WorkItem)`

## Key Capabilities
- Displays a specific Sub-Project's Gates (Level 3) and their underlying test/progress blocks
- Uses recursive data loading from the WBS tree
- Shows `IsDateVisible` conditionally: start/end dates only for "Gate" type rows
- Uses `MapToRowRecursive()` to flatten the hierarchy for display

## Related Pages
- [[Gantt View]] — parent view with drill-down trigger
- [[WorkBreakdownItem]] — Gate-level data
- [[ProgressBlock & Items]] — test block details
- [[MainViewModel]] — `GoToGateProgress()` navigation handler
