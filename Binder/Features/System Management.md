---
tags: [feature]
purpose: AI reference for the System Management CRUD view.
---

# System Management

**Files**: `ProjectManagement/Features/SystemManagement/SystemManagementViewModel.cs`, `SystemManagementView.xaml`

## Orchestration
- **Trigger**: `TacticalRibbonView` → SYSTEMS button
- **Command**: `MainViewModel.ShowSystemsCommand`
- **Handler**: `MainViewModel.ShowSystemManagementView()` → `CurrentView = "Systems"`

## Key Capabilities
| Feature | Method/Command |
|---------|----------------|
| Hierarchical system tree display | `HierarchicalSystems` collection |
| Add child item | `AddChild` command |
| Apply template to sub-project | `HandleApplyTemplate` → opens [[Apply Template Flow]] dialog |
| Edit details | `EditDetails` command |
| Delete item (with cascade cleanup) | `DeleteCommand`, `ExecuteDeleteSelected` |
| Copy / Paste | `HandleCopy`, `HandlePaste`, `ExecutePaste` |
| Expansion state preservation | `RefreshData`, `OnDataChanged` |
| Schedule Mode Toggle | Dropdown toggle for **Dynamic** vs **Manual** mode directly in the Name column |
| EVM Mode Awareness | `IsEvmHoursBased` / `EvmDisplayMode` |
| Assign Developer/PM to project | `AssignDeveloper` command |

## Access Control
Systems are **pure containers** — they are not assigned to users. Visibility is role-based:

| Role | Systems Seen | Projects Seen |
|------|-------------|---------------|
| Admin / FlightChief / SectionChief / TechnicalSpecialist | All | All |
| ProjectManager | All (as containers) | Only managed projects |
| Developer | Only relevant | Only assigned branches |

See [[User & Role]] for full access control matrix.

## Deletion Cascade
When a project or any child is deleted, the following cleanup is performed:
1. **Collect** all descendant item IDs (recursive)
2. **Remove** those IDs from all PM users' `ManagedProjectIds`
3. **Clear** all `ResourceAssignment` entries on the deleted branch
4. **Clear** all `AssignedDeveloperId` fields on the deleted branch
5. **Save** to database

Methods involved: `DataService.CollectAllItemIdsRecursive()`, `DataService.CleanupManagedProjectIds()`, `DataService.CleanupAssignmentsOnDelete()`

## PM Assignment Sync
When a PM is assigned to a Level 1 project via the Assign Developer dialog:
1. A `ResourceAssignment` is created on the project item
2. The PM's `User.ManagedProjectIds` is automatically updated to include the project ID
3. When unassigned, the project ID is removed from `ManagedProjectIds`

This ensures `FilterProjectsForPM()` works correctly via both lookup paths.

## SystemHierarchyItemViewModel
Wrapper ViewModel for each tree row with:
- `IsExpanded` with `ToggleExpansionCommand`
- Name parsing: auto-numbers for Level 0–2, free-text for Level 3+
- `ApplyTemplateCommand` for Sub-Project rows
- System rows (Level 0) display `"--"` for assignee (containers have no owner)

### Schedule Properties
| Property | Purpose |
|----------|---------|
| `Predecessors` | Raw predecessor ID string (editable via double-click) |
| `PredecessorDisplayText` | Computed — resolves GUIDs to WBS codes (e.g., `1.1.1.1`) for display |
| `TotalFloat` | Days of slack, shown in the "Slack" column |
| `IsCritical` | `true` when `TotalFloat == 0` (red text in UI) |
| `DurationDays` | Business-day duration |
| `ScheduleMode` | **Manual** (user-driven) or **Dynamic** (engine-driven). Manual mode respects user date entries and updates `DurationDays`. |

### Predecessor Display
The Predecessors column uses a **dual-binding** pattern:
- **Display mode** (`TextBlock`): binds to `PredecessorDisplayText` — shows human-readable WBS codes
- **Edit mode** (`TextBox`): binds to raw `Predecessors` — the actual ID string

The `PredecessorDisplayText` getter traverses the ViewModel tree to resolve each predecessor GUID to its `WbsValue`, returning a comma-separated list like `"1.1.1.1, 1.1.1.2"`.

## Column Layout
Key columns in `SystemManagementView.xaml`:
| Column | Width | SharedSizeGroup |
|--------|-------|-----------------|
| Name | `*` | — |
| Work (h) | 80 | WorkCol |
| BAC ($) | 80 | BacCol |
| Start Date | 100 | StartCol |
| End Date | 100 | EndCol |
| Slack | 60 | FloatCol |
| **Pred** | **150** | PredCol |
| Constraint | 100 | SnetCol |
| Duration | 60 | DurCol |

## Contextual Toolbar (shown when `CurrentView = "Systems"`)
- Add System, Add Item, Apply Template, Gate Progress, Delete Item, **RECONST PROJECT**

## Related Pages
- [[ProjectData & SystemItem]] — SystemItem model
- [[User & Role]] — role-based access control
- [[DataService]] — CRUD operations
- [[Apply Template Flow]] — template application
- [[TemplateService]] — template conversion
- [[ScheduleCalculationService]] — CPM engine (drives dates, float, critical path)
- [[Navigation & Ribbons]] — toolbar context
