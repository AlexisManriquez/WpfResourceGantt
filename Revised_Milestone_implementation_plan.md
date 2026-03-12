# Implementation Plan: Polished Milestone Integration (Revised)

## User Review Required
Please review the proposed approach. Milestones will now be an enum type rather than a boolean, and will be entirely excluded from standard Rollup (timeline) and EVM (financial/resource) computations. 

## Proposed Changes

### Data Model Enhancements
#### [MODIFY] [ProjectManagement/Models/ProjectData.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Models/ProjectData.cs)
- **Enum Approach**: Introduce `WorkItemType` enum (`Standard`, `Milestone`) and add an `ItemType` property to `WorkBreakdownItem`.
- **Constraint Enforcement**: Add an `EnforceMilestoneConstraints` logic block that forces `DurationDays = 0` and ensures `StartDate == EndDate` upon assigning a milestone length.
- **Rollup Exclusion**: Modify `RecalculateRollup` so that when traversing children, any child where `ItemType == WorkItemType.Milestone` is completely ignored. Parent items will not expand their `StartDate` or `EndDate`, and will not accumulate `DurationDays` from milestones.

#### [MODIFY] [ProjectManagement/data/AppDbContext.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/data/AppDbContext.cs)
- Map the `ItemType` enum to the `WorkItems` table.

### Core Services
#### [MODIFY] [ProjectManagement/Services/EvmCalculationService.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Services/EvmCalculationService.cs)
- EVM engines will feature a strict exclusion clause. Milestones generate zero Planned Value (PV) curves, accrue zero Earned Value (EV), and their execution does not weigh into Cost Performance (CPI) or Schedule Performance (SPI) indices.

#### [MODIFY] [ProjectManagement/Services/ScheduleCalculationService.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Services/ScheduleCalculationService.cs)
- Adjust dependency schedules so a successor to a milestone starts directly upon milestone completion, without any duration buffering inherited from the milestone itself.

#### [MODIFY] [ProjectManagement/Services/TemplateService.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Services/TemplateService.cs)
- Ensure the `ItemType` state is saved to templates and correctly unpacked when instantiating new projects.

#### [MODIFY] [ProjectManagement/DataService.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/DataService.cs)
- Update `CloneWorkItem` to ensure `ItemType` transfers appropriately, but `ActualFinishDate` is correctly nulled out so milestones are cloned in an uncompleted state.

### UI & ViewModels
#### [MODIFY] [ProjectManagement/Features/SystemManagement/SystemManagementViewModel.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/SystemManagement/SystemManagementViewModel.cs)
#### [MODIFY] [ProjectManagement/Features/SystemManagement/SystemHierarchyItemViewModel.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/SystemManagement/SystemHierarchyItemViewModel.cs)
- Add binding support to represent `ItemType` state and compute a quick-access `IsMilestone` readonly boolean for XAML bindings.

#### [MODIFY] [ProjectManagement/Features/SystemManagement/SystemManagementView.xaml](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/SystemManagement/SystemManagementView.xaml)
- Adapt the DataGrid to only show relevant duration input boxes for Standard items. Milestones reflect a single Target Date column.

#### [MODIFY] [ProjectManagement/Features/Gantt/AnimatedGanttBar.xaml.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Gantt/AnimatedGanttBar.xaml.cs)
- **Visualization Update**: When an item's type is `Milestone`, the Gantt shape engine must override standard duration-based rectangle sizing. Instead, it must stamp a fixed-pixel-size diamond marker (◆) centered over the temporal coordinate of the `EndDate`.

#### [MODIFY] [ProjectManagement/Features/GateProgress/GateProgressViewModel.cs](file:///z:/IMATOOL/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/GateProgress/GateProgressViewModel.cs)
- Add automation hook: When a milestone's internal `ProgressItems` collectively calculate to 100% (1.0), automatically trigger `ActualFinishDate = DateTime.Today`.

## Verification Plan
### Automated Tests
- None specified; relies on in-app manual testing hooks.

### Manual Verification
- **EVM and Time Rollup Tests**: Embed a milestone into a hierarchical system that exceeds the parent bounds. Expected outcome: The parent bound does *not* shift, and EVM budget graphs plot unchanged.
- **Visual Checks**: Assert that the Gantt UI produces a centralized diamond for the milestone rather than failing to draw a 0px duration rectangle.
