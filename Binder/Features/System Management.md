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
| Delete item | `DeleteCommand` |
| Copy / Paste | `HandleCopy`, `HandlePaste`, `ExecutePaste` |
| Expansion state preservation | `RefreshData`, `OnDataChanged` |
| EVM Mode Awareness | `IsEvmHoursBased` / `EvmDisplayMode` |

## SystemHierarchyItemViewModel
Wrapper ViewModel for each tree row with:
- `IsExpanded` with `ToggleExpansionCommand`
- Name parsing: auto-numbers for Level 0–2, free-text for Level 3+
- `ApplyTemplateCommand` for Sub-Project rows

### Schedule Properties
| Property | Purpose |
|----------|---------|
| `Predecessors` | Raw predecessor ID string (editable via double-click) |
| `PredecessorDisplayText` | Computed — resolves GUIDs to WBS codes (e.g., `1.1.1.1`) for display |
| `TotalFloat` | Days of slack, shown in the "Slack" column |
| `IsCritical` | `true` when `TotalFloat == 0` (red text in UI) |
| `DurationDays` | Business-day duration |

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
- Add System, Add Item, Apply Template, Gate Progress, Delete Item

## Related Pages
- [[ProjectData & SystemItem]] — SystemItem model
- [[DataService]] — CRUD operations
- [[Apply Template Flow]] — template application
- [[TemplateService]] — template conversion
- [[ScheduleCalculationService]] — CPM engine (drives dates, float, critical path)
- [[Navigation & Ribbons]] — toolbar context
