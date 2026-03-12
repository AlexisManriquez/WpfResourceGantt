Implementation Plan: Subproject-Level Milestone Integration

1. Architectural Overview and Objective

The core purpose of the Milestone feature is to provide a specialized execution mode for WorkBreakdownItem entities at Level 2 (Subproject level). Architecturally, while standard Subprojects represent a span of time and effort, a Milestone represents a singular point in time—a project "Gate."

Milestones are distinguished by their use of a point-in-time "Planned vs. Actual" tracking model. Although they are points in time, they leverage the existing checklist infrastructure (ProgressBlocks and ProgressItems) to determine completion. This implementation allows project leads to define fixed temporal targets and automate their realization through granular execution data.

2. Data Model Enhancements

The WorkBreakdownItem class within ProjectData.cs requires the addition of the IsMilestone flag. Other required properties for tracking dates already exist in the model but must be repurposed for milestone-specific logic.

Property	Type	Status	Description
IsMilestone	bool	New	Flag to differentiate milestones from standard subprojects (Level 2 only).
EndDate	DateTime?	Existing	Repurposed as the "Planned Date" for the milestone.
ActualFinishDate	DateTime?	Existing	Repurposed as the "Actual Date" when the milestone is completed.

Logic Command: Duration and Rollup Handling For items where IsMilestone is true:

* Duration: The DurationDays property must be set to 0 to represent a point in time.
* Rollup Logic: Modify the RecalculateRollup method in ProjectData.cs. When IsMilestone is true, the EndDate should not be dynamically calculated as the Max() of its children's dates; instead, it should act as a fixed target. The ActualFinishDate should remain null until the milestone prerequisites (checklists) are 100% complete.

3. Creation Workflow Updates: CreateWorkItemViewModel

The CreateWorkItemViewModel.cs manages the instantiation and editing of work items. Updates are required to handle the milestone state during the creation workflow.

* ViewModel Property: Add bool IsMilestone to the ViewModel.
* UI Visibility & Constraints:
  * The Milestone toggle must only be visible/enabled in the UI when _level == 2.
  * Constructor Mapping: Update both the "Create" and "Edit" constructors to map the IsMilestone property from the WorkBreakdownItem entity.
* Validation and Synchronization:
  * Implement logic within the IsMilestone property setter: if true, the DurationDays input must be disabled and set to 0.
  * StartDate must be synchronized to match EndDate to ensure the item represents a single point on the timeline.
  * Ensure the ToModel() logic (or the saving routine) correctly persists the IsMilestone flag back to the DataService.

4. UI Integration: Systems Management Tree-Grid

The SystemManagementViewModel.cs and the associated tree-grid must be updated to facilitate milestone management.

* Milestone Identification: Update SystemHierarchyItemViewModel to include the IsMilestone property. In the XAML View, use a DataTrigger to provide a visual indicator (e.g., bolding the Name text or changing the icon) when IsMilestone is true.
* Inline Editing Requirements:
  * Conditional DatePickers: Modify the Tree-Grid XAML to include columns for Planned Date (binding to EndDate) and Actual Date (binding to ActualFinishDate).
  * Visibility Control: Use a Visibility converter to ensure these DatePickers are only visible and editable if the item's Level == 2.
* Command Integration: The EditDetailsCommand must be updated. If IsMilestone is true, the command must prioritize navigation to the GateProgressViewModel to manage checklist completion.

5. Progress Tracking: Milestone Checklist Integration

Milestones utilize the GateProgressViewModel.cs to track prerequisites via checklists. Completion of these items triggers the milestone's completion date.

* Milestone-to-Checklist Mapping: Milestones retain the ability to possess ProgressBlocks and ProgressItems. This allows the milestone to represent the culmination of specific "Gate" requirements.
* Progress Calculation Logic: Modify the SaveAndRecalculate method in GateProgressViewModel.cs.
  * Automated Completion: If masterItem.IsMilestone is true and the calculatedProgress >= 1.0, the system must automatically set masterItem.ActualFinishDate = DateTime.Today.
  * If progress falls below 100% (e.g., an item is unchecked), ActualFinishDate should be reset to null.
* Snapshot Requirement: The UpdateProgressHistory method must capture this state. This ensures that the ActualFinishDate is recorded in the weekly snapshot, allowing the EVM S-curve to plot the "Actual Completion" point against the planned target.

6. Data Persistence and Sanitation

Backend updates in DataService.cs are required to ensure milestones are correctly handled during data operations.

* Sanitization Logic: Modify SanitizeChildrenIds. Ensure that milestones maintain the standard unique ID format (parentIdPrefix|item.Sequence). This is critical to ensure that checklist items (ProgressBlocks) remain correctly keyed to the milestone ID during hierarchy reorders.
* Cloning Logic: Update the CloneWorkItem method.
  * The IsMilestone flag and the EndDate (Planned Date) must be copied to the new instance.
  * The ActualFinishDate must be explicitly set to null to ensure the clone represents an uncompleted milestone.
* Database Mapping: Update AppDbContext to include the IsMilestone property in the WorkItems table configuration. A database migration is required to add this column to the physical schema.

7. Implementation Checklist

* [ ] ProjectData.cs: Add IsMilestone property to WorkBreakdownItem; update RecalculateRollup to handle DurationDays = 0 and skip date-span logic for milestones.
* [ ] AppDbContext.cs: Add IsMilestone to the WorkItem entity configuration and perform a database migration.
* [ ] CreateWorkItemViewModel.cs: Add IsMilestone property, implement Level 2 visibility logic, and enforce StartDate/EndDate synchronization.
* [ ] SystemManagementViewModel.cs: Update SystemHierarchyItemViewModel with IsMilestone flag; add inline DatePickers for Level 2 items in the tree-grid.
* [ ] GateProgressViewModel.cs: Update SaveAndRecalculate to automate ActualFinishDate setting upon 100% checklist completion.
* [ ] DataService.cs: Update SanitizeChildrenIds for ID preservation and CloneWorkItem to correctly reset milestone completion state.
